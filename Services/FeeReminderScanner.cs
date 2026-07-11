using System.Text.Json;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// AWA-05: "When a fee due date approaches, the Backend API shall notify the parent to pay."
// AC: "Reminder fires at a configurable number of days before the due date." daysBeforeDue is
// owned by the caller (the hosted service reads it from configuration) rather than baked in
// here, so it stays configurable without a code change.
//
// Extracted from the hosted-service loop (FeeReminderHostedService), same split as
// NoLoginAlertScanner/NoLoginAlertHostedService for SDA-13 — this is directly unit-testable
// against an in-memory AppDbContext.
public static class FeeReminderScanner
{
    public static async Task ScanAsync(AppDbContext db, INotificationRouter notifications, DateTime nowUtc, int daysBeforeDue, CancellationToken cancellationToken = default)
    {
        var targetDate = DateOnly.FromDateTime(nowUtc).AddDays(daysBeforeDue);

        var dueFees = await db.FeeRecords
            .Where(f => f.Status == FeeStatus.Pending && f.DueDate == targetDate)
            .Include(f => f.Student)
            .ToListAsync(cancellationToken);

        foreach (var fee in dueFees)
        {
            var parentIds = await db.ParentWards
                .Where(w => w.StudentId == fee.StudentId)
                .Select(w => w.ParentUserId)
                .ToListAsync(cancellationToken);

            foreach (var parentId in parentIds)
            {
                if (await AlreadyRemindedAsync(db, parentId, fee.Id, cancellationToken))
                {
                    continue;
                }

                await notifications.RouteAsync(parentId, NotificationType.FeeReminder, new
                {
                    feeRecordId = fee.Id,
                    studentId = fee.StudentId,
                    studentName = fee.Student.FullName,
                    amount = fee.Amount,
                    dueDate = fee.DueDate,
                }, cancellationToken);
            }
        }
    }

    // No dedicated de-duplication table (would be a schema change requiring sign-off per
    // CLAUDE.md) — same pattern as NoLoginAlertScanner.AlreadyAlertedAsync: reads back recent
    // FeeReminder notifications for this parent and checks whether one already references this
    // exact fee record. Notification volume per parent is small enough that this stays cheap.
    private static async Task<bool> AlreadyRemindedAsync(AppDbContext db, Guid parentId, Guid feeRecordId, CancellationToken cancellationToken)
    {
        var recent = await db.Notifications
            .Where(n => n.RecipientId == parentId && n.Type == NotificationType.FeeReminder)
            .Select(n => n.Payload)
            .ToListAsync(cancellationToken);

        foreach (var payload in recent)
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("feeRecordId", out var fid) && fid.GetGuid() == feeRecordId)
            {
                return true;
            }
        }
        return false;
    }
}

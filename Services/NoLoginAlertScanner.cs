using System.Text.Json;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// SDA-13: "No-login-while-marked-present" alert. If a student is marked present but has not
// logged in within 20 minutes of the session start, notify the assigned teacher. AC: fires
// once per session, only after the 20-minute window has elapsed.
//
// Extracted from the hosted-service loop (NoLoginAlertHostedService) so the actual scan
// logic is directly unit-testable against an in-memory AppDbContext, rather than only being
// exercisable by letting a BackgroundService's infinite loop run.
public static class NoLoginAlertScanner
{
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(20);

    // Only scan a narrow band just past the 20-minute mark. Combined with the AlreadyAlerted
    // de-duplication check below, this means a session that's scanned on every poll interval
    // still only ever produces one notification — the band existing at all is just to keep
    // each poll's query cheap (it doesn't need to re-check sessions from hours ago).
    private static readonly TimeSpan ScanBandWidth = TimeSpan.FromMinutes(5);

    public static async Task ScanAsync(AppDbContext db, INotificationRouter notifications, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var bandStart = nowUtc - GraceWindow - ScanBandWidth;
        var bandEnd = nowUtc - GraceWindow;

        // AttendanceRecords don't carry the session's start time directly — that lives on
        // the TimetableSlot (StartTime, a TimeOnly, combined with the ClassSession's
        // SessionDate). Filtering that combination in SQL would need a computed column, so
        // this pulls today's-and-yesterday's present records (bounded, not "all present
        // records ever") and filters the combined timestamp in memory.
        var candidateDate = DateOnly.FromDateTime(nowUtc);
        var candidates = await db.AttendanceRecords
            .Where(r => r.Status == AttendanceStatus.Present)
            .Where(r => r.ClassSession.SessionDate == candidateDate || r.ClassSession.SessionDate == candidateDate.AddDays(-1))
            .Include(r => r.ClassSession).ThenInclude(cs => cs.TimetableSlot)
            .Include(r => r.Student)
            .ToListAsync(cancellationToken);

        foreach (var record in candidates)
        {
            var slot = record.ClassSession.TimetableSlot;
            var sessionStart = record.ClassSession.SessionDate.ToDateTime(slot.StartTime, DateTimeKind.Utc);
            if (sessionStart < bandStart || sessionStart >= bandEnd)
            {
                continue;
            }

            // #159: this used to be `CreatedAt >= sessionStart`, which only recognizes a
            // session created at-or-after the class started. A student who logged in the
            // evening before and never logged out has a still-open (IsActive) session with
            // an earlier CreatedAt — that's a legitimate login covering this class window,
            // not a no-login. Count a session as "logged in for this class" if it was either
            // created during/after the window, or is still active from before it.
            var hasLoggedIn = await db.UserSessions
                .AnyAsync(s => s.UserId == record.StudentId && (s.CreatedAt >= sessionStart || s.IsActive), cancellationToken);
            if (hasLoggedIn)
            {
                continue;
            }

            if (await AlreadyAlertedAsync(db, slot.TeacherId, record.StudentId, record.ClassSessionId, cancellationToken))
            {
                continue;
            }

            await notifications.RouteAsync(slot.TeacherId, NotificationType.AbsencePing, new
            {
                studentId = record.StudentId,
                studentName = record.Student.FullName,
                classSessionId = record.ClassSessionId,
                sectionId = slot.SectionId,
                subjectId = slot.SubjectId,
                sessionStart,
            }, cancellationToken);
        }
    }

    // No dedicated de-duplication table (would be a schema change requiring sign-off per
    // CLAUDE.md) — instead this reads back recently-created AbsencePing notifications for
    // this teacher and checks whether one already references this exact (student, session)
    // pair. Notification volume for one teacher is small enough that this stays cheap.
    private static async Task<bool> AlreadyAlertedAsync(AppDbContext db, Guid teacherId, Guid studentId, Guid classSessionId, CancellationToken cancellationToken)
    {
        var recent = await db.Notifications
            .Where(n => n.RecipientId == teacherId && n.Type == NotificationType.AbsencePing)
            .Select(n => n.Payload)
            .ToListAsync(cancellationToken);

        foreach (var payload in recent)
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("studentId", out var sid) && sid.GetGuid() == studentId &&
                root.TryGetProperty("classSessionId", out var csid) && csid.GetGuid() == classSessionId)
            {
                return true;
            }
        }
        return false;
    }
}

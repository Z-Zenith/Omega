using System.Security.Claims;
using System.Text.Json;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/fees")]
[Authorize]
public class FeesController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // AWA-04 (Track 2). One FeeRecord per link, so the link is only ever valid for the
    // exact amount/due-date it was generated with — a link can't later be reused/edited
    // to cover a different period without creating a new record.
    [HttpPost("links")]
    public async Task<ActionResult<FeeLinkResponse>> CreateLink(CreateFeeLinkRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "manage_fees"))
        {
            return Forbid();
        }

        if (request.Amount <= 0 || request.Amount > 10_000_000m)
        {
            return BadRequest(new { error = "invalid_amount", message = "Fee amount must be greater than zero and no more than 10,000,000." });
        }

        if (request.DueDate == default || request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new { error = "invalid_due_date", message = "Due date must be a real date that isn't in the past." });
        }

        var student = await db.Users.FindAsync(request.StudentId);
        if (student is null || student.AccountType != AccountType.Student)
        {
            return BadRequest(new { error = "unknown_student", message = "No student exists with that id." });
        }

        var feeRecord = new FeeRecord
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = FeeStatus.Pending,
        };
        // The Payment Gateway integration itself is out of scope here (external system,
        // per architecture doc Section 4) — the link just needs to resolve, via the
        // fee record id, to exactly this amount/due-date pair.
        feeRecord.PaymentLink = $"https://payments.campus.local/pay/{feeRecord.Id}";

        db.FeeRecords.Add(feeRecord);
        await db.SaveChangesAsync();

        return Ok(new FeeLinkResponse(feeRecord.Id, feeRecord.PaymentLink, feeRecord.Amount, feeRecord.DueDate, feeRecord.Status.ToString()));
    }

    // PRT-03 — pays via the (stubbed) Payment Gateway and reflects the confirmed status
    // synchronously, so the parent never needs to check a separate confirmation email.
    [HttpPost("{id}/pay")]
    public async Task<ActionResult<PayFeeResponse>> Pay(Guid id)
    {
        // Existence and ownership collapse into a single Forbid so an unauthorized caller can't
        // distinguish "this fee doesn't exist" from "this fee isn't yours" by probing IDs.
        var fee = await db.FeeRecords.FindAsync(id);
        if (fee is null || await ParentWardAccess.GetAuthorizedParentIdAsync(db, User, fee.StudentId) is null)
        {
            return Forbid();
        }

        var gatewayTxnId = $"sim_{Guid.NewGuid():N}";
        var processedAt = DateTime.UtcNow;

        // Atomic conditional update closes the check-then-act race between concurrent pay
        // requests for the same fee — only the request that actually flips the status runs
        // the state transition; a losing concurrent request sees rowsUpdated == 0.
        var rowsUpdated = await db.FeeRecords
            .Where(f => f.Id == id && f.Status != FeeStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FeeStatus.Paid)
                .SetProperty(f => f.PaidAt, processedAt));

        if (rowsUpdated == 0)
        {
            return Conflict(new { error = "already_paid", message = "This fee has already been paid." });
        }

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            FeeRecordId = fee.Id,
            GatewayTxnId = gatewayTxnId,
            Status = "confirmed",
            ProcessedAt = processedAt,
        });
        await db.SaveChangesAsync();

        return Ok(new PayFeeResponse(fee.Id, FeeStatus.Paid.ToString(), processedAt, gatewayTxnId));
    }

    // AWA-05: "reminder fires at a configurable number of days before the due date" —
    // daysBefore is that configuration. No scheduler exists yet to call this
    // automatically (that's separate infra); exposed as an Admin/Finance-triggered
    // action for now. Writes directly to the existing shared `notifications` table
    // rather than introducing a new "Notification Router" abstraction — that's larger
    // shared infrastructure Track 1 also depends on and needs its own coordination
    // (CLAUDE.md: the Notification Router is shared code).
    [HttpPost("reminders")]
    public async Task<ActionResult<SendFeeRemindersResponse>> SendPaymentReminders([FromQuery] int daysBefore = 7)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "manage_fees"))
        {
            return Forbid();
        }
        if (daysBefore < 0)
        {
            return BadRequest(new { error = "invalid_days_before", message = "daysBefore must not be negative." });
        }

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysBefore));
        var dueSoon = await db.FeeRecords
            .Where(f => f.Status == FeeStatus.Pending && f.DueDate == targetDate)
            .ToListAsync();

        var notifiedParentIds = new List<Guid>();
        foreach (var fee in dueSoon)
        {
            var parentIds = await db.ParentWards
                .Where(w => w.StudentId == fee.StudentId)
                .Select(w => w.ParentUserId)
                .ToListAsync();

            foreach (var parentId in parentIds)
            {
                var payloadMarker = fee.Id.ToString();
                var alreadyReminded = await db.Notifications
                    .AnyAsync(n => n.RecipientId == parentId && n.Payload.Contains(payloadMarker));
                if (alreadyReminded)
                {
                    continue;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    type = "FeeReminder",
                    feeRecordId = fee.Id,
                    amount = fee.Amount,
                    dueDate = fee.DueDate,
                });
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    RecipientId = parentId,
                    Payload = payload,
                    CreatedAt = DateTime.UtcNow,
                });
                notifiedParentIds.Add(parentId);
            }
        }
        await db.SaveChangesAsync();

        return Ok(new SendFeeRemindersResponse(dueSoon.Count, notifiedParentIds));
    }

    // PRT-02
    [HttpGet("ward/{studentId}")]
    [ServiceFilter(typeof(WardAccessFilter))]
    public async Task<ActionResult<IReadOnlyList<WardFeeDto>>> Ward(Guid studentId)
    {
        var fees = await db.FeeRecords
            .Where(f => f.StudentId == studentId)
            .OrderByDescending(f => f.DueDate)
            .Select(f => new WardFeeDto(f.Id, f.Amount, f.DueDate, f.Status.ToString(), f.PaidAt))
            .ToListAsync();

        return Ok(fees);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

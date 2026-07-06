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
public class FeesController(AppDbContext db) : ControllerBase
{
    // AWA-04 (Track 2)
    [HttpPost("links")]
    public IActionResult CreateLink() => StatusCode(501, new { feature = "AWA-04", status = "not_implemented" });

    // PRT-03 — pays via the (stubbed) Payment Gateway and reflects the confirmed status
    // synchronously, so the parent never needs to check a separate confirmation email.
    [HttpPost("{id}/pay")]
    [Authorize]
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

    // PRT-02
    [HttpGet("ward/{studentId}")]
    [Authorize]
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
}

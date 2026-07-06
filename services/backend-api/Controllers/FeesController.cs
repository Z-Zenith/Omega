using System.Security.Claims;
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

        if (request.Amount <= 0)
        {
            return BadRequest(new { error = "invalid_amount", message = "Fee amount must be greater than zero." });
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

    // PRT-03
    [HttpPost("{id}/pay")]
    public IActionResult Pay(Guid id) => StatusCode(501, new { feature = "PRT-03", status = "not_implemented" });

    // PRT-02
    [HttpGet("ward/{studentId}")]
    public IActionResult Ward(Guid studentId) => StatusCode(501, new { feature = "PRT-02", status = "not_implemented" });

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

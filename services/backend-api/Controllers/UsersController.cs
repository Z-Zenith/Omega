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
[Route("api/v1/users")]
[Authorize]
public class UsersController(AppDbContext db, IPasswordHasher passwordHasher, ITotpService totpService, IPermissionService permissions) : ControllerBase
{
    // AWA-09: account creation. Returns the TOTP provisioning URI once, at creation time,
    // since SDA-02/TWA-03 login always requires a TOTP code alongside password.
    //
    // Gated on manage_accounts now that the controller carries [Authorize] (added for
    // AWA-07): previously this action had no authentication at all, so adding
    // authentication alone would have left it reachable — and unrestricted — by any
    // logged-in account of any role. That's a wider hole than before, not a narrower
    // one, so the permission check has to land in the same change that adds [Authorize].
    [HttpPost]
    public async Task<ActionResult<CreateUserResponse>> Create(CreateUserRequest request)
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }
        if (!await permissions.HasPermissionAsync(caller.Id, "manage_accounts"))
        {
            return Forbid();
        }

        var totpSecret = totpService.GenerateSecret();

        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = request.CollegeId,
            AccountType = request.AccountType,
            Identifier = request.Identifier,
            PasswordHash = passwordHasher.Hash(request.InitialPassword),
            TotpSecret = totpSecret,
            FullName = request.FullName,
            DepartmentId = request.DepartmentId,
            IsActive = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var provisioningUri = totpService.BuildProvisioningUri(totpSecret, request.Identifier, "Campus Platform");
        return CreatedAtAction(nameof(Create), new { id = user.Id }, new CreateUserResponse(user.Id, provisioningUri, totpSecret));
    }

    // AWA-07, AWA-08. Self-view needs no special permission; viewing another user's
    // record is scoped to students only and requires view_all_student_records — this
    // endpoint doesn't double as a general "view any employee's profile" API.
    [HttpGet("{id}/profile")]
    public async Task<ActionResult<StudentRecordDto>> GetProfile(Guid id)
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var isSelf = caller.Id == id;
        if (!isSelf
            && (user.AccountType != AccountType.Student
                || user.CollegeId != caller.CollegeId
                || !await permissions.HasPermissionAsync(caller.Id, "view_all_student_records")))
        {
            return Forbid();
        }

        // r.Teacher.FullName in the projection below joins regardless of Teacher.IsActive —
        // a remark must survive the submitting teacher being deactivated later (AWA-07
        // acceptance criterion).
        var remarks = await db.TeacherReports
            .Where(r => r.StudentId == id)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new TeacherRemarkDto(r.Id, r.TeacherId, r.Teacher.FullName, r.Content, r.SubmittedAt))
            .ToListAsync();

        var browsingSummaries = await db.BrowsingHistorySummaries
            .Where(s => s.StudentId == id)
            .OrderByDescending(s => s.GeneratedAt)
            .Select(s => new BrowsingSummaryReportDto(s.Id, s.SummaryText, s.GeneratedAt))
            .ToListAsync();

        var suspiciousFlags = await db.SuspiciousFlags
            .Where(f => f.StudentId == id)
            .OrderByDescending(f => f.FlaggedAt)
            .Select(f => new SuspiciousFlagReportDto(f.Id, f.ConfidenceScore, f.FlaggedAt, f.AssignmentId, f.ClassSessionId))
            .ToListAsync();

        return Ok(new StudentRecordDto(
            user.Id,
            user.FullName,
            user.Identifier,
            user.AccountType.ToString(),
            user.CollegeId,
            user.DepartmentId,
            user.IsActive,
            remarks,
            browsingSummaries,
            suspiciousFlags));
    }

    // AWA-10. Same reasoning as Create above: gated on reset_password so that adding
    // [Authorize] to this controller (for AWA-07) doesn't leave this reachable — and
    // able to take over any account — for any authenticated caller regardless of role.
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }
        if (!await permissions.HasPermissionAsync(caller.Id, "reset_password"))
        {
            return Forbid();
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<User?> CurrentUserAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        return await db.Users.FindAsync(userId);
    }
}

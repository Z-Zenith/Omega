using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/parent")]
public class ParentController(AppDbContext db, IJwtTokenService jwtTokenService) : ControllerBase
{
    // PRT-01 — roll number + DOB only, no TOTP. The credential identifies the ward, not a
    // separate parent identity; a parent account must already be linked to that ward via
    // parent_wards (provisioned out-of-band by admin account management, AWA-09/10).
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ParentLoginResponse>> Login(ParentLoginRequest request)
    {
        var student = await db.Users
            .Where(u => u.Identifier == request.RollNumber && u.AccountType == AccountType.Student)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync();

        if (student is null || student.DateOfBirth != request.DateOfBirth)
        {
            return Unauthorized(new { error = "invalid_credentials", message = "No student matches that roll number and date of birth." });
        }

        if (!student.IsActive)
        {
            return Unauthorized(new { error = "account_inactive", message = "This student's account has been deactivated." });
        }

        var wardLink = await db.ParentWards
            .Include(w => w.ParentUser)
            .Where(w => w.StudentId == student.Id)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync();

        if (wardLink is null || !wardLink.ParentUser.IsActive)
        {
            return Unauthorized(new { error = "no_registered_parent", message = "No parent account is registered for this student." });
        }

        var parent = wardLink.ParentUser;

        var existingActiveSessions = await db.UserSessions
            .Where(s => s.UserId == parent.Id && s.IsActive)
            .ToListAsync();
        foreach (var session in existingActiveSessions)
        {
            session.IsActive = false;
        }

        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = parent.Id,
            DeviceInfo = request.DeviceInfo,
            IsActive = true,
        };
        db.UserSessions.Add(newSession);
        await db.SaveChangesAsync();

        var token = jwtTokenService.IssueToken(parent, newSession.Id, student.Id);
        return Ok(new ParentLoginResponse(token, parent.Id, newSession.Id, student.Id, student.FullName, student.Identifier));
    }
}

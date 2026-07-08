using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/parent")]
public class ParentController(AppDbContext db, IJwtTokenService jwtTokenService) : ControllerBase
{
    // PRT-01 — roll number + DOB only, no TOTP. The credential identifies the ward, not a
    // separate parent identity; a parent account must already be linked to that ward via
    // parent_wards (provisioned out-of-band by admin account management, AWA-09/10).
    // Rate limited (#79): DOB-only auth is guessable, so this endpoint gets the same
    // IP-keyed sliding-window limiter as AuthController.Login.
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicies.Auth)]
    public async Task<ActionResult<ParentLoginResponse>> Login(ParentLoginRequest request)
    {
        // Identifier is only unique per (college_id, identifier), not globally — a roll number
        // can legitimately collide across colleges. Fail closed on ambiguity rather than
        // arbitrarily picking one match, since that could silently authenticate a parent
        // against a different college's student.
        var matchingStudents = await db.Users
            .Where(u => u.Identifier == request.RollNumber && u.AccountType == AccountType.Student)
            .ToListAsync();

        var student = matchingStudents.Count == 1 ? matchingStudents[0] : null;

        if (student is null)
        {
            return Unauthorized(new { error = "invalid_credentials", message = "No student matches that roll number and date of birth." });
        }

        if (student.DateOfBirth is null)
        {
            // date_of_birth is nullable and not backfilled for students created before this
            // feature shipped — surface that distinctly rather than a generic mismatch, since
            // no DOB entered by the parent could ever satisfy the check below.
            return Unauthorized(new { error = "dob_not_set", message = "This student's date of birth hasn't been recorded yet; contact the school to enable parent login." });
        }

        if (student.DateOfBirth != request.DateOfBirth)
        {
            return Unauthorized(new { error = "invalid_credentials", message = "No student matches that roll number and date of birth." });
        }

        if (!student.IsActive)
        {
            return Unauthorized(new { error = "account_inactive", message = "This student's account has been deactivated." });
        }

        var wardLinks = await db.ParentWards
            .Include(w => w.ParentUser)
            .Where(w => w.StudentId == student.Id)
            .ToListAsync();

        if (wardLinks.Count == 0)
        {
            return Unauthorized(new { error = "no_registered_parent", message = "No parent account is registered for this student." });
        }

        if (wardLinks.Count > 1)
        {
            // The roll number + DOB credential identifies the ward, not which parent is
            // logging in. With more than one guardian registered we can't safely tell them
            // apart, so fail closed rather than always resolving to the same one.
            return Unauthorized(new { error = "ambiguous_parent", message = "Multiple parent accounts are registered for this student; contact the school to resolve this before signing in." });
        }

        var wardLink = wardLinks[0];
        if (!wardLink.ParentUser.IsActive)
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

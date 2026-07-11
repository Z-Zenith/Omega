using System.Security.Claims;
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
[Route("api/v1/auth")]
public class AuthController(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    // SDA-02, TWA-03: roll number/username + password + TOTP code.
    // Acceptance criteria requires a distinct rejection message per failure reason.
    // Rate limited (#79): no lockout otherwise existed on this endpoint, repo-wide.
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicies.Auth)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        // Identifier is only unique per (college_id, identifier) - see users_college_id_identifier_key
        // - not globally, so a roll number/username can legitimately collide across colleges
        // (#151). Fail closed on ambiguity rather than deterministically picking one match
        // (e.g. oldest by CreatedAt): silently authenticating against the wrong college's
        // account would fail the password check against the wrong hash and permanently lock
        // that user out with a misleading "invalid password" error. Mirrors the same
        // fail-closed pattern already used in ParentController.Login.
        var matchingUsers = await db.Users
            .Where(u => u.Identifier == request.Identifier)
            .ToListAsync();

        if (matchingUsers.Count > 1)
        {
            return Unauthorized(new
            {
                error = "identifier_ambiguous",
                message = "This roll number/username exists at more than one institution. Contact your institution's admin to resolve this.",
            });
        }

        var user = matchingUsers.SingleOrDefault();

        if (user is null)
        {
            return Unauthorized(new { error = "unknown_identifier", message = "No account with that roll number/username." });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { error = "account_inactive", message = "This account has been deactivated." });
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "invalid_password", message = "Incorrect password." });
        }

        if (string.IsNullOrEmpty(user.TotpSecret) || !totpService.ValidateCode(user.TotpSecret, request.TotpCode))
        {
            return Unauthorized(new { error = "invalid_totp", message = "Incorrect or expired authentication code." });
        }

        var existingActiveSessions = await db.UserSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .ToListAsync();
        foreach (var session in existingActiveSessions)
        {
            session.IsActive = false;
        }

        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceInfo = request.DeviceInfo,
            IsActive = true,
        };
        db.UserSessions.Add(newSession);
        await db.SaveChangesAsync();

        var token = jwtTokenService.IssueToken(user, newSession.Id);
        return Ok(new LoginResponse(token, user.Id, newSession.Id, user.AccountType.ToString(), user.FullName));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var sessionId = Guid.Parse(User.FindFirstValue("session_id")!);
        var session = await db.UserSessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.IsActive = false;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // Session activity/ownership is enforced globally by SessionActiveFilter before this
    // action runs (#77) — no need to re-check UserSessions.IsActive here.
    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult<SessionInfoResponse>> GetSession()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var sessionId = Guid.Parse(User.FindFirstValue("session_id")!);

        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new SessionInfoResponse(user.Id, sessionId, user.AccountType.ToString(), user.FullName, user.CollegeId));
    }

    // SDA-23: password change requires a fresh, successful TOTP challenge. Session
    // activity/ownership is enforced globally by SessionActiveFilter (#77).
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { error = "invalid_password", message = "Incorrect current password." });
        }

        if (string.IsNullOrEmpty(user.TotpSecret) || !totpService.ValidateCode(user.TotpSecret, request.TotpCode))
        {
            return Unauthorized(new { error = "invalid_totp", message = "TOTP challenge failed." });
        }

        // #140: same minimum strength policy as account creation and admin-initiated reset.
        if (!PasswordPolicy.IsValid(request.NewPassword, out var passwordError))
        {
            return BadRequest(new { error = "weak_password", message = passwordError });
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // #132 — a password change must also cut off any session issued under the old
        // password. Before this, only Login flipped old sessions to IsActive=false (on the
        // *next* login); ChangePassword itself never touched UserSessions, so a JWT issued to
        // an attacker who'd phished the old credentials+TOTP stayed valid for the rest of its
        // ~60-minute lifetime even after the victim "fixed" it here. Mirrors Login's
        // single-active-session flip, applied immediately rather than on next login — the
        // caller already has the new password and can simply log back in.
        var activeSessions = await db.UserSessions.Where(s => s.UserId == userId && s.IsActive).ToListAsync();
        foreach (var session in activeSessions)
        {
            session.IsActive = false;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }
}

using System.Security.Claims;
using BackendApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// #77 — before this, UserSessions.IsActive was only re-checked ad hoc inside AuthController
// (Logout/GetSession/ChangePassword). Every other [Authorize] controller relied solely on JWT
// signature/expiry, so a token from a session that had been revoked (second-device login, or
// explicit logout) kept working against Timetable/Marks/Fees/Assignments/Community etc. for
// the rest of its ~60-minute lifetime. Registered globally (Program.cs) so a new controller is
// covered by default instead of depending on remembering to paste this check into it.
public class SessionActiveFilter(AppDbContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Filters.Any(f => f is IAllowAnonymousFilter))
        {
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            // No token at all — leave it to [Authorize] (or the endpoint's own AllowAnonymous
            // status) to produce the right response rather than second-guessing it here.
            return;
        }

        var sessionIdClaim = user.FindFirstValue("session_id");
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(sessionIdClaim, out var sessionId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var session = await db.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null || !session.IsActive || session.UserId != userId)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "session_revoked", message = "This session is no longer active." });
        }
    }
}

using System.Security.Claims;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BackendApi.Hubs;

// Notification Router (shared) — real-time transport for every ping/alert the router fans
// out (SDA-12/13, TWA-11/13, AWA-05, ...). Each connection joins a group keyed by the
// authenticated user's id so NotificationRouter.RouteAsync can push straight to a recipient
// without a separate routing table. Clients only ever receive their own notifications.
[Authorize]
public class NotificationsHub(ISessionActivityService sessionActivityService) : Hub
{
    // #130 — [Authorize] only proves the JWT is cryptographically valid and unexpired; it says
    // nothing about whether the session behind it has since been revoked (logout, second-device
    // login, or — as of #132 — a password change/reset). SessionActiveFilter enforces that for
    // every MVC controller action, but this hub is a SignalR Hub, not a controller, so it never
    // ran through that filter — a captured/leaked token could keep receiving the victim's
    // real-time notifications for the rest of the JWT's ~60-minute lifetime even after the
    // session was revoked. Re-check the same UserSessions.IsActive/ownership condition here via
    // the shared ISessionActivityService and abort the connection outright if it fails.
    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        var sessionIdClaim = Context.User?.FindFirstValue("session_id");

        if (!Guid.TryParse(userIdClaim, out var userId)
            || !Guid.TryParse(sessionIdClaim, out var sessionId)
            || !await sessionActivityService.IsSessionActiveAsync(userId, sessionId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid userId) => $"user:{userId}";
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BackendApi.Hubs;

// Notification Router (shared) — real-time transport for every ping/alert the router fans
// out (SDA-12/13, TWA-11/13, AWA-05, ...). Each connection joins a group keyed by the
// authenticated user's id so NotificationRouter.RouteAsync can push straight to a recipient
// without a separate routing table. Clients only ever receive their own notifications.
[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid userId) => $"user:{userId}";
}

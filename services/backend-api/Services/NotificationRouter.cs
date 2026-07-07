using System.Text.Json;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BackendApi.Services;

// Notification Router (shared) — see INotificationRouter for the contract. Persists to the
// notifications table (source of truth / offline catch-up) and pushes the same event over
// SignalR to whichever clients are connected to NotificationsHub for that recipient, so
// delivery happens within a few seconds while the recipient is online (SDA-12 AC).
public class NotificationRouter(AppDbContext db, IHubContext<NotificationsHub> hub) : INotificationRouter
{
    public async Task<Notification> RouteAsync(Guid recipientId, NotificationType type, object payload, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            Type = type,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        await hub.Clients.Group(NotificationsHub.GroupName(recipientId)).SendAsync(
            "notificationReceived",
            new
            {
                id = notification.Id,
                type = notification.Type.ToString(),
                payload,
                createdAt = notification.CreatedAt,
            },
            cancellationToken);

        return notification;
    }
}

using BackendApi.Data.Entities;

namespace BackendApi.Services;

// Notification Router (shared) — the single place, per docs/Campus platform architecture.md
// section on the Notification Router, that owns every ping/alert/routing requirement
// (exit-ping, absence-ping, report routing, timetable-change routing, fee reminders, ...).
// Callers persist a notification and fan it out over the real-time transport through this
// one entry point rather than writing to the notifications table directly.
public interface INotificationRouter
{
    Task<Notification> RouteAsync(Guid recipientId, NotificationType type, object payload, CancellationToken cancellationToken = default);
}

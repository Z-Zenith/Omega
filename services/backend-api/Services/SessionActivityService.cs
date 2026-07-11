using BackendApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// #130/#77 — the "is this session still active and owned by this user" check used to live
// solely inside SessionActiveFilter, which only runs for MVC controller actions
// (AddControllers(...).Filters). NotificationsHub (a SignalR Hub, not a controller) never
// went through it, so a revoked session's JWT kept working against the notifications hub
// for the rest of the token's lifetime. Pulled the check out into its own scoped service so
// both SessionActiveFilter and NotificationsHub share one implementation instead of the hub
// growing its own copy that could drift out of sync.
public interface ISessionActivityService
{
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId);
}

public class SessionActivityService(AppDbContext db) : ISessionActivityService
{
    public async Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId)
    {
        var session = await db.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        return session is not null && session.IsActive && session.UserId == userId;
    }
}

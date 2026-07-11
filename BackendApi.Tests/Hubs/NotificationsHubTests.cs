using System.Security.Claims;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Hubs;
using BackendApi.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Hubs;

// #130 — NotificationsHub is a SignalR Hub, not an MVC controller, so SessionActiveFilter
// (Program.cs AddControllers(...).Filters) never runs for it. [Authorize] alone only proves
// the JWT is cryptographically valid/unexpired, not that the session behind it is still
// active. These tests exercise OnConnectedAsync directly against the shared
// ISessionActivityService the same way SessionActiveFilterTests exercises the filter.
public class NotificationsHubTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal AuthenticatedUser(Guid userId, Guid sessionId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("session_id", sessionId.ToString())],
        "TestAuth"));

    [Fact]
    public async Task OnConnectedAsync_AbortsConnection_WhenSessionIsRevoked()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = userId, IsActive = false });
        await db.SaveChangesAsync();

        var context = new FakeHubCallerContext(AuthenticatedUser(userId, sessionId));
        var groups = new FakeGroupManager();
        var hub = new NotificationsHub(new SessionActivityService(db)) { Context = context, Groups = groups };

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Added);
    }

    // Defends against a session_id claim being replayed with a mismatched sub, same as
    // SessionActiveFilterTests.RejectsRequest_WhenSessionBelongsToDifferentUser.
    [Fact]
    public async Task OnConnectedAsync_AbortsConnection_WhenSessionBelongsToDifferentUser()
    {
        await using var db = NewDb();
        var sessionOwnerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = sessionOwnerId, IsActive = true });
        await db.SaveChangesAsync();

        var context = new FakeHubCallerContext(AuthenticatedUser(callerId, sessionId));
        var groups = new FakeGroupManager();
        var hub = new NotificationsHub(new SessionActivityService(db)) { Context = context, Groups = groups };

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnectedAsync_JoinsUserGroup_WhenSessionIsActiveAndOwnedByCaller()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = userId, IsActive = true });
        await db.SaveChangesAsync();

        var context = new FakeHubCallerContext(AuthenticatedUser(userId, sessionId));
        var groups = new FakeGroupManager();
        var hub = new NotificationsHub(new SessionActivityService(db)) { Context = context, Groups = groups };

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        Assert.Contains((context.ConnectionId, NotificationsHub.GroupName(userId)), groups.Added);
    }

    private class FakeHubCallerContext(ClaimsPrincipal user) : HubCallerContext
    {
        public bool Aborted { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString();

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User { get; } = user;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Aborted = true;
    }

    private class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

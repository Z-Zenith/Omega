using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Hubs;
using BackendApi.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Services;

// SDA-12 — exercises the Notification Router's public RouteAsync surface: it must persist
// the notification and push it to the recipient's SignalR group (not broadcast, not any
// other group), without needing a live hub connection.
public class NotificationRouterTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RouteAsync_PersistsNotification_ForTheRecipient()
    {
        using var db = NewDb();
        var recipientId = Guid.NewGuid();
        var router = new NotificationRouter(db, new FakeHubContext());

        var notification = await router.RouteAsync(recipientId, NotificationType.ExitPing, new { studentId = Guid.NewGuid() });

        var stored = await db.Notifications.SingleAsync();
        Assert.Equal(notification.Id, stored.Id);
        Assert.Equal(recipientId, stored.RecipientId);
        Assert.Equal(NotificationType.ExitPing, stored.Type);
        Assert.Contains("studentId", stored.Payload);
    }

    [Fact]
    public async Task RouteAsync_PushesOnlyToTheRecipientsGroup()
    {
        using var db = NewDb();
        var recipientId = Guid.NewGuid();
        var hub = new FakeHubContext();
        var router = new NotificationRouter(db, hub);

        await router.RouteAsync(recipientId, NotificationType.ExitPing, new { ping = true });

        Assert.Equal(NotificationsHub.GroupName(recipientId), hub.Clients.LastGroupName);
        Assert.Equal("notificationReceived", hub.Clients.LastGroupProxy?.LastMethod);
    }

    private class FakeHubContext : IHubContext<NotificationsHub>
    {
        public FakeHubClients Clients { get; } = new();

        IHubClients IHubContext<NotificationsHub>.Clients => Clients;

        public IGroupManager Groups => throw new NotSupportedException("Not needed by NotificationRouter.");
    }

    private class FakeHubClients : IHubClients
    {
        public string? LastGroupName { get; private set; }

        public FakeClientProxy? LastGroupProxy { get; private set; }

        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy Group(string groupName)
        {
            LastGroupName = groupName;
            LastGroupProxy = new FakeClientProxy();
            return LastGroupProxy;
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private class FakeClientProxy : IClientProxy
    {
        public string? LastMethod { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            LastMethod = method;
            return Task.CompletedTask;
        }
    }
}

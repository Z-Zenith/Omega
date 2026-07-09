using System.Security.Claims;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

// Notification Router (shared) — GET /notifications and the mark-as-read endpoint.
// NotificationRouterTests (Services) covers RouteAsync's persistence + SignalR push;
// these tests cover the read side: a caller only ever sees/marks their own rows.
public class MessagingControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = AccountType.Student,
        IsActive = true,
    };

    private static MessagingController ControllerAs(AppDbContext db, Guid userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth"));
        return new MessagingController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    private static Notification NewNotification(Guid recipientId, DateTime createdAt, DateTime? readAt = null) => new()
    {
        Id = Guid.NewGuid(),
        RecipientId = recipientId,
        Type = NotificationType.ExitPing,
        Payload = "{\"studentId\":\"" + Guid.NewGuid() + "\"}",
        CreatedAt = createdAt,
        ReadAt = readAt,
    };

    [Fact]
    public async Task ListNotifications_ReturnsOnlyTheCallersNotifications_MostRecentFirst()
    {
        await using var db = NewDb();
        var caller = NewUser();
        var other = NewUser();
        db.Users.AddRange(caller, other);

        var older = NewNotification(caller.Id, DateTime.UtcNow.AddMinutes(-10));
        var newer = NewNotification(caller.Id, DateTime.UtcNow);
        var othersNotification = NewNotification(other.Id, DateTime.UtcNow);
        db.Notifications.AddRange(older, newer, othersNotification);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, caller.Id);
        var result = await controller.ListNotifications();

        var page = Assert.IsType<OkObjectResult>(result.Result).Value as Contracts.NotificationsPageResponse;
        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(2, page.Notifications.Count);
        Assert.Equal(newer.Id, page.Notifications[0].Id);
        Assert.Equal(older.Id, page.Notifications[1].Id);
        Assert.DoesNotContain(page.Notifications, n => n.Id == othersNotification.Id);
    }

    [Fact]
    public async Task ListNotifications_Paginates()
    {
        await using var db = NewDb();
        var caller = NewUser();
        db.Users.Add(caller);

        for (var i = 0; i < 5; i++)
        {
            db.Notifications.Add(NewNotification(caller.Id, DateTime.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, caller.Id);
        var firstPage = await controller.ListNotifications(page: 1, pageSize: 2);
        var firstPageBody = Assert.IsType<OkObjectResult>(firstPage.Result).Value as Contracts.NotificationsPageResponse;
        Assert.NotNull(firstPageBody);
        Assert.Equal(5, firstPageBody!.TotalCount);
        Assert.Equal(2, firstPageBody.Notifications.Count);

        var secondPage = await controller.ListNotifications(page: 2, pageSize: 2);
        var secondPageBody = Assert.IsType<OkObjectResult>(secondPage.Result).Value as Contracts.NotificationsPageResponse;
        Assert.NotNull(secondPageBody);
        Assert.Equal(2, secondPageBody!.Notifications.Count);
        Assert.DoesNotContain(secondPageBody.Notifications, n => firstPageBody.Notifications.Select(x => x.Id).Contains(n.Id));
    }

    [Fact]
    public async Task ListNotifications_ReportsIsReadFromReadAt()
    {
        await using var db = NewDb();
        var caller = NewUser();
        db.Users.Add(caller);
        var read = NewNotification(caller.Id, DateTime.UtcNow, readAt: DateTime.UtcNow);
        var unread = NewNotification(caller.Id, DateTime.UtcNow.AddMinutes(-1));
        db.Notifications.AddRange(read, unread);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, caller.Id);
        var result = await controller.ListNotifications();
        var page = Assert.IsType<OkObjectResult>(result.Result).Value as Contracts.NotificationsPageResponse;

        Assert.True(page!.Notifications.Single(n => n.Id == read.Id).IsRead);
        Assert.False(page.Notifications.Single(n => n.Id == unread.Id).IsRead);
    }

    [Fact]
    public async Task MarkNotificationRead_MarksTheCallersOwnNotification_AndIsIdempotent()
    {
        await using var db = NewDb();
        var caller = NewUser();
        db.Users.Add(caller);
        var notification = NewNotification(caller.Id, DateTime.UtcNow);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, caller.Id);
        var firstResult = await controller.MarkNotificationRead(notification.Id);
        var firstBody = Assert.IsType<OkObjectResult>(firstResult.Result).Value as Contracts.MarkNotificationReadResponse;
        Assert.NotNull(firstBody);

        var secondResult = await controller.MarkNotificationRead(notification.Id);
        var secondBody = Assert.IsType<OkObjectResult>(secondResult.Result).Value as Contracts.MarkNotificationReadResponse;

        // Idempotent: the second call doesn't overwrite the original ReadAt.
        Assert.Equal(firstBody!.ReadAt, secondBody!.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationRead_ReturnsNotFound_ForAnotherUsersNotification()
    {
        await using var db = NewDb();
        var owner = NewUser();
        var intruder = NewUser();
        db.Users.AddRange(owner, intruder);
        var notification = NewNotification(owner.Id, DateTime.UtcNow);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, intruder.Id);
        var result = await controller.MarkNotificationRead(notification.Id);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Null((await db.Notifications.FindAsync(notification.Id))!.ReadAt);
    }
}

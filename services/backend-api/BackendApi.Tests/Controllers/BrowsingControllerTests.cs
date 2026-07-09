using System.Security.Claims;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

// SDA-04 + Notification Router (shared) — approving a whitelist request routes a
// WhitelistRequest notification back to the original requester. NotificationRouterTests
// (Services) covers RouteAsync itself; this covers that ApproveWhitelistRequest actually
// calls it, for the right recipient, only on the request that's genuinely approved.
public class BrowsingControllerTests
{
    // No test in this file needs real SignalR delivery — NotificationRouterTests owns
    // that. This just records what ApproveWhitelistRequest routed.
    private class RecordingNotificationRouter : INotificationRouter
    {
        public List<(Guid RecipientId, NotificationType Type)> Routed { get; } = new();

        public Task<Notification> RouteAsync(Guid recipientId, NotificationType type, object payload, CancellationToken cancellationToken = default)
        {
            Routed.Add((recipientId, type));
            return Task.FromResult(new Notification { Id = Guid.NewGuid(), RecipientId = recipientId, Type = type });
        }
    }

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(Guid collegeId, AccountType accountType) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = collegeId,
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static BrowsingController ControllerAs(AppDbContext db, User user, RecordingNotificationRouter router)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new BrowsingController(db, router)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    [Fact]
    public async Task ApproveWhitelistRequest_NotifiesTheOriginalRequester()
    {
        await using var db = NewDb();
        var collegeId = Guid.NewGuid();
        var requester = NewUser(collegeId, AccountType.Student);
        var reviewer = NewUser(collegeId, AccountType.Teacher);
        db.Users.AddRange(requester, reviewer);

        var request = new WhitelistRequest
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com",
            RequestedBy = requester.Id,
            Status = WhitelistRequestStatus.Pending,
        };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var router = new RecordingNotificationRouter();
        var controller = ControllerAs(db, reviewer, router);

        var result = await controller.ApproveWhitelistRequest(request.Id);

        Assert.IsType<OkObjectResult>(result.Result);
        var routed = Assert.Single(router.Routed);
        Assert.Equal(requester.Id, routed.RecipientId);
        Assert.Equal(NotificationType.WhitelistRequest, routed.Type);
    }

    [Fact]
    public async Task ApproveWhitelistRequest_DoesNotNotify_WhenAlreadyReviewed()
    {
        await using var db = NewDb();
        var collegeId = Guid.NewGuid();
        var requester = NewUser(collegeId, AccountType.Student);
        var reviewer = NewUser(collegeId, AccountType.Teacher);
        db.Users.AddRange(requester, reviewer);

        var request = new WhitelistRequest
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com",
            RequestedBy = requester.Id,
            Status = WhitelistRequestStatus.Approved,
        };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var router = new RecordingNotificationRouter();
        var controller = ControllerAs(db, reviewer, router);

        var result = await controller.ApproveWhitelistRequest(request.Id);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Empty(router.Routed);
    }
}

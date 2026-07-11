using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class CalendarControllerTests
{
    // Grants "create_event" unconditionally — no test in this file needs department-scoped
    // permission checks.
    private class AllowingPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode) => Task.FromResult(true);
        public Task<Guid?> GetDepartmentScopeAsync(Guid userId) => Task.FromResult<Guid?>(null);
    }

    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser(AccountType accountType) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static CalendarController ControllerAs(AppDbContext db, User user) => new(db, new AllowingPermissionService())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth")),
            },
        },
    };

    // #159: CreateEvent had no validation at all that EndTime came after StartTime.
    [Fact]
    public async Task Issue159_CreateEvent_RejectsEndTimeAtOrBeforeStartTime()
    {
        await using var db = NewDb();
        var creator = NewUser(AccountType.AdminTier);
        db.Users.Add(creator);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, creator);
        var start = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = await controller.CreateEvent(new CreateEventRequest("Orientation", start, start, null, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Events.ToListAsync());
    }

    [Fact]
    public async Task Issue159_CreateEvent_RejectsEndTimeBeforeStartTime()
    {
        await using var db = NewDb();
        var creator = NewUser(AccountType.AdminTier);
        db.Users.Add(creator);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, creator);
        var start = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = await controller.CreateEvent(new CreateEventRequest("Orientation", start, start.AddHours(-1), null, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Events.ToListAsync());
    }

    [Fact]
    public async Task Issue159_CreateEvent_AllowsValidTimeRange()
    {
        await using var db = NewDb();
        var creator = NewUser(AccountType.AdminTier);
        db.Users.Add(creator);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, creator);
        var start = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = await controller.CreateEvent(new CreateEventRequest("Orientation", start, start.AddHours(1), null, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<EventDto>(ok.Value);
        Assert.Single(await db.Events.ToListAsync());
    }
}

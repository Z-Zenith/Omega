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

public class FeesControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(AccountType accountType, Guid collegeId) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = collegeId,
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static async Task<Guid> GrantManageFeesAsync(AppDbContext db, Guid userId)
    {
        db.Roles.Add(new Role { Code = "finance" });
        db.Permissions.Add(new Permission { Code = "manage_fees", Description = "x" });
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = userId, RoleCode = "finance", GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var role = await db.Roles.FindAsync("finance");
        var permission = await db.Permissions.FindAsync("manage_fees");
        role!.PermissionCodes.Add(permission!);
        await db.SaveChangesAsync();
        return userId;
    }

    private static FeesController ControllerAs(AppDbContext db, Guid userId)
    {
        var controller = new FeesController(db, new PermissionService(db), new CollegeScopeService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth")),
            },
        };
        return controller;
    }

    [Fact]
    public async Task CreateLink_SucceedsForSameCollegeStudent()
    {
        using var db = NewDb();
        var college = Guid.NewGuid();
        var caller = NewUser(AccountType.AdminTier, college);
        var student = NewUser(AccountType.Student, college);
        db.Users.AddRange(caller, student);
        await GrantManageFeesAsync(db, caller.Id);

        var controller = ControllerAs(db, caller.Id);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // #129 — manage_fees is checked globally; without a CollegeId check, a caller at one
    // college could create a fee link (and payment obligation) against a student at a
    // different college.
    [Fact]
    public async Task CreateLink_ForbidsCrossCollegeStudent()
    {
        using var db = NewDb();
        var caller = NewUser(AccountType.AdminTier, Guid.NewGuid());
        var student = NewUser(AccountType.Student, Guid.NewGuid()); // different college
        db.Users.AddRange(caller, student);
        await GrantManageFeesAsync(db, caller.Id);

        var controller = ControllerAs(db, caller.Id);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 5000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(await db.FeeRecords.ToListAsync());
    }
}

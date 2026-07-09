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

public class RolesControllerTests
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
        AccountType = AccountType.Teacher,
        IsActive = true,
    };

    private static RolesController ControllerAs(AppDbContext db, Guid userId) => new(db, new PermissionService(db), new CollegeScopeService(db))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth")),
            },
        },
    };

    private static async Task SeedRolesAndPermissionsAsync(AppDbContext db)
    {
        var manageRolesPermission = new Permission { Code = "manage_roles_and_permissions", Description = "x" };
        var createTimetablePermission = new Permission { Code = "create_timetable", Description = "x" };
        var admin = new Role { Code = "admin" };
        admin.PermissionCodes.Add(manageRolesPermission);
        var lecturer = new Role { Code = "lecturer" };
        db.Roles.AddRange(admin, lecturer);
        db.Permissions.AddRange(manageRolesPermission, createTimetablePermission);
        await db.SaveChangesAsync();
    }

    // AWA-13
    [Fact]
    public async Task CreateRoleBinding_ForbidsCallerWithoutManagePermission()
    {
        await using var db = NewDb();
        await SeedRolesAndPermissionsAsync(db);
        var caller = NewUser();
        var target = NewUser();
        db.Users.AddRange(caller, target);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, caller.Id);
        var result = await controller.CreateRoleBinding(new CreateRoleBindingRequest(target.Id, "lecturer", ScopeKind.Global, null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-13
    [Fact]
    public async Task CreateRoleBinding_CreatesBindingForAdminCaller()
    {
        await using var db = NewDb();
        await SeedRolesAndPermissionsAsync(db);
        var admin = NewUser();
        var target = NewUser();
        target.CollegeId = admin.CollegeId;
        db.Users.AddRange(admin, target);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.CreateRoleBinding(new CreateRoleBindingRequest(target.Id, "lecturer", ScopeKind.Global, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RoleBindingDto>(ok.Value);
        Assert.Equal(target.Id, dto.UserId);
        Assert.Equal("lecturer", dto.RoleCode);
        Assert.Single(db.RoleBindings.Local, b => b.UserId == target.Id);
    }

    // #127 — cross-college privilege escalation: an admin at one college must not be able
    // to grant a role to a user at a different college.
    [Fact]
    public async Task CreateRoleBinding_ForbidsCrossCollegeTarget()
    {
        await using var db = NewDb();
        await SeedRolesAndPermissionsAsync(db);
        var admin = NewUser();
        var target = NewUser(); // different (random) CollegeId than admin, by construction
        db.Users.AddRange(admin, target);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.CreateRoleBinding(new CreateRoleBindingRequest(target.Id, "lecturer", ScopeKind.Global, null));

        Assert.IsType<ForbidResult>(result.Result);
        Assert.DoesNotContain(db.RoleBindings.Local, b => b.UserId == target.Id);
    }

    // AWA-13
    [Fact]
    public async Task DeletePermissionGrant_RevokedOverrideStopsApplyingImmediately()
    {
        await using var db = NewDb();
        await SeedRolesAndPermissionsAsync(db);
        var admin = NewUser();
        var target = NewUser();
        target.CollegeId = admin.CollegeId;
        db.Users.AddRange(admin, target);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var permissionService = new PermissionService(db);
        var controller = ControllerAs(db, admin.Id);

        var grantResult = await controller.CreatePermissionGrant(new CreatePermissionGrantRequest(target.Id, "create_timetable", true, null));
        var ok = Assert.IsType<OkObjectResult>(grantResult.Result);
        var grantDto = Assert.IsType<PermissionGrantDto>(ok.Value);

        Assert.True(await permissionService.HasPermissionAsync(target.Id, "create_timetable"));

        var deleteResult = await controller.DeletePermissionGrant(grantDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        // Live DB read, no session/cache to invalidate — reflects the AC that a revoke
        // applies immediately without requiring the affected user to re-login.
        Assert.False(await permissionService.HasPermissionAsync(target.Id, "create_timetable"));
    }

    // AWA-13
    [Fact]
    public async Task CreatePermissionGrant_RejectsUnknownPermissionCode()
    {
        await using var db = NewDb();
        await SeedRolesAndPermissionsAsync(db);
        var admin = NewUser();
        var target = NewUser();
        target.CollegeId = admin.CollegeId;
        db.Users.AddRange(admin, target);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.CreatePermissionGrant(new CreatePermissionGrantRequest(target.Id, "not_a_real_permission", true, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}

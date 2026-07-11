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

public class UsersControllerTests
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

    private static UsersController ControllerAs(AppDbContext db, Guid userId) =>
        new(db, new BcryptPasswordHasher(), new TotpService(), new PermissionService(db))
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

    // AWA-10 / #78 — ResetPassword must bind a request DTO ({"newPassword": "..."}), not a
    // bare JSON string, so it matches every other mutating endpoint's calling convention.
    [Fact]
    public async Task ResetPassword_HashesNewPasswordFromRequestBody()
    {
        await using var db = NewDb();
        var admin = NewUser();
        var target = NewUser();
        db.Users.AddRange(admin, target);
        db.Permissions.Add(new Permission { Code = "reset_password", Description = "x" });
        var role = new Role { Code = "admin" };
        role.PermissionCodes.Add(db.Permissions.Local.First());
        db.Roles.Add(role);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.ResetPassword(target.Id, new ResetPasswordRequest("a-new-password"));

        Assert.IsType<NoContentResult>(result);
        var updated = await db.Users.FindAsync(target.Id);
        Assert.NotEqual("hash", updated!.PasswordHash);
    }

    // #132 — an admin-initiated reset must revoke the target's existing sessions, not just
    // update PasswordHash. Otherwise a JWT issued before the reset (e.g. to an attacker on a
    // compromised account the admin is resetting specifically because of that) stays valid for
    // the rest of its ~60-minute lifetime.
    [Fact]
    public async Task ResetPassword_RevokesAllActiveSessions_ForTheTargetUser()
    {
        await using var db = NewDb();
        var admin = NewUser();
        var target = NewUser();
        db.Users.AddRange(admin, target);
        db.Permissions.Add(new Permission { Code = "reset_password", Description = "x" });
        var role = new Role { Code = "admin" };
        role.PermissionCodes.Add(db.Permissions.Local.First());
        db.Roles.Add(role);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });

        var targetSession = new UserSession { Id = Guid.NewGuid(), UserId = target.Id, IsActive = true };
        var adminSession = new UserSession { Id = Guid.NewGuid(), UserId = admin.Id, IsActive = true };
        db.UserSessions.AddRange(targetSession, adminSession);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.ResetPassword(target.Id, new ResetPasswordRequest("a-new-password"));

        Assert.IsType<NoContentResult>(result);
        Assert.False((await db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == targetSession.Id)).IsActive);
        // The admin's own session (a different user) must be untouched.
        Assert.True((await db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == adminSession.Id)).IsActive);
    }
}

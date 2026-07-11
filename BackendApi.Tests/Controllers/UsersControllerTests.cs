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
        var result = await controller.ResetPassword(target.Id, new ResetPasswordRequest("a-new-password1"));

        Assert.IsType<NoContentResult>(result);
        var updated = await db.Users.FindAsync(target.Id);
        Assert.NotEqual("hash", updated!.PasswordHash);
    }

    // #140 — no server-side strength check existed before; a 1-character reset password
    // used to be accepted and hashed as-is.
    [Fact]
    public async Task ResetPassword_RejectsWeakPassword()
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
        var result = await controller.ResetPassword(target.Id, new ResetPasswordRequest("a"));

        Assert.IsType<BadRequestObjectResult>(result);
        var unchanged = await db.Users.FindAsync(target.Id);
        Assert.Equal("hash", unchanged!.PasswordHash);
    }

    // #140 — same policy on account creation: InitialPassword must meet the minimum bar too.
    [Fact]
    public async Task Create_RejectsWeakInitialPassword()
    {
        await using var db = NewDb();
        var admin = NewUser();
        db.Users.Add(admin);
        db.Permissions.Add(new Permission { Code = "manage_accounts", Description = "x" });
        var role = new Role { Code = "admin" };
        role.PermissionCodes.Add(db.Permissions.Local.First());
        db.Roles.Add(role);
        db.RoleBindings.Add(new RoleBinding { Id = Guid.NewGuid(), UserId = admin.Id, RoleCode = "admin", ScopeType = ScopeKind.Global, GrantedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin.Id);
        var result = await controller.Create(new CreateUserRequest(admin.CollegeId, AccountType.Student, "new-student", "weak", "New Student", null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(await db.Users.AnyAsync(u => u.Identifier == "new-student"));
    }
}

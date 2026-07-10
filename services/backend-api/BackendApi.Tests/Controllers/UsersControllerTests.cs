using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.DataProtection;
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

    private static UsersController ControllerAs(AppDbContext db, Guid userId, ITotpService? totpService = null) =>
        new(db, new BcryptPasswordHasher(), totpService ?? new TotpService(new EphemeralDataProtectionProvider()), new PermissionService(db))
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

    // #131 — Create must never persist the raw Base32 TOTP secret. What lands in
    // User.TotpSecret has to be the encrypted (Protect()'d) form, distinct from both the raw
    // secret and from the one-time value returned to the caller for provisioning.
    [Fact]
    public async Task Create_PersistsEncryptedTotpSecret_NotRawPlaintext()
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

        var totpService = new TotpService(new EphemeralDataProtectionProvider());
        var controller = ControllerAs(db, admin.Id, totpService);
        var request = new CreateUserRequest(admin.CollegeId, AccountType.Student, "student-1", "initial-pass", "New Student", null);

        var result = await controller.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<CreateUserResponse>(created.Value);

        var stored = await db.Users.FindAsync(response.UserId);
        Assert.NotNull(stored!.TotpSecret);

        // The DB-facing value must not be (or contain) the raw secret handed back in the
        // creation response — that would mean encryption was skipped entirely.
        Assert.NotEqual(response.TotpSecret, stored.TotpSecret);
        Assert.DoesNotContain(response.TotpSecret, stored.TotpSecret);

        // Round-trip: the stored ciphertext must still decrypt+verify against a code
        // generated from the raw secret that was returned once at creation time.
        var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(response.TotpSecret));
        var code = totp.ComputeTotp();
        Assert.True(totpService.ValidateCode(stored.TotpSecret, code));
    }
}

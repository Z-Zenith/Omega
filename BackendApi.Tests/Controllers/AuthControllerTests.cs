using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace BackendApi.Tests.Controllers;

public class AuthControllerTests
{
    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null) => "fake-token";
    }

    private static AuthController ControllerAs(AppDbContext db, Guid userId, IPasswordHasher hasher, ITotpService totp) =>
        new(db, hasher, totp, new FakeJwtTokenService())
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

    // #140 — no server-side strength check existed before; a 1-character new password used
    // to be accepted by self-service change-password once current password + TOTP checked out.
    [Fact]
    public async Task ChangePassword_RejectsWeakNewPassword()
    {
        await using var db = NewDb();
        var hasher = new BcryptPasswordHasher();
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = "student-1",
            PasswordHash = hasher.Hash("current-password1"),
            FullName = "Student",
            IsActive = true,
            AccountType = AccountType.Student,
            TotpSecret = secret,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var controller = ControllerAs(db, user.Id, hasher, totpService);

        var result = await controller.ChangePassword(new ChangePasswordRequest("current-password1", "a", code));

        Assert.IsType<BadRequestObjectResult>(result);
        var unchanged = await db.Users.FindAsync(user.Id);
        Assert.True(hasher.Verify("current-password1", unchanged!.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_Succeeds_WithStrongNewPassword()
    {
        await using var db = NewDb();
        var hasher = new BcryptPasswordHasher();
        var totpService = new TotpService();
        var secret = totpService.GenerateSecret();
        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = "student-1",
            PasswordHash = hasher.Hash("current-password1"),
            FullName = "Student",
            IsActive = true,
            AccountType = AccountType.Student,
            TotpSecret = secret,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var controller = ControllerAs(db, user.Id, hasher, totpService);

        var result = await controller.ChangePassword(new ChangePasswordRequest("current-password1", "Str0ngPass!", code));

        Assert.IsType<NoContentResult>(result);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.True(hasher.Verify("Str0ngPass!", updated!.PasswordHash));
    }
}

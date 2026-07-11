using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace BackendApi.Tests.Controllers;

// #132 — before this, ChangePassword only ever updated User.PasswordHash; it never touched
// UserSessions, so a JWT/session issued before the change (e.g. to an attacker who'd phished
// the old credentials+TOTP) stayed valid for the rest of its ~60-minute lifetime even after
// the victim "fixed" it by changing their password.
public class AuthControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static string CurrentTotpCode(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    private static AuthController ControllerAs(AppDbContext db, Guid userId, ITotpService totpService) =>
        new(db, new BcryptPasswordHasher(), totpService, new ThrowingJwtTokenService())
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

    [Fact]
    public async Task ChangePassword_RevokesAllActiveSessions_ForTheUser()
    {
        await using var db = NewDb();
        var hasher = new BcryptPasswordHasher();
        // Same TotpService instance (and thus the same ephemeral key) used to both Protect()
        // the stored secret and, via the controller, ValidateCode() it back - two separate
        // instances would each get their own ephemeral key and could never decrypt each other's
        // ciphertext.
        var totpService = new TotpService(new EphemeralDataProtectionProvider());
        var totpSecret = totpService.GenerateSecret();
        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = $"user-{Guid.NewGuid():N}",
            PasswordHash = hasher.Hash("old-password"),
            TotpSecret = totpService.Protect(totpSecret),
            FullName = "Test User",
            AccountType = AccountType.Teacher,
            IsActive = true,
        };
        db.Users.Add(user);

        var sessionA = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true };
        var sessionB = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true };
        var otherUserSession = new UserSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), IsActive = true };
        db.UserSessions.AddRange(sessionA, sessionB, otherUserSession);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, user.Id, totpService);
        var result = await controller.ChangePassword(new ChangePasswordRequest("old-password", "new-password", CurrentTotpCode(totpSecret)));

        Assert.IsType<NoContentResult>(result);
        var allSessions = await db.UserSessions.AsNoTracking().ToListAsync();
        Assert.Equal(3, allSessions.Count);
        Assert.False(allSessions.Single(s => s.Id == sessionA.Id).IsActive);
        Assert.False(allSessions.Single(s => s.Id == sessionB.Id).IsActive);
        // A different user's session must be untouched.
        Assert.True(allSessions.Single(s => s.Id == otherUserSession.Id).IsActive);
    }

    [Fact]
    public async Task ChangePassword_DoesNotRevokeSessions_WhenCurrentPasswordIsWrong()
    {
        await using var db = NewDb();
        var hasher = new BcryptPasswordHasher();
        var totpService = new TotpService(new EphemeralDataProtectionProvider());
        var totpSecret = totpService.GenerateSecret();
        var user = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = $"user-{Guid.NewGuid():N}",
            PasswordHash = hasher.Hash("old-password"),
            TotpSecret = totpService.Protect(totpSecret),
            FullName = "Test User",
            AccountType = AccountType.Teacher,
            IsActive = true,
        };
        db.Users.Add(user);
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, user.Id, totpService);
        var result = await controller.ChangePassword(new ChangePasswordRequest("wrong-password", "new-password", CurrentTotpCode(totpSecret)));

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.True((await db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).IsActive);
    }

    private class ThrowingJwtTokenService : IJwtTokenService
    {
        public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null) =>
            throw new NotSupportedException("Not needed by ChangePassword.");
    }
}

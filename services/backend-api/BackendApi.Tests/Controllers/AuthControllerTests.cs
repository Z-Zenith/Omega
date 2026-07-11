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
using OtpNet;

namespace BackendApi.Tests.Controllers;

// #132 — before this, ChangePassword only ever updated User.PasswordHash; it never touched
// UserSessions, so a JWT/session issued before the change (e.g. to an attacker who'd phished
// the old credentials+TOTP) stayed valid for the rest of its ~60-minute lifetime even after
// the victim "fixed" it by changing their password.
public class AuthControllerTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser(string identifier) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = Guid.NewGuid(),
        Identifier = identifier,
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = AccountType.Student,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static string CurrentTotpCode(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    private class NotCalledPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => throw new InvalidOperationException("should not be called");
        public bool Verify(string password, string hash) => throw new InvalidOperationException("should not be called");
    }

    private class NotCalledTotpService : ITotpService
    {
        public string GenerateSecret() => throw new InvalidOperationException("should not be called");
        public string Protect(string base32Secret) => throw new InvalidOperationException("should not be called");
        public bool ValidateCode(string protectedSecret, string code) => throw new InvalidOperationException("should not be called");
        public string BuildProvisioningUri(string base32Secret, string accountIdentifier, string issuer) =>
            throw new InvalidOperationException("should not be called");
    }

    private class NotCalledJwtTokenService : IJwtTokenService
    {
        public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null) =>
            throw new InvalidOperationException("should not be called");
    }

    private class ThrowingJwtTokenService : IJwtTokenService
    {
        public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null) =>
            throw new NotSupportedException("Not needed by ChangePassword.");
    }

    private static AuthController NewController(AppDbContext db) =>
        new(db, new NotCalledPasswordHasher(), new NotCalledTotpService(), new NotCalledJwtTokenService());

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

    // #151 — a roll number/username is only unique per (college_id, identifier); the same
    // identifier can legitimately exist at two different colleges. Login must fail closed on
    // that ambiguity rather than deterministically authenticating against one of the matches,
    // which would silently check the password against the wrong college's account and
    // permanently lock that user out with a misleading "invalid password" error.
    [Fact]
    public async Task Login_FailsClosed_WhenIdentifierMatchesMultipleColleges()
    {
        await using var db = NewDb();
        var collegeAStudent = NewUser("101");
        var collegeBStudent = NewUser("101");
        db.Users.AddRange(collegeAStudent, collegeBStudent);
        await db.SaveChangesAsync();

        var controller = NewController(db);
        var result = await controller.Login(new LoginRequest("101", "irrelevant", "000000", null));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var value = Assert.IsAssignableFrom<object>(unauthorized.Value);
        var errorProp = value.GetType().GetProperty("error")!.GetValue(value);
        Assert.Equal("identifier_ambiguous", errorProp);
    }

    [Fact]
    public async Task Login_ReturnsUnknownIdentifier_WhenNoUserMatches()
    {
        await using var db = NewDb();

        var controller = NewController(db);
        var result = await controller.Login(new LoginRequest("nonexistent", "irrelevant", "000000", null));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var value = Assert.IsAssignableFrom<object>(unauthorized.Value);
        var errorProp = value.GetType().GetProperty("error")!.GetValue(value);
        Assert.Equal("unknown_identifier", errorProp);
    }

    // #140 — no server-side strength check existed before; a 1-character new password used
    // to be accepted by self-service change-password once current password + TOTP checked out.
    [Fact]
    public async Task ChangePassword_RejectsWeakNewPassword()
    {
        await using var db = NewDb();
        var hasher = new BcryptPasswordHasher();
        var totpService = new TotpService(new EphemeralDataProtectionProvider());
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
            TotpSecret = totpService.Protect(secret),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var controller = ControllerAs(db, user.Id, totpService);

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
        var totpService = new TotpService(new EphemeralDataProtectionProvider());
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
            TotpSecret = totpService.Protect(secret),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
        var controller = ControllerAs(db, user.Id, totpService);

        var result = await controller.ChangePassword(new ChangePasswordRequest("current-password1", "Str0ngPass!", code));

        Assert.IsType<NoContentResult>(result);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.True(hasher.Verify("Str0ngPass!", updated!.PasswordHash));
    }

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
}

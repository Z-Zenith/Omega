using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

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

    private static AuthController NewController(AppDbContext db) =>
        new(db, new NotCalledPasswordHasher(), new NotCalledTotpService(), new NotCalledJwtTokenService());

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
}

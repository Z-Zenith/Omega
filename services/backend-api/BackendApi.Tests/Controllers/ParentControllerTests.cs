using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class ParentControllerTests
{
    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null) => "fake-token";
    }

    private static async Task<(Guid studentId, DateOnly dob)> SeedStudentWithParentAsync(AppDbContext db, string rollNumber)
    {
        var collegeId = Guid.NewGuid();
        var dob = new DateOnly(2005, 6, 15);
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = studentId,
            CollegeId = collegeId,
            Identifier = rollNumber,
            PasswordHash = "hash",
            FullName = "Student One",
            IsActive = true,
            AccountType = AccountType.Student,
            DateOfBirth = dob,
        });
        db.Users.Add(new User
        {
            Id = parentId,
            CollegeId = collegeId,
            Identifier = $"parent-{rollNumber}",
            PasswordHash = "hash",
            FullName = "Parent One",
            IsActive = true,
            AccountType = AccountType.Parent,
        });
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = parentId, StudentId = studentId });

        await db.SaveChangesAsync();
        return (studentId, dob);
    }

    // #134: five wrong-DOB attempts against the same roll number must lock it out, even
    // though rate limiting itself (RateLimiterPolicies.Auth) isn't exercised in a unit test —
    // this is the second, IP-independent layer.
    [Fact]
    public async Task Login_LocksOutRollNumber_AfterFiveFailedAttempts()
    {
        using var db = NewDb();
        var (_, dob) = await SeedStudentWithParentAsync(db, "ROLL-100");
        var lockout = new ParentLoginLockoutService();
        var controller = new ParentController(db, new FakeJwtTokenService(), lockout);
        var wrongDob = dob.AddYears(1);

        for (var i = 0; i < 5; i++)
        {
            var attempt = await controller.Login(new ParentLoginRequest("ROLL-100", wrongDob, null));
            Assert.IsType<UnauthorizedObjectResult>(attempt.Result);
        }

        // A 6th attempt with the CORRECT DOB must still be locked out — the point of the
        // lockout is to stop further guessing regardless of whether this particular guess
        // would have succeeded.
        var lockedAttempt = await controller.Login(new ParentLoginRequest("ROLL-100", dob, null));

        var objectResult = Assert.IsType<ObjectResult>(lockedAttempt.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task Login_Succeeds_WithCorrectCredentialsBeforeLockoutThreshold()
    {
        using var db = NewDb();
        var (_, dob) = await SeedStudentWithParentAsync(db, "ROLL-200");
        var lockout = new ParentLoginLockoutService();
        var controller = new ParentController(db, new FakeJwtTokenService(), lockout);

        var result = await controller.Login(new ParentLoginRequest("ROLL-200", dob, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<ParentLoginResponse>(ok.Value);
    }
}

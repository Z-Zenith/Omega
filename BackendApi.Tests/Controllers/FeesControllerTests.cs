using System.Security.Claims;
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
    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private class FakePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode) => Task.FromResult(false);
        public Task<Guid?> GetDepartmentScopeAsync(Guid userId) => Task.FromResult<Guid?>(null);
    }

    private static ClaimsPrincipal ParentPrincipal(Guid userId, Guid sessionId, Guid wardId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("account_type", nameof(AccountType.Parent)),
            new Claim("ward_id", wardId.ToString()),
            new Claim("session_id", sessionId.ToString()),
        ], "TestAuth"));

    private static FeesController ControllerAs(AppDbContext db, ClaimsPrincipal principal) =>
        new(db, new FakePermissionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    private static async Task<(Guid parentId, Guid studentId, Guid sessionId, Guid feeId)> SeedAsync(AppDbContext db)
    {
        var collegeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var feeId = Guid.NewGuid();

        db.Users.Add(new User { Id = parentId, CollegeId = collegeId, Identifier = "parent-1", PasswordHash = "hash", FullName = "Parent", IsActive = true, AccountType = AccountType.Parent });
        db.Users.Add(new User { Id = studentId, CollegeId = collegeId, Identifier = "student-1", PasswordHash = "hash", FullName = "Student", IsActive = true, AccountType = AccountType.Student });
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = parentId, StudentId = studentId });
        db.UserSessions.Add(new UserSession { Id = sessionId, UserId = parentId, IsActive = true });
        db.FeeRecords.Add(new FeeRecord { Id = feeId, StudentId = studentId, Amount = 100, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Status = FeeStatus.Pending });

        await db.SaveChangesAsync();
        return (parentId, studentId, sessionId, feeId);
    }

    // #93: FeesController.Pay must return NotFound (not Forbid) when the fee simply doesn't
    // exist — this is the "existence" half of the standardized convention.
    [Fact]
    public async Task Pay_ReturnsNotFound_WhenFeeDoesNotExist()
    {
        using var db = NewDb();
        var (parentId, studentId, sessionId, _) = await SeedAsync(db);
        var controller = ControllerAs(db, ParentPrincipal(parentId, sessionId, studentId));

        var result = await controller.Pay(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // #93: and also NotFound (not Forbid) when the fee exists but belongs to a different
    // ward than the caller's — the two cases must be indistinguishable to the caller.
    [Fact]
    public async Task Pay_ReturnsNotFound_WhenFeeBelongsToADifferentWard()
    {
        using var db = NewDb();
        var (parentId, studentId, sessionId, feeId) = await SeedAsync(db);
        var otherWardId = Guid.NewGuid();
        var controller = ControllerAs(db, ParentPrincipal(parentId, sessionId, otherWardId));

        var result = await controller.Pay(feeId);

        Assert.IsType<NotFoundResult>(result.Result);
        var fee = await db.FeeRecords.FindAsync(feeId);
        Assert.Equal(FeeStatus.Pending, fee!.Status);
    }

    // Pay()'s success path uses ExecuteUpdateAsync, which the EF Core in-memory provider
    // doesn't support (pre-existing limitation, unrelated to #93) — so only the two
    // NotFound-before-that-point cases above are exercised here; Pay()'s happy path isn't
    // unit-testable against this provider.
}

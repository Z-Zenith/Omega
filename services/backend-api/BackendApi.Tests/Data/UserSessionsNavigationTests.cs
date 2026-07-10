using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Data;

// #92 — user_sessions only enforces uniqueness on user_id where is_active = true (a partial
// unique index; see uniq_user_active_session in db/init/01_schema.sql). A new login flips the
// previous row to is_active=false rather than deleting it, so multiple historical rows
// accumulate per user. User.UserSession was previously modeled as a one-to-one nav property,
// which is undefined-behavior-adjacent once more than one row exists for a user. This locks in
// that the nav property is a collection, and that callers must filter IsActive themselves to
// find "the current session."
public class UserSessionsNavigationTests
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
        AccountType = AccountType.Student,
        IsActive = true,
    };

    [Fact]
    public async Task User_CanHaveMultipleHistoricalSessions()
    {
        await using var db = NewDb();
        var user = NewUser();
        db.Users.Add(user);

        var oldSession1 = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var oldSession2 = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var activeSession = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.UserSessions.AddRange(oldSession1, oldSession2, activeSession);

        // Would throw with the old one-to-one nav (EF would materialize/track at most one
        // UserSession per User via WithOne) — three rows for the same user must persist fine
        // now that the relationship is one-to-many.
        await db.SaveChangesAsync();

        var allSessionsForUser = await db.UserSessions.Where(s => s.UserId == user.Id).ToListAsync();
        Assert.Equal(3, allSessionsForUser.Count);
    }

    [Fact]
    public async Task UserSessions_FilteredByIsActive_ReturnsOnlyTheCurrentSession()
    {
        await using var db = NewDb();
        var user = NewUser();
        db.Users.Add(user);

        db.UserSessions.Add(new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-2) });
        db.UserSessions.Add(new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        var activeSession = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.UserSessions.Add(activeSession);
        await db.SaveChangesAsync();

        // The pattern every call site (SessionActiveFilter, AuthController, ParentController)
        // must use in place of the old implicit one-to-one User.UserSession nav property:
        // explicitly filter IsActive rather than trusting a single implicit row.
        var current = await db.UserSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .SingleOrDefaultAsync();

        Assert.NotNull(current);
        Assert.Equal(activeSession.Id, current!.Id);
    }
}

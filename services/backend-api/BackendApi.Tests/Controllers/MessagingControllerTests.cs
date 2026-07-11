using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class MessagingControllerTests
{
    private static AppDbContext NewDb(string dbName) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static User NewUser(AccountType accountType, Guid? collegeId = null) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = collegeId ?? Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static MessagingController ControllerAs(AppDbContext db, User user) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth")),
            },
        },
    };

    // #159: simulates the exact race CreateThread must survive — two concurrent requests
    // for the same (student, teacher) pair both pass the "does a thread already exist"
    // check before either commits, then one wins the message_threads unique-index race. EF
    // Core's in-memory provider doesn't enforce unique indexes, so the "other request" is
    // simulated by overriding SaveChangesAsync to insert the winning row via a second
    // context sharing the same in-memory database, then throwing DbUpdateException the way
    // a real unique-constraint violation would. Mirrors #94's RaceSimulatingDbContext
    // pattern in CalendarControllerTests.
    private sealed class RaceSimulatingDbContext(DbContextOptions<AppDbContext> options, string dbName) : AppDbContext(options)
    {
        private bool _injected;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var added = ChangeTracker.Entries<MessageThread>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;

            if (!_injected && added is not null)
            {
                _injected = true;

                await using var winnerDb = NewDb(dbName);
                winnerDb.MessageThreads.Add(new MessageThread
                {
                    Id = Guid.NewGuid(),
                    StudentId = added.StudentId,
                    TeacherId = added.TeacherId,
                    CreatedAt = DateTime.UtcNow,
                });
                await winnerDb.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException("Simulated unique-constraint race on (student_id, teacher_id).");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Issue159_CreateThread_RecoversFromConcurrentDuplicateInsert()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedDb = NewDb(dbName);
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        seedDb.Users.AddRange(student, teacher);
        await seedDb.SaveChangesAsync();

        await using var raceDb = new RaceSimulatingDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, dbName);
        var controller = ControllerAs(raceDb, student);

        var result = await controller.CreateThread(new CreateThreadRequest(student.Id, teacher.Id));

        // Not a 500 — the concurrent-insert race is recovered from with the thread the
        // other request actually persisted, same as BrowsingController.ApproveWhitelistRequest.
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MessageThreadResponse>(ok.Value);
        Assert.Equal(student.Id, dto.StudentId);
        Assert.Equal(teacher.Id, dto.TeacherId);

        await using var verifyDb = NewDb(dbName);
        var threads = await verifyDb.MessageThreads
            .Where(t => t.StudentId == student.Id && t.TeacherId == teacher.Id)
            .ToListAsync();
        // Exactly one thread survives — the winner's row — not a second row from this
        // request's own failed insert.
        Assert.Single(threads);
    }

    [Fact]
    public async Task Issue159_CreateThread_ReturnsExistingThread_WhenAlreadyCreated()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDb(dbName);
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, teacher);
        db.MessageThreads.Add(new MessageThread { Id = Guid.NewGuid(), StudentId = student.Id, TeacherId = teacher.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.CreateThread(new CreateThreadRequest(student.Id, teacher.Id));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(await db.MessageThreads.ToListAsync());
    }
}

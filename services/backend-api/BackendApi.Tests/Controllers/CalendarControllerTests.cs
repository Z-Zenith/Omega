using System.Security.Claims;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class CalendarControllerTests
{
    private static AppDbContext NewDb(string dbName) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private class FakePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode) => Task.FromResult(false);
        public Task<Guid?> GetDepartmentScopeAsync(Guid userId) => Task.FromResult<Guid?>(null);
    }

    // #94: simulates the exact race CalendarController.RegisterForEvent must survive — two
    // concurrent requests for the same (event, student) both pass the "does a registration
    // already exist" check before either commits, then one of them wins the unique-index
    // race. EF Core's in-memory provider doesn't enforce unique indexes (verified: a plain
    // duplicate insert does not throw), so the "other request" is simulated by overriding
    // SaveChangesAsync to insert the winning row via a second context sharing the same
    // in-memory database, then throwing DbUpdateException the way a real unique-constraint
    // violation would.
    private sealed class RaceSimulatingDbContext(DbContextOptions<AppDbContext> options, string dbName) : AppDbContext(options)
    {
        private bool _injected;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var added = ChangeTracker.Entries<EventRegistration>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;

            if (!_injected && added is not null)
            {
                _injected = true;

                await using var winnerDb = NewDb(dbName);
                winnerDb.EventRegistrations.Add(new EventRegistration
                {
                    Id = Guid.NewGuid(),
                    EventId = added.EventId,
                    StudentId = added.StudentId,
                    RegisteredAt = DateTime.UtcNow,
                });
                await winnerDb.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException("Simulated unique-constraint race on (event_id, student_id).");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record Fixture(Guid StudentId, Guid EventId);

    private static async Task<Fixture> SeedAsync(AppDbContext db)
    {
        var collegeId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        db.Departments.Add(new Department { Id = departmentId, CollegeId = collegeId, Name = "CS" });
        db.Sections.Add(new Section { Id = sectionId, DepartmentId = departmentId, Year = 1, Name = "A" });
        db.Users.Add(new User
        {
            Id = studentId,
            CollegeId = collegeId,
            Identifier = "student-1",
            PasswordHash = "hash",
            FullName = "Student One",
            IsActive = true,
            AccountType = AccountType.Student,
        });
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = sectionId, StudentId = studentId });
        db.Events.Add(new Event
        {
            Id = eventId,
            CollegeId = collegeId,
            Title = "Fest",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            CreatedBy = studentId,
        });

        await db.SaveChangesAsync();
        return new Fixture(studentId, eventId);
    }

    private static CalendarController ControllerAs(AppDbContext db, Guid studentId) =>
        new(db, new FakePermissionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, studentId.ToString())], "TestAuth")),
                },
            },
        };

    [Fact]
    public async Task RegisterForEvent_RecoversFromConcurrentDuplicateInsert()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedDb = NewDb(dbName);
        var fixture = await SeedAsync(seedDb);

        await using var raceDb = new RaceSimulatingDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, dbName);
        var controller = ControllerAs(raceDb, fixture.StudentId);

        var result = await controller.RegisterForEvent(fixture.EventId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);

        await using var verifyDb = NewDb(dbName);
        var registrations = await verifyDb.EventRegistrations
            .Where(r => r.EventId == fixture.EventId && r.StudentId == fixture.StudentId)
            .ToListAsync();
        // Exactly one registration survives — the "winner's" row — not a second row from
        // this request's own (failed) insert.
        Assert.Single(registrations);
    }

    [Fact]
    public async Task RegisterForEvent_ReturnsExistingRegistration_WhenAlreadyRegistered()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDb(dbName);
        var fixture = await SeedAsync(db);
        db.EventRegistrations.Add(new EventRegistration { Id = Guid.NewGuid(), EventId = fixture.EventId, StudentId = fixture.StudentId });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, fixture.StudentId);
        var result = await controller.RegisterForEvent(fixture.EventId);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(await db.EventRegistrations.ToListAsync());
    }
}

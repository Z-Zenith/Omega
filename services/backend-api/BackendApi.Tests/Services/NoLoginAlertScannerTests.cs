using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Services;

// SDA-13
public class NoLoginAlertScannerTests
{
    private class FakeNotificationRouter : INotificationRouter
    {
        public List<(Guid RecipientId, NotificationType Type, object Payload)> Sent { get; } = [];

        public Task<Notification> RouteAsync(Guid recipientId, NotificationType type, object payload, CancellationToken cancellationToken = default)
        {
            Sent.Add((recipientId, type, payload));
            return Task.FromResult(new Notification
            {
                Id = Guid.NewGuid(),
                RecipientId = recipientId,
                Type = type,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private record Fixture(Guid TeacherId, Guid StudentId, Guid ClassSessionId, DateTime SessionStart);

    private static async Task<Fixture> SeedPresentButNotLoggedInAsync(AppDbContext db, DateTime sessionStart)
    {
        var collegeId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var teacher = new User { Id = Guid.NewGuid(), CollegeId = collegeId, Identifier = "t1", PasswordHash = "hash", FullName = "Teacher", IsActive = true, AccountType = AccountType.Teacher };
        var student = new User { Id = Guid.NewGuid(), CollegeId = collegeId, Identifier = "s1", PasswordHash = "hash", FullName = "Student", IsActive = true, AccountType = AccountType.Student };
        var section = new Section { Id = Guid.NewGuid(), DepartmentId = departmentId, Year = 1, Name = "Sec A" };
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = departmentId, Code = "C1", Name = "Course" };
        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = (int)sessionStart.DayOfWeek,
            StartTime = TimeOnly.FromDateTime(sessionStart),
            EndTime = TimeOnly.FromDateTime(sessionStart.AddHours(1)),
        };
        var session = new ClassSession { Id = Guid.NewGuid(), TimetableSlotId = slot.Id, SessionDate = DateOnly.FromDateTime(sessionStart) };
        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            ClassSessionId = session.Id,
            StudentId = student.Id,
            Status = AttendanceStatus.Present,
            MarkedAt = sessionStart,
            MarkedBy = teacher.Id,
        };

        db.Users.AddRange(teacher, student);
        db.Sections.Add(section);
        db.Subjects.Add(subject);
        db.TimetableSlots.Add(slot);
        db.ClassSessions.Add(session);
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        return new Fixture(teacher.Id, student.Id, session.Id, sessionStart);
    }

    [Fact]
    public async Task Alerts_WhenPresentButNotLoggedInAfter20Minutes()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        var fixture = await SeedPresentButNotLoggedInAsync(db, sessionStart);
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(21));

        var sent = Assert.Single(router.Sent);
        Assert.Equal(fixture.TeacherId, sent.RecipientId);
        Assert.Equal(NotificationType.AbsencePing, sent.Type);
    }

    [Fact]
    public async Task DoesNotAlert_BeforeThe20MinuteWindowElapses()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        await SeedPresentButNotLoggedInAsync(db, sessionStart);
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(10));

        Assert.Empty(router.Sent);
    }

    [Fact]
    public async Task DoesNotAlert_WhenStudentLoggedInAfterSessionStart()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        var fixture = await SeedPresentButNotLoggedInAsync(db, sessionStart);
        db.UserSessions.Add(new UserSession { Id = Guid.NewGuid(), UserId = fixture.StudentId, IsActive = true, CreatedAt = sessionStart.AddMinutes(5) });
        await db.SaveChangesAsync();
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(21));

        Assert.Empty(router.Sent);
    }

    // #159: a student who logged in the evening before and never logged out (session still
    // IsActive) must not be treated as "never logged in" just because that session's
    // CreatedAt predates the class start.
    [Fact]
    public async Task DoesNotAlert_WhenPriorSessionStillActiveFromBeforeClassStart()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        var fixture = await SeedPresentButNotLoggedInAsync(db, sessionStart);
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = fixture.StudentId,
            IsActive = true,
            CreatedAt = sessionStart.AddHours(-12),
        });
        await db.SaveChangesAsync();
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(21));

        Assert.Empty(router.Sent);
    }

    // #159: the converse — a session from before class start that was already logged out
    // (IsActive == false) is not a current login and must not suppress the alert.
    [Fact]
    public async Task StillAlerts_WhenPriorSessionEndedBeforeClassStart()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        var fixture = await SeedPresentButNotLoggedInAsync(db, sessionStart);
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = fixture.StudentId,
            IsActive = false,
            CreatedAt = sessionStart.AddHours(-12),
        });
        await db.SaveChangesAsync();
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(21));

        var sent = Assert.Single(router.Sent);
        Assert.Equal(fixture.TeacherId, sent.RecipientId);
    }

    [Fact]
    public async Task FiresOnlyOncePerSession_AcrossRepeatedScans()
    {
        await using var db = NewDb();
        var sessionStart = new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
        await SeedPresentButNotLoggedInAsync(db, sessionStart);
        var router = new FakeNotificationRouter();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(21));
        // Persist the sent notification into the DB, as the real NotificationRouter would,
        // so the second scan's de-duplication check has something to find.
        foreach (var (recipientId, type, payload) in router.Sent)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                RecipientId = recipientId,
                Type = type,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        await NoLoginAlertScanner.ScanAsync(db, router, sessionStart.AddMinutes(22));

        Assert.Single(router.Sent);
    }
}

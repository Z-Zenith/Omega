using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Services;

// AWA-05 + Notification Router (shared, #80) — "When a fee due date approaches, the Backend
// API shall notify the parent to pay", with the AC "reminder fires at a configurable number of
// days before the due date."
public class FeeReminderScannerTests
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

    private record Fixture(Guid ParentId, Guid StudentId, Guid FeeRecordId);

    private static async Task<Fixture> SeedPendingFeeAsync(AppDbContext db, DateOnly dueDate, FeeStatus status = FeeStatus.Pending)
    {
        var collegeId = Guid.NewGuid();
        var student = new User { Id = Guid.NewGuid(), CollegeId = collegeId, Identifier = "s1", PasswordHash = "hash", FullName = "Student", IsActive = true, AccountType = AccountType.Student };
        var parent = new User { Id = Guid.NewGuid(), CollegeId = collegeId, Identifier = "p1", PasswordHash = "hash", FullName = "Parent", IsActive = true, AccountType = AccountType.Parent };
        var feeRecord = new FeeRecord { Id = Guid.NewGuid(), StudentId = student.Id, Amount = 5000m, DueDate = dueDate, Status = status };
        var parentWard = new ParentWard { Id = Guid.NewGuid(), ParentUserId = parent.Id, StudentId = student.Id, CreatedAt = DateTime.UtcNow };

        db.Users.AddRange(student, parent);
        db.FeeRecords.Add(feeRecord);
        db.ParentWards.Add(parentWard);
        await db.SaveChangesAsync();

        return new Fixture(parent.Id, student.Id, feeRecord.Id);
    }

    [Fact]
    public async Task Reminds_WhenDueDateIsExactlyDaysBeforeDueConfigured()
    {
        await using var db = NewDb();
        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var dueDate = DateOnly.FromDateTime(nowUtc).AddDays(3);
        var fixture = await SeedPendingFeeAsync(db, dueDate);
        var router = new FakeNotificationRouter();

        await FeeReminderScanner.ScanAsync(db, router, nowUtc, daysBeforeDue: 3);

        var sent = Assert.Single(router.Sent);
        Assert.Equal(fixture.ParentId, sent.RecipientId);
        Assert.Equal(NotificationType.FeeReminder, sent.Type);
    }

    [Fact]
    public async Task DoesNotRemind_WhenDueDateIsNotYetInTheConfiguredWindow()
    {
        await using var db = NewDb();
        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var dueDate = DateOnly.FromDateTime(nowUtc).AddDays(10);
        await SeedPendingFeeAsync(db, dueDate);
        var router = new FakeNotificationRouter();

        await FeeReminderScanner.ScanAsync(db, router, nowUtc, daysBeforeDue: 3);

        Assert.Empty(router.Sent);
    }

    [Fact]
    public async Task DoesNotRemind_ForAlreadyPaidFees()
    {
        await using var db = NewDb();
        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var dueDate = DateOnly.FromDateTime(nowUtc).AddDays(3);
        await SeedPendingFeeAsync(db, dueDate, status: FeeStatus.Paid);
        var router = new FakeNotificationRouter();

        await FeeReminderScanner.ScanAsync(db, router, nowUtc, daysBeforeDue: 3);

        Assert.Empty(router.Sent);
    }

    [Fact]
    public async Task FiresOnlyOncePerFeeRecord_AcrossRepeatedScans()
    {
        await using var db = NewDb();
        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var dueDate = DateOnly.FromDateTime(nowUtc).AddDays(3);
        await SeedPendingFeeAsync(db, dueDate);
        var router = new FakeNotificationRouter();

        await FeeReminderScanner.ScanAsync(db, router, nowUtc, daysBeforeDue: 3);
        // Persist the sent notification into the DB, as the real NotificationRouter would, so
        // the second scan's de-duplication check has something to find.
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

        await FeeReminderScanner.ScanAsync(db, router, nowUtc.AddHours(1), daysBeforeDue: 3);

        Assert.Single(router.Sent);
    }

    [Fact]
    public async Task Reminds_EveryParentLinkedToTheStudent()
    {
        await using var db = NewDb();
        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var dueDate = DateOnly.FromDateTime(nowUtc).AddDays(3);
        var fixture = await SeedPendingFeeAsync(db, dueDate);
        var secondParent = new User { Id = Guid.NewGuid(), CollegeId = Guid.NewGuid(), Identifier = "p2", PasswordHash = "hash", FullName = "Second Parent", IsActive = true, AccountType = AccountType.Parent };
        db.Users.Add(secondParent);
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = secondParent.Id, StudentId = fixture.StudentId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var router = new FakeNotificationRouter();

        await FeeReminderScanner.ScanAsync(db, router, nowUtc, daysBeforeDue: 3);

        Assert.Equal(2, router.Sent.Count);
        Assert.Contains(router.Sent, s => s.RecipientId == fixture.ParentId);
        Assert.Contains(router.Sent, s => s.RecipientId == secondParent.Id);
    }
}

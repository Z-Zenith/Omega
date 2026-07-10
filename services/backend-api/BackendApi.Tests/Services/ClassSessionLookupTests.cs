using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Services;

// SDA-12
public class ClassSessionLookupTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid studentId, Guid teacherId, Guid sectionId, Guid subjectId, TimetableSlot slot)> SeedEnrolledStudentAsync(
        AppDbContext db, int dayOfWeek, TimeOnly start, TimeOnly end)
    {
        var collegeId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Users.Add(new User { Id = teacherId, CollegeId = collegeId, Identifier = "teacher-1", PasswordHash = "hash", FullName = "Teacher One", IsActive = true, AccountType = AccountType.Teacher });
        db.Users.Add(new User { Id = studentId, CollegeId = collegeId, Identifier = "student-1", PasswordHash = "hash", FullName = "Student One", IsActive = true, AccountType = AccountType.Student });
        db.Sections.Add(new Section { Id = sectionId, DepartmentId = Guid.NewGuid(), Year = 1, Name = "Section A" });

        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            SectionId = sectionId,
            SubjectId = subjectId,
            TeacherId = teacherId,
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
            ManuallyEdited = false,
        };
        db.TimetableSlots.Add(slot);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = sectionId, StudentId = studentId });

        await db.SaveChangesAsync();
        return (studentId, teacherId, sectionId, subjectId, slot);
    }

    [Fact]
    public async Task ReturnsNull_WhenNoClassIsScheduledRightNow()
    {
        using var db = NewDb();
        var now = new DateTime(2026, 7, 6, 9, 30, 0, DateTimeKind.Utc); // Monday
        var (studentId, _, _, _, _) = await SeedEnrolledStudentAsync(db, dayOfWeek: (int)DayOfWeek.Monday, start: new TimeOnly(11, 0), end: new TimeOnly(12, 0));

        var result = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, now);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsActiveSessionAndCreatesIt_WhenAClassIsScheduledRightNow()
    {
        using var db = NewDb();
        var now = new DateTime(2026, 7, 6, 9, 30, 0, DateTimeKind.Utc); // Monday
        var (studentId, teacherId, sectionId, subjectId, _) = await SeedEnrolledStudentAsync(db, dayOfWeek: (int)DayOfWeek.Monday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));

        var result = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, now);

        Assert.NotNull(result);
        Assert.Equal(teacherId, result!.TeacherId);
        Assert.Equal(sectionId, result.SectionId);
        Assert.Equal(subjectId, result.SubjectId);
        Assert.Single(db.ClassSessions);
    }

    [Fact]
    public async Task ReturnsExistingSession_InsteadOfCreatingADuplicate()
    {
        using var db = NewDb();
        var now = new DateTime(2026, 7, 6, 9, 30, 0, DateTimeKind.Utc); // Monday
        var (studentId, _, _, _, slot) = await SeedEnrolledStudentAsync(db, dayOfWeek: (int)DayOfWeek.Monday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));

        var first = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, now);
        var second = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, now.AddMinutes(5));

        Assert.Equal(first!.ClassSessionId, second!.ClassSessionId);
        Assert.Single(db.ClassSessions);
        Assert.Equal(slot.Id, db.ClassSessions.Single().TimetableSlotId);
    }

    [Fact]
    public async Task PrefersActualTeacher_OverTimetableAssignedTeacher()
    {
        using var db = NewDb();
        var now = new DateTime(2026, 7, 6, 9, 30, 0, DateTimeKind.Utc); // Monday
        var (studentId, _, _, _, slot) = await SeedEnrolledStudentAsync(db, dayOfWeek: (int)DayOfWeek.Monday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));

        var substituteTeacherId = Guid.NewGuid();
        db.Users.Add(new User { Id = substituteTeacherId, CollegeId = Guid.NewGuid(), Identifier = "sub-1", PasswordHash = "hash", FullName = "Substitute", IsActive = true, AccountType = AccountType.Teacher });
        db.ClassSessions.Add(new ClassSession
        {
            Id = Guid.NewGuid(),
            TimetableSlotId = slot.Id,
            SessionDate = DateOnly.FromDateTime(now),
            ActualTeacherId = substituteTeacherId,
        });
        await db.SaveChangesAsync();

        var result = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, now);

        Assert.Equal(substituteTeacherId, result!.TeacherId);
    }

    [Fact]
    public async Task ReturnsNull_WhenStudentIsNotEnrolledInTheSection()
    {
        using var db = NewDb();
        var now = new DateTime(2026, 7, 6, 9, 30, 0, DateTimeKind.Utc); // Monday
        await SeedEnrolledStudentAsync(db, dayOfWeek: (int)DayOfWeek.Monday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));
        var unenrolledStudentId = Guid.NewGuid();
        db.Users.Add(new User { Id = unenrolledStudentId, CollegeId = Guid.NewGuid(), Identifier = "student-2", PasswordHash = "hash", FullName = "Student Two", IsActive = true, AccountType = AccountType.Student });
        await db.SaveChangesAsync();

        var result = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, unenrolledStudentId, now);

        Assert.Null(result);
    }
}

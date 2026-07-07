using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests;

public class TimetableControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(AccountType accountType) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = $"Test {accountType}",
        AccountType = accountType,
        IsActive = true,
    };

    private static TimetableController ControllerAs(AppDbContext db, User user) => new(db, new PermissionService(db))
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

    private record Fixture(TimetableSlot Slot, List<User> Students);

    // Sets up one teacher-owned slot for a section with `studentCount` enrolled students.
    private static async Task<Fixture> SeedSectionAsync(AppDbContext db, User teacher, int studentCount)
    {
        var section = new Section { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Year = 1, Name = "CS-A" };
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = section.DepartmentId, Code = "CS101", Name = "Intro to CS" };
        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };
        db.Sections.Add(section);
        db.Subjects.Add(subject);
        db.TimetableSlots.Add(slot);
        db.Users.Add(teacher);

        var students = new List<User>();
        for (var i = 0; i < studentCount; i++)
        {
            var student = NewUser(AccountType.Student);
            students.Add(student);
            db.Users.Add(student);
            db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student.Id });
        }

        await db.SaveChangesAsync();
        return new Fixture(slot, students);
    }

    // TWA-08
    [Fact]
    public async Task Twa08_MarkAttendance_ForbidsCallersWhoAreNotTeachers()
    {
        await using var db = NewDb();
        var notTeacher = NewUser(AccountType.AdminTier);
        db.Users.Add(notTeacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, notTeacher);
        var result = await controller.MarkAttendance(new MarkAttendanceRequest(Guid.NewGuid(), null, []));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-08 — scoped to the teacher's own assigned section only.
    [Fact]
    public async Task Twa08_MarkAttendance_ForbidsTeacherNotAssignedToSlot()
    {
        await using var db = NewDb();
        var owningTeacher = NewUser(AccountType.Teacher);
        var otherTeacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, owningTeacher, studentCount: 2);
        db.Users.Add(otherTeacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var request = new MarkAttendanceRequest(fixture.Slot.Id, new DateOnly(2026, 7, 6),
            fixture.Students.Select(s => new AttendanceEntryRequest(s.Id, "Present")).ToList());
        var result = await controller.MarkAttendance(request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Twa08_MarkAttendance_NotFoundForUnknownSlot()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.MarkAttendance(new MarkAttendanceRequest(Guid.NewGuid(), null, []));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // TWA-08 AC: every enrolled student must have a status set after marking completes —
    // an incomplete submission must be rejected outright, not partially saved.
    [Fact]
    public async Task Twa08_MarkAttendance_RejectsIncompleteRoster()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 3);

        var controller = ControllerAs(db, teacher);
        var request = new MarkAttendanceRequest(fixture.Slot.Id, new DateOnly(2026, 7, 6),
            fixture.Students.Take(2).Select(s => new AttendanceEntryRequest(s.Id, "Present")).ToList());
        var result = await controller.MarkAttendance(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await db.AttendanceRecords.CountAsync());
        Assert.Contains("incomplete_attendance", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task Twa08_MarkAttendance_RejectsUnknownStudent()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 1);
        var outsider = NewUser(AccountType.Student);
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var entries = fixture.Students.Select(s => new AttendanceEntryRequest(s.Id, "Present"))
            .Append(new AttendanceEntryRequest(outsider.Id, "Present"))
            .ToList();
        var result = await controller.MarkAttendance(new MarkAttendanceRequest(fixture.Slot.Id, new DateOnly(2026, 7, 6), entries));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("unknown_student", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task Twa08_MarkAttendance_RejectsInvalidStatusValue()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 1);

        var controller = ControllerAs(db, teacher);
        var request = new MarkAttendanceRequest(fixture.Slot.Id, new DateOnly(2026, 7, 6),
            [new AttendanceEntryRequest(fixture.Students[0].Id, "on_fire")]);
        var result = await controller.MarkAttendance(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("invalid_status", badRequest.Value!.ToString());
    }

    // TWA-08 AC: every enrolled student must have a status set after marking completes.
    [Fact]
    public async Task Twa08_MarkAttendance_CompleteRosterCreatesRecordForEveryEnrolledStudent()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 3);

        var controller = ControllerAs(db, teacher);
        var entries = new List<AttendanceEntryRequest>
        {
            new(fixture.Students[0].Id, "Present"),
            new(fixture.Students[1].Id, "Absent"),
            new(fixture.Students[2].Id, "Late"),
        };
        var result = await controller.MarkAttendance(new MarkAttendanceRequest(fixture.Slot.Id, new DateOnly(2026, 7, 6), entries));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MarkAttendanceResponse>(ok.Value);
        Assert.Equal(3, response.Records.Count);
        Assert.Equal(fixture.Slot.SectionId, response.SectionId);

        var session = await db.ClassSessions.SingleAsync(s => s.TimetableSlotId == fixture.Slot.Id);
        Assert.Equal(new DateOnly(2026, 7, 6), session.SessionDate);

        var records = await db.AttendanceRecords.Where(r => r.ClassSessionId == session.Id).ToListAsync();
        Assert.Equal(3, records.Count);
        foreach (var student in fixture.Students)
        {
            Assert.Contains(records, r => r.StudentId == student.Id && r.MarkedBy == teacher.Id);
        }
    }

    // TWA-08
    [Fact]
    public async Task Twa08_Roster_ReturnsEnrolledStudentsForOwnSlot()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 2);

        var controller = ControllerAs(db, teacher);
        var result = await controller.Roster(fixture.Slot.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var roster = Assert.IsType<List<RosterStudentDto>>(ok.Value);
        Assert.Equal(2, roster.Count);
        Assert.All(fixture.Students, s => Assert.Contains(roster, r => r.StudentId == s.Id));
    }

    // TWA-08
    [Fact]
    public async Task Twa08_Roster_ForbidsTeacherNotAssignedToSlot()
    {
        await using var db = NewDb();
        var owningTeacher = NewUser(AccountType.Teacher);
        var otherTeacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, owningTeacher, studentCount: 1);
        db.Users.Add(otherTeacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var result = await controller.Roster(fixture.Slot.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-08 — re-marking the same session (e.g. correcting a mistake) updates in place
    // rather than duplicating rows, respecting the DB's unique (session, student) constraint.
    [Fact]
    public async Task Twa08_MarkAttendance_ReMarkingSameSessionUpdatesExistingRecordsInPlace()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var fixture = await SeedSectionAsync(db, teacher, studentCount: 2);
        var date = new DateOnly(2026, 7, 6);

        var controller = ControllerAs(db, teacher);
        await controller.MarkAttendance(new MarkAttendanceRequest(fixture.Slot.Id, date,
        [
            new AttendanceEntryRequest(fixture.Students[0].Id, "Present"),
            new AttendanceEntryRequest(fixture.Students[1].Id, "Present"),
        ]));

        await controller.MarkAttendance(new MarkAttendanceRequest(fixture.Slot.Id, date,
        [
            new AttendanceEntryRequest(fixture.Students[0].Id, "Absent"),
            new AttendanceEntryRequest(fixture.Students[1].Id, "Present"),
        ]));

        Assert.Equal(2, await db.AttendanceRecords.CountAsync());
        var updated = await db.AttendanceRecords.SingleAsync(r => r.StudentId == fixture.Students[0].Id);
        Assert.Equal(AttendanceStatus.Absent, updated.Status);
    }
}

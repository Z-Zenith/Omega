using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class SubjectsControllerTests
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
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static SubjectsController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new SubjectsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    private static (Section Section, Subject Subject, User Teacher) SeedTaughtSection(AppDbContext db, User student)
    {
        var teacher = NewUser(AccountType.Teacher);
        var section = new Section { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Year = 3, Name = "3rd Year CSE - A" };
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Code = "CS301", Name = "Operating Systems" };
        db.Users.Add(teacher);
        db.Sections.Add(section);
        db.Subjects.Add(subject);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student.Id });
        db.TeacherSectionAssignments.Add(new TeacherSectionAssignment { Id = Guid.NewGuid(), TeacherId = teacher.Id, SectionId = section.Id, SubjectId = subject.Id });
        return (section, subject, teacher);
    }

    // SDA-18: acceptance-critical — "every enrolled subject has a non-empty course-info
    // and teacher-info entry".
    [Fact]
    public async Task Sda18_Mine_ReturnsCourseAndTeacherInfoForEveryEnrolledSubject()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var (_, subject, teacher) = SeedTaughtSection(db, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Mine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsType<List<MySubjectDto>>(ok.Value);
        var entry = Assert.Single(subjects);
        Assert.Equal(subject.Id, entry.SubjectId);
        Assert.Equal("CS301", entry.SubjectCode);
        Assert.Equal("Operating Systems", entry.SubjectName);
        Assert.Equal(teacher.Id, entry.TeacherId);
        Assert.Equal(teacher.FullName, entry.TeacherName);
    }

    // SDA-18
    [Fact]
    public async Task Sda18_Mine_ExcludesSubjectsFromSectionsCallerIsNotEnrolledIn()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var otherStudent = NewUser(AccountType.Student);
        db.Users.AddRange(student, otherStudent);
        SeedTaughtSection(db, otherStudent);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Mine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsType<List<MySubjectDto>>(ok.Value);
        Assert.Empty(subjects);
    }

    // SDA-18: a student enrolled in multiple sections/subjects sees all of them.
    [Fact]
    public async Task Sda18_Mine_ReturnsMultipleSubjectsAcrossDifferentSectionsAndTeachers()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var (_, firstSubject, firstTeacher) = SeedTaughtSection(db, student);
        var (_, secondSubject, secondTeacher) = SeedTaughtSection(db, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Mine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsType<List<MySubjectDto>>(ok.Value);
        Assert.Equal(2, subjects.Count);
        Assert.Contains(subjects, s => s.SubjectId == firstSubject.Id && s.TeacherId == firstTeacher.Id);
        Assert.Contains(subjects, s => s.SubjectId == secondSubject.Id && s.TeacherId == secondTeacher.Id);
    }

    // SDA-18: Subject.TeacherId is the canonical teacher elsewhere (AssignmentsController
    // gates assignment creation on it) — when it's set, it must win over the
    // TeacherSectionAssignment row's own TeacherId so a student isn't told a different
    // teacher here than assignments for the same subject come from.
    [Fact]
    public async Task Sda18_Mine_PrefersSubjectTeacherId_OverAssignmentTeacher_WhenBothPresent()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var (_, subject, assignmentTeacher) = SeedTaughtSection(db, student);
        var canonicalTeacher = NewUser(AccountType.Teacher);
        db.Users.Add(canonicalTeacher);
        subject.TeacherId = canonicalTeacher.Id;
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Mine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsType<List<MySubjectDto>>(ok.Value);
        var entry = Assert.Single(subjects);
        Assert.Equal(canonicalTeacher.Id, entry.TeacherId);
        Assert.NotEqual(assignmentTeacher.Id, entry.TeacherId);
    }

    // SDA-18: co-teaching (two different teachers assigned to the same section+subject,
    // which the schema's unique index on (teacher_id, section_id, subject_id) allows) must
    // not be collapsed into a single entry when Subject.TeacherId is unset — Distinct()
    // should only fold together true duplicates, not legitimately different assignment rows.
    [Fact]
    public async Task Sda18_Mine_DoesNotCollapseCoTeachingAssignments_WhenSubjectTeacherIdIsUnset()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var (section, subject, firstTeacher) = SeedTaughtSection(db, student);
        var secondTeacher = NewUser(AccountType.Teacher);
        db.Users.Add(secondTeacher);
        db.TeacherSectionAssignments.Add(new TeacherSectionAssignment
        {
            Id = Guid.NewGuid(),
            TeacherId = secondTeacher.Id,
            SectionId = section.Id,
            SubjectId = subject.Id,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Mine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsType<List<MySubjectDto>>(ok.Value);
        Assert.Equal(2, subjects.Count);
        Assert.Contains(subjects, s => s.TeacherId == firstTeacher.Id);
        Assert.Contains(subjects, s => s.TeacherId == secondTeacher.Id);
    }
}

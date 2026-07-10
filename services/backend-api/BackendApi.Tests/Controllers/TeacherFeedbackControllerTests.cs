using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class TeacherFeedbackControllerTests
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

    private static TeacherFeedbackController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new TeacherFeedbackController(db)
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

    // SDA-17
    [Fact]
    public async Task Sda17_MyTeachers_ReturnsTeachersOfCallersEnrolledSections()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var (_, subject, teacher) = SeedTaughtSection(db, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.MyTeachers();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var teachers = Assert.IsType<List<MyTeacherDto>>(ok.Value);
        var entry = Assert.Single(teachers);
        Assert.Equal(teacher.Id, entry.TeacherId);
        Assert.Equal(subject.Id, entry.SubjectId);
        Assert.Equal("Operating Systems", entry.SubjectName);
    }

    // SDA-17
    [Fact]
    public async Task Sda17_MyTeachers_ExcludesTeachersOfSectionsCallerIsNotEnrolledIn()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var otherStudent = NewUser(AccountType.Student);
        db.Users.AddRange(student, otherStudent);
        SeedTaughtSection(db, otherStudent);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.MyTeachers();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var teachers = Assert.IsType<List<MyTeacherDto>>(ok.Value);
        Assert.Empty(teachers);
    }

    // SDA-17
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Sda17_SubmitFeedback_RejectsRatingOutsideOneToFive(int rating)
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var (_, _, teacher) = SeedTaughtSection(db, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitFeedback(new SubmitTeacherFeedbackRequest(teacher.Id, rating, "comment"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-17: acceptance-critical — "feedback is attributable to the ... teacher it was
    // submitted against" — a student can't give feedback about a teacher who has never
    // taught any of their enrolled sections.
    [Fact]
    public async Task Sda17_SubmitFeedback_ForbidsFeedbackAboutATeacherWhoNeverTaughtTheStudent()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var unrelatedTeacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, unrelatedTeacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitFeedback(new SubmitTeacherFeedbackRequest(unrelatedTeacher.Id, 3, "comment"));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // SDA-17
    [Fact]
    public async Task Sda17_SubmitFeedback_CreatesFeedbackAttributedToStudentAndTeacher()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var (_, _, teacher) = SeedTaughtSection(db, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitFeedback(new SubmitTeacherFeedbackRequest(teacher.Id, 4, "Explains concepts clearly."));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TeacherFeedbackDto>(ok.Value);
        Assert.Equal(teacher.Id, dto.TeacherId);
        Assert.Equal(4, dto.Rating);
        Assert.Equal("Explains concepts clearly.", dto.Comments);

        var stored = await db.TeacherFeedbacks.SingleAsync();
        Assert.Equal(student.Id, stored.StudentId);
        Assert.Equal(teacher.Id, stored.TeacherId);
    }
}

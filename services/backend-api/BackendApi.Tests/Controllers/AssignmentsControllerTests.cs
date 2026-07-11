using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

public class AssignmentsControllerTests
{
    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record Fixture(Guid TeacherId, Guid StudentId, Guid OtherStudentId, Guid SubjectId, Guid SectionId, Guid AssignmentId);

    // #135: an assignment's subject is taught to a section via TeacherSectionAssignments,
    // and a student is only bound to that subject by being enrolled (SectionEnrollments) in
    // one of the sections it's taught to. `enrollStudent: false` reproduces the pre-fix IDOR:
    // a student with valid auth but no enrollment link to this assignment's subject at all.
    private static async Task<Fixture> SeedAsync(AppDbContext db, bool enrollStudent = true)
    {
        var collegeId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        db.Departments.Add(new Department { Id = departmentId, CollegeId = collegeId, Name = "CS" });
        db.Users.Add(new User { Id = teacherId, CollegeId = collegeId, Identifier = "teacher-1", PasswordHash = "hash", FullName = "Teacher", IsActive = true, AccountType = AccountType.Teacher, DepartmentId = departmentId });
        db.Users.Add(new User { Id = studentId, CollegeId = collegeId, Identifier = "student-1", PasswordHash = "hash", FullName = "Student", IsActive = true, AccountType = AccountType.Student, DepartmentId = departmentId });
        db.Users.Add(new User { Id = otherStudentId, CollegeId = collegeId, Identifier = "student-2", PasswordHash = "hash", FullName = "Other Student", IsActive = true, AccountType = AccountType.Student, DepartmentId = departmentId });
        db.Sections.Add(new Section { Id = sectionId, DepartmentId = departmentId, Year = 1, Name = "A" });
        db.Subjects.Add(new Subject { Id = subjectId, DepartmentId = departmentId, Code = "CS101", Name = "Intro to CS", TeacherId = teacherId });
        db.TeacherSectionAssignments.Add(new TeacherSectionAssignment { Id = Guid.NewGuid(), TeacherId = teacherId, SectionId = sectionId, SubjectId = subjectId });
        db.Assignments.Add(new Assignment
        {
            Id = assignmentId,
            SubjectId = subjectId,
            TeacherId = teacherId,
            Title = "HW1",
            Type = AssignmentType.FileUpload,
            DueDate = DateTime.UtcNow.AddDays(7),
            SubmissionWindowStart = DateTime.UtcNow.AddHours(-1),
            SubmissionWindowEnd = DateTime.UtcNow.AddDays(7),
        });

        if (enrollStudent)
        {
            db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = sectionId, StudentId = studentId });
        }
        // otherStudentId is deliberately never enrolled anywhere — stands in for "any other
        // authenticated student in the system" probing this assignment id.

        await db.SaveChangesAsync();
        return new Fixture(teacherId, studentId, otherStudentId, subjectId, sectionId, assignmentId);
    }

    private static AssignmentsController ControllerAs(AppDbContext db, Guid userId) =>
        new(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth")),
                },
            },
        };

    [Fact]
    public async Task Submit_Succeeds_WhenStudentIsEnrolledInSubjectsSection()
    {
        using var db = NewDb();
        var fixture = await SeedAsync(db);
        var controller = ControllerAs(db, fixture.StudentId);

        var result = await controller.Submit(fixture.AssignmentId, new SubmitAssignmentRequest("https://files.campus.local/a.pdf", AssignmentType.FileUpload));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<SubmissionDto>(ok.Value);
    }

    // #135 (IDOR): before the fix, any authenticated student could submit against any
    // assignment id, regardless of enrollment — only AccountType==Student was checked.
    [Fact]
    public async Task Submit_ReturnsNotFound_WhenStudentNotEnrolledInAssignmentsSubject()
    {
        using var db = NewDb();
        var fixture = await SeedAsync(db);
        var controller = ControllerAs(db, fixture.OtherStudentId);

        var result = await controller.Submit(fixture.AssignmentId, new SubmitAssignmentRequest("https://files.campus.local/a.pdf", AssignmentType.FileUpload));

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(await db.Submissions.ToListAsync());
    }

    [Fact]
    public async Task AutoSubmit_Succeeds_WhenStudentIsEnrolledInSubjectsSection()
    {
        using var db = NewDb();
        var fixture = await SeedAsync(db);
        var controller = ControllerAs(db, fixture.StudentId);

        var result = await controller.AutoSubmit(fixture.AssignmentId, new SubmitAssignmentRequest("https://files.campus.local/a.pdf", AssignmentType.FileUpload));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<SubmissionDto>(ok.Value);
    }

    // #135 (IDOR): same gap as Submit(), on the auto-submit path used by the desktop app's
    // exit-detection trigger (SDA-11).
    [Fact]
    public async Task AutoSubmit_ReturnsNotFound_WhenStudentNotEnrolledInAssignmentsSubject()
    {
        using var db = NewDb();
        var fixture = await SeedAsync(db);
        var controller = ControllerAs(db, fixture.OtherStudentId);

        var result = await controller.AutoSubmit(fixture.AssignmentId, new SubmitAssignmentRequest("https://files.campus.local/a.pdf", AssignmentType.FileUpload));

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(await db.Submissions.ToListAsync());
    }
}

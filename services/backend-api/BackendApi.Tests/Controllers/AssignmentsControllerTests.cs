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

    private static AssignmentsController ControllerAs(AppDbContext db, User user) => new(db)
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

    // #135 (already merged, see below) requires the submitting student to be enrolled in a
    // section the assignment's subject is taught to - seed that link too so these #159 tests
    // (which predate #135) still reach the "already submitted" logic they're actually testing.
    private static (Subject Subject, Assignment Assignment) SeedAssignment(AppDbContext db, User teacher, User student, DateTime? windowStart = null, DateTime? windowEnd = null)
    {
        var departmentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = departmentId, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            Title = "HW1",
            Type = AssignmentType.FileUpload,
            DueDate = DateTime.UtcNow.AddDays(1),
            SubmissionWindowStart = windowStart ?? DateTime.UtcNow.AddHours(-1),
            SubmissionWindowEnd = windowEnd ?? DateTime.UtcNow.AddHours(1),
        };
        db.Subjects.Add(subject);
        db.Assignments.Add(assignment);
        db.Sections.Add(new Section { Id = sectionId, DepartmentId = departmentId, Year = 1, Name = "A" });
        db.TeacherSectionAssignments.Add(new TeacherSectionAssignment { Id = Guid.NewGuid(), TeacherId = teacher.Id, SectionId = sectionId, SubjectId = subject.Id });
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = sectionId, StudentId = student.Id });
        return (subject, assignment);
    }

    // #159: Submit had no equivalent of AutoSubmit's "already_submitted" guard, so a student
    // could call it repeatedly and accumulate unlimited submissions for the same assignment.
    [Fact]
    public async Task Issue159_Submit_RejectsSecondSubmission_WithConflict()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, student);
        var (_, assignment) = SeedAssignment(db, teacher, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var first = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://example.com/a.pdf", AssignmentType.FileUpload));
        Assert.IsType<OkObjectResult>(first.Result);

        var second = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://example.com/b.pdf", AssignmentType.FileUpload));

        var conflict = Assert.IsType<ConflictObjectResult>(second.Result);
        Assert.Single(await db.Submissions.Where(s => s.AssignmentId == assignment.Id && s.StudentId == student.Id).ToListAsync());
    }

    // #159: a prior auto-submission also blocks a later manual Submit call, consistent with
    // AutoSubmit's own "already has a submission (manual or auto)" rule.
    [Fact]
    public async Task Issue159_Submit_RejectsWhenAlreadyAutoSubmitted()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, student);
        var (_, assignment) = SeedAssignment(db, teacher, student);
        db.Submissions.Add(new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            StudentId = student.Id,
            ContentUrl = "https://example.com/auto.pdf",
            SubmittedAt = DateTime.UtcNow,
            IsAutosubmitted = true,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://example.com/manual.pdf", AssignmentType.FileUpload));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Issue159_Submit_AllowsFirstSubmission()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, student);
        var (_, assignment) = SeedAssignment(db, teacher, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://example.com/a.pdf", AssignmentType.FileUpload));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SubmissionDto>(ok.Value);
        Assert.False(dto.IsAutosubmitted);
    }

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

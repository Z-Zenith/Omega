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

    private static (Subject Subject, Assignment Assignment) SeedAssignment(AppDbContext db, User teacher, DateTime? windowStart = null, DateTime? windowEnd = null)
    {
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
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
        var (_, assignment) = SeedAssignment(db, teacher);
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
        var (_, assignment) = SeedAssignment(db, teacher);
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
        var (_, assignment) = SeedAssignment(db, teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://example.com/a.pdf", AssignmentType.FileUpload));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SubmissionDto>(ok.Value);
        Assert.False(dto.IsAutosubmitted);
    }
}

using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests;

public class AssignmentsControllerTests
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

    private static Department NewDepartment() => new() { Id = Guid.NewGuid(), Name = "CS", CollegeId = Guid.NewGuid() };

    private static AssignmentsController ControllerAs(AppDbContext db, User user, FakeAiServicesClient? aiServices = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new AssignmentsController(db, aiServices ?? new FakeAiServicesClient())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    private static CreateAssignmentRequest ValidCreateRequest(Guid subjectId, AssignmentType type = AssignmentType.Essay) =>
        new(subjectId, "Essay 1", "Write about something", type,
            new DateTime(2026, 8, 15), new DateTime(2026, 8, 1), new DateTime(2026, 8, 15), null);

    // TWA-07
    [Fact]
    public async Task Twa07_Create_RejectsMissingDueDate()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var request = ValidCreateRequest(subject.Id) with { DueDate = default };
        var result = await controller.Create(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-07
    [Fact]
    public async Task Twa07_Create_RejectsInvertedSubmissionWindow()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var request = ValidCreateRequest(subject.Id) with { SubmissionWindowStart = new DateTime(2026, 8, 15), SubmissionWindowEnd = new DateTime(2026, 8, 1) };
        var result = await controller.Create(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-07
    [Fact]
    public async Task Twa07_Create_RejectsMalformedTypeSpecificSettingsJson()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var request = ValidCreateRequest(subject.Id) with { TypeSpecificSettings = "{not json" };
        var result = await controller.Create(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-07
    [Fact]
    public async Task Twa07_Create_ForbidsTeacherWhoDoesNotTeachTheSubject()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var subjectTeacher = NewUser(AccountType.Teacher);
        var otherTeacher = NewUser(AccountType.Teacher);
        db.Departments.Add(department);
        db.Users.AddRange(subjectTeacher, otherTeacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = subjectTeacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var result = await controller.Create(ValidCreateRequest(subject.Id));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-07
    [Fact]
    public async Task Twa07_Create_AllowsAdminRegardlessOfSubjectTeacher()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var subjectTeacher = NewUser(AccountType.Teacher);
        var admin = NewUser(AccountType.AdminTier);
        db.Departments.Add(department);
        db.Users.AddRange(subjectTeacher, admin);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = subjectTeacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.Create(ValidCreateRequest(subject.Id));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // TWA-07
    [Fact]
    public async Task Twa07_Create_CreatesAssignment_ForSubjectsOwnTeacher()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.Create(ValidCreateRequest(subject.Id, AssignmentType.Code));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AssignmentDto>(ok.Value);
        Assert.Equal("Code", dto.Type);
        Assert.Single(await db.Assignments.ToListAsync());
    }

    // SDA-10
    [Fact]
    public async Task Sda10_Submit_ForbidsNonStudentAccounts()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://files.example/essay.docx", AssignmentType.Essay));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // SDA-10
    [Fact]
    public async Task Sda10_Submit_ReturnsNotFound_ForUnknownAssignment()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(Guid.NewGuid(), new SubmitAssignmentRequest("https://files.example/essay.docx", AssignmentType.Essay));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // SDA-10: acceptance-critical — "a quiz-type assignment cannot be submitted as a
    // file upload", generalized to any assignment-type/submission-format mismatch.
    [Fact]
    public async Task Sda10_Submit_RejectsFormatMismatch_QuizAssignmentSubmittedAsFileUpload()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "Quiz 1", Type = AssignmentType.Quiz, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://files.example/upload.zip", AssignmentType.FileUpload));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-10
    [Fact]
    public async Task Sda10_Submit_RejectsEmptyContentUrl()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "Essay 1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("   ", AssignmentType.Essay));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-10: acceptance-critical — "submission after the deadline is flagged as late,
    // not silently accepted as on-time".
    [Fact]
    public async Task Sda10_Submit_FlagsLateSubmission_WhenPastDueDate()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "Essay 1", Type = AssignmentType.Essay, DueDate = DateTime.UtcNow.AddDays(-1), SubmissionWindowStart = DateTime.UtcNow.AddDays(-10), SubmissionWindowEnd = DateTime.UtcNow.AddDays(-1) };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://files.example/essay.docx", AssignmentType.Essay));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SubmissionDto>(ok.Value);
        Assert.True(dto.IsLate);
        Assert.False(dto.IsAutosubmitted);
    }

    // SDA-10
    [Fact]
    public async Task Sda10_Submit_DoesNotFlagLate_WhenBeforeDueDate()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "Essay 1", Type = AssignmentType.Essay, DueDate = DateTime.UtcNow.AddDays(5), SubmissionWindowStart = DateTime.UtcNow.AddDays(-1), SubmissionWindowEnd = DateTime.UtcNow.AddDays(5) };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Submit(assignment.Id, new SubmitAssignmentRequest("https://files.example/essay.docx", AssignmentType.Essay));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SubmissionDto>(ok.Value);
        Assert.False(dto.IsLate);
    }

    // AIS-03
    [Fact]
    public async Task Ais03_CopyCheck_ForbidsCallersWhoAreNotTheAssignmentsTeacher()
    {
        await using var db = NewDb();
        var otherTeacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "A1", Type = AssignmentType.Code, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        db.Users.Add(otherTeacher);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var result = await controller.CopyCheck(assignment.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Ais03_CopyCheck_PersistsFlaggedMatchesFromAiServices()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Code, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submissionA = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "print(1)", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        var submissionB = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "print(1)", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        db.Users.Add(teacher);
        db.Assignments.Add(assignment);
        db.Submissions.AddRange(submissionA, submissionB);
        await db.SaveChangesAsync();

        var fakeAi = new FakeAiServicesClient
        {
            SimilarityMatches = [new(submissionA.Id.ToString(), submissionB.Id.ToString(), 0.95)],
        };
        var controller = ControllerAs(db, teacher, fakeAi);
        var result = await controller.CopyCheck(assignment.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var matches = Assert.IsType<List<CopyCheckMatchDto>>(ok.Value);
        var match = Assert.Single(matches);
        Assert.Equal(0.95m, match.SimilarityScore);
        Assert.Single(await db.CopyCheckFlags.ToListAsync());
    }

    [Fact]
    public async Task Ais03_CopyCheck_ReCheckReplacesPreviousFlags()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Code, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submissionA = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "print(1)", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        var submissionB = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "print(2)", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        db.Users.Add(teacher);
        db.Assignments.Add(assignment);
        db.Submissions.AddRange(submissionA, submissionB);
        db.CopyCheckFlags.Add(new CopyCheckFlag { Id = Guid.NewGuid(), SubmissionAId = submissionA.Id, SubmissionBId = submissionB.Id, SimilarityScore = 0.99m, FlaggedAt = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        // Nothing matches this time — the stale flag from the previous run must go away.
        var controller = ControllerAs(db, teacher, new FakeAiServicesClient());
        var result = await controller.CopyCheck(assignment.Id);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(await db.CopyCheckFlags.ToListAsync());
    }

    // AIS-04
    [Fact]
    public async Task Ais04_AutogradeSuggestion_ForbidsCallersWhoAreNotTheAssignmentsTeacher()
    {
        await using var db = NewDb();
        var otherTeacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submission = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "my essay", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        db.Users.Add(otherTeacher);
        db.Assignments.Add(assignment);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var result = await controller.AutogradeSuggestion(submission.Id, new RequestAutogradeSuggestion([new("Thesis", ["thesis"], 1.0)], 100));

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Ais04_AutogradeSuggestion_RejectsRubricWeightsThatDoNotSumToOne()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submission = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "my essay", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        db.Users.Add(teacher);
        db.Assignments.Add(assignment);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.AutogradeSuggestion(submission.Id, new RequestAutogradeSuggestion([new("Thesis", ["thesis"], 0.5)], 100));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Ais04_AutogradeSuggestion_PersistsSuggestedGrade_AndReturnsAdvisoryDetail()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submission = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "my essay about the thesis", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        db.Users.Add(teacher);
        db.Assignments.Add(assignment);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var fakeAi = new FakeAiServicesClient
        {
            AutogradeResult = new(82.5, 100, 0.8, ["Thesis"], ["Good coverage of the thesis criterion."]),
        };
        var controller = ControllerAs(db, teacher, fakeAi);
        var result = await controller.AutogradeSuggestion(submission.Id, new RequestAutogradeSuggestion([new("Thesis", ["thesis"], 1.0)], 100));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AutogradeSuggestionDto>(ok.Value);
        Assert.Equal(82.5m, dto.SuggestedGrade);
        Assert.Equal("Good coverage of the thesis criterion.", Assert.Single(dto.Feedback));

        var stored = Assert.Single(await db.AutogradeSuggestions.ToListAsync());
        Assert.False(stored.ConfirmedByTeacher);
    }

    [Fact]
    public async Task Ais04_Grade_ConfirmsTheSuggestion_ButDoesNotThrowForATeacherOnlyBookkeepingRecord()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = teacher.Id, Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submission = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "my essay", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        var suggestion = new BackendApi.Data.Entities.AutogradeSuggestion { Id = Guid.NewGuid(), SubmissionId = submission.Id, SuggestedGrade = 80m, ConfirmedByTeacher = false };
        db.Users.Add(teacher);
        db.Assignments.Add(assignment);
        db.Submissions.Add(submission);
        db.AutogradeSuggestions.Add(suggestion);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.Grade(submission.Id, new ConfirmGradeRequest(suggestion.Id));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ConfirmedGradeDto>(ok.Value);
        Assert.True(dto.ConfirmedByTeacher);
        Assert.NotNull(dto.ConfirmedAt);
    }

    [Fact]
    public async Task Ais04_Grade_ForbidsCallersWhoAreNotTheAssignmentsTeacher()
    {
        await using var db = NewDb();
        var otherTeacher = NewUser(AccountType.Teacher);
        var assignment = new Assignment { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Title = "A1", Type = AssignmentType.Essay, DueDate = new DateTime(2026, 8, 15), SubmissionWindowStart = new DateTime(2026, 8, 1), SubmissionWindowEnd = new DateTime(2026, 8, 15) };
        var submission = new Submission { Id = Guid.NewGuid(), AssignmentId = assignment.Id, StudentId = Guid.NewGuid(), ContentUrl = "my essay", SubmittedAt = DateTime.UtcNow, IsLate = false, IsAutosubmitted = false };
        var suggestion = new BackendApi.Data.Entities.AutogradeSuggestion { Id = Guid.NewGuid(), SubmissionId = submission.Id, SuggestedGrade = 80m, ConfirmedByTeacher = false };
        db.Users.Add(otherTeacher);
        db.Assignments.Add(assignment);
        db.Submissions.Add(submission);
        db.AutogradeSuggestions.Add(suggestion);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, otherTeacher);
        var result = await controller.Grade(submission.Id, new ConfirmGradeRequest(suggestion.Id));

        Assert.IsType<ForbidResult>(result.Result);
    }
}

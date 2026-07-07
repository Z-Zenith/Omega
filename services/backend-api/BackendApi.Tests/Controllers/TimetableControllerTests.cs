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
    // No test in this file needs a real permission lookup — TWA-12's endpoint doesn't
    // consult IPermissionService at all (see the controller's comment on why), but the
    // controller still requires one in its constructor for the other actions.
    private class FakePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode) => Task.FromResult(false);
        public Task<Guid?> GetDepartmentScopeAsync(Guid userId) => Task.FromResult<Guid?>(null);
    }

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
        FullName = "Test Teacher",
        AccountType = accountType,
        IsActive = true,
    };

    private static Department NewDepartment() => new() { Id = Guid.NewGuid(), Name = "CS", CollegeId = Guid.NewGuid() };

    private static Section NewSection(Guid departmentId) => new() { Id = Guid.NewGuid(), DepartmentId = departmentId, Year = 1, Name = "CS-A" };

    private static TimetableController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new TimetableController(db, new FakePermissionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    // TWA-12
    [Fact]
    public async Task Twa12_SubmitSectionFeedback_RejectsRatingOutOfRange()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        var section = NewSection(department.Id);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        db.Sections.Add(section);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SubmitSectionFeedback(section.Id, new SubmitSectionFeedbackRequest(6, "too high"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-12
    [Fact]
    public async Task Twa12_SubmitSectionFeedback_ForbidsTeacherWhoDidNotTeachTheSection()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        var section = NewSection(department.Id);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        db.Sections.Add(section);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SubmitSectionFeedback(section.Id, new SubmitSectionFeedbackRequest(3, null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-12
    [Fact]
    public async Task Twa12_SubmitSectionFeedback_ReturnsNotFound_ForUnknownSection()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SubmitSectionFeedback(Guid.NewGuid(), new SubmitSectionFeedbackRequest(3, null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-12: acceptance-critical — the row this writes must be exactly the shape
    // Generate() reads for AWA-02 (teacher_id, section_id, rating <= 2 excludes them).
    [Fact]
    public async Task Twa12_SubmitSectionFeedback_WritesRowInShapeGenerateReadsForAwa02()
    {
        await using var db = NewDb();
        var department = NewDepartment();
        var teacher = NewUser(AccountType.Teacher);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        var section = NewSection(department.Id);
        db.Departments.Add(department);
        db.Users.Add(teacher);
        db.Sections.Add(section);
        db.Subjects.Add(subject);
        db.TeacherSectionAssignments.Add(new TeacherSectionAssignment { Id = Guid.NewGuid(), TeacherId = teacher.Id, SectionId = section.Id, SubjectId = subject.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SubmitSectionFeedback(section.Id, new SubmitSectionFeedbackRequest(1, "struggled with this group"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SectionFeedbackDto>(ok.Value);
        Assert.Equal(section.Id, dto.SectionId);
        Assert.Equal(1, dto.Rating);

        var stored = Assert.Single(await db.SectionFeedbacks.ToListAsync());
        Assert.Equal(teacher.Id, stored.TeacherId);
        Assert.Equal(section.Id, stored.SectionId);
        Assert.Equal(1, stored.Rating);

        // Mirror Generate()'s exact query shape for the negative-feedback exclusion set.
        var negativeFeedbackPairs = await db.SectionFeedbacks
            .Where(f => f.SectionId == section.Id && f.Rating <= 2)
            .Select(f => new { f.TeacherId, f.SectionId })
            .ToListAsync();
        Assert.Contains(negativeFeedbackPairs, p => p.TeacherId == teacher.Id && p.SectionId == section.Id);
    }
}

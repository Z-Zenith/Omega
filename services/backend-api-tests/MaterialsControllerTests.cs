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

public class MaterialsControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(AccountType accountType, Guid? collegeId = null) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = collegeId ?? Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = true,
    };

    private static CommunityController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new CommunityController(db, new PermissionService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    // TWA-06
    [Fact]
    public async Task Twa06_UploadMaterial_ForbidsStudents()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://files.example/a.pdf", null, group.Id));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-06
    [Fact]
    public async Task Twa06_UploadMaterial_RejectsWhenNeitherSubjectNorGroupGiven()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://files.example/a.pdf", null, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-06
    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://wrong-scheme.example/a.pdf")]
    public async Task Twa06_UploadMaterial_RejectsInvalidFileUrl(string invalidUrl)
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", invalidUrl, null, group.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-06
    [Fact]
    public async Task Twa06_UploadMaterial_RejectsUnknownGroupId()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://files.example/a.pdf", null, Guid.NewGuid()));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-06
    [Fact]
    public async Task Twa06_UploadMaterial_CreatesMaterial_ForValidGroupTarget()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://files.example/a.pdf", null, group.Id));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MaterialDto>(ok.Value);
        Assert.Equal("Notes", dto.Title);
        Assert.Equal(teacher.Id, dto.UploadedBy);
        Assert.Single(await db.Materials.ToListAsync());
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_ReturnsNotFound_ForUnknownId()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.DownloadMaterial(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_AllowsUploader()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.DownloadMaterial(material.Id);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://files.example/a.pdf", redirect.Url);
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_ForbidsNonMemberForGroupScopedMaterial()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var outsider = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, outsider);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", GroupId = group.Id, UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, outsider);
        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsType<ForbidResult>(result);
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_AllowsGroupMemberForGroupScopedMaterial()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var member = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, member);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = member.Id });
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", GroupId = group.Id, UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, member);
        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsType<RedirectResult>(result);
    }

    // API-03: acceptance-critical — a student enrolled in the subject's section (via a
    // timetable slot) can download subject-scoped material without being a group member.
    [Fact]
    public async Task Api03_DownloadMaterial_AllowsEnrolledStudentForSubjectScopedMaterial()
    {
        await using var db = NewDb();
        var department = new Department { Id = Guid.NewGuid(), Name = "CS", CollegeId = Guid.NewGuid() };
        var teacher = NewUser(AccountType.Teacher, department.CollegeId);
        var student = NewUser(AccountType.Student, department.CollegeId);
        db.Departments.Add(department);
        db.Users.AddRange(teacher, student);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        var section = new Section { Id = Guid.NewGuid(), DepartmentId = department.Id, Year = 1, Name = "A" };
        db.Subjects.Add(subject);
        db.Sections.Add(section);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student.Id });
        db.TimetableSlots.Add(new TimetableSlot
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        });
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", SubjectId = subject.Id, UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsType<RedirectResult>(result);
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_ForbidsUnenrolledStudentForSubjectScopedMaterial()
    {
        await using var db = NewDb();
        var department = new Department { Id = Guid.NewGuid(), Name = "CS", CollegeId = Guid.NewGuid() };
        var teacher = NewUser(AccountType.Teacher, department.CollegeId);
        var unenrolledStudent = NewUser(AccountType.Student, department.CollegeId);
        db.Departments.Add(department);
        db.Users.AddRange(teacher, unenrolledStudent);
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id };
        db.Subjects.Add(subject);
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", SubjectId = subject.Id, UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, unenrolledStudent);
        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsType<ForbidResult>(result);
    }

    // API-03
    [Fact]
    public async Task Api03_DownloadMaterial_AllowsAdminForAnyMaterial()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var admin = NewUser(AccountType.AdminTier);
        db.Users.AddRange(teacher, admin);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        var material = new Material { Id = Guid.NewGuid(), Title = "Notes", FileUrl = "https://files.example/a.pdf", GroupId = group.Id, UploadedBy = teacher.Id, UploadedAt = DateTime.UtcNow };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsType<RedirectResult>(result);
    }
}

using System.Security.Claims;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests;

public class UsersControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(AccountType accountType, Guid? collegeId = null, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = collegeId ?? Guid.NewGuid(),
        Identifier = $"user-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test User",
        AccountType = accountType,
        IsActive = isActive,
    };

    // Mirrors PermissionService's actual resolution, as a direct grant so tests don't need
    // to seed roles/role-bindings just to exercise the controller's permission check.
    private static PermissionGrant GrantViewAllStudentRecords(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PermissionCode = "view_all_student_records",
        Granted = true,
        GrantedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private static UsersController ControllerAs(AppDbContext db, User user) => new(
        db,
        new FakePasswordHasher(),
        new FakeTotpService(),
        new PermissionService(db))
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

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class FakeTotpService : ITotpService
    {
        public string GenerateSecret() => "secret";
        public bool ValidateCode(string base32Secret, string code) => true;
        public string BuildProvisioningUri(string base32Secret, string accountIdentifier, string issuer) => "otpauth://test";
    }

    // AWA-09: class-level [Authorize] was added to this controller for AWA-07's GetProfile
    // gating — this endpoint had no authentication or permission check at all before, so
    // it needs its own explicit gate rather than inheriting only "any authenticated user".
    [Fact]
    public async Task Awa09_Create_ForbidsCallersWithoutManageAccountsPermission()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.Create(new Contracts.CreateUserRequest(
            Guid.NewGuid(), AccountType.Student, "new-student", "pw", "New Student", null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-09
    [Fact]
    public async Task Awa09_Create_SucceedsForCallerWithManageAccountsPermission()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        db.Users.Add(admin);
        db.PermissionGrants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            PermissionCode = "manage_accounts",
            Granted = true,
            GrantedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.Create(new Contracts.CreateUserRequest(
            Guid.NewGuid(), AccountType.Student, "new-student", "pw", "New Student", null));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    // AWA-10: same reasoning as Awa09_Create_ForbidsCallersWithoutManageAccountsPermission —
    // acceptance-critical that this doesn't allow arbitrary account takeover.
    [Fact]
    public async Task Awa10_ResetPassword_ForbidsCallersWithoutResetPasswordPermission()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var victim = NewUser(AccountType.AdminTier);
        db.Users.AddRange(student, victim);
        var originalHash = victim.PasswordHash;
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.ResetPassword(victim.Id, "attacker-controlled-password");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(originalHash, (await db.Users.FindAsync(victim.Id))!.PasswordHash);
    }

    // AWA-10
    [Fact]
    public async Task Awa10_ResetPassword_SucceedsForCallerWithResetPasswordPermission()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var target = NewUser(AccountType.Student);
        db.Users.AddRange(admin, target);
        db.PermissionGrants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            PermissionCode = "reset_password",
            Granted = true,
            GrantedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.ResetPassword(target.Id, "new-password");

        Assert.IsType<NoContentResult>(result);
    }

    // AWA-07, AWA-08
    [Fact]
    public async Task Awa07_GetProfile_AllowsSelfView_WithoutAnySpecialPermission()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.GetProfile(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<Contracts.StudentRecordDto>(ok.Value);
        Assert.Equal(student.Id, dto.Id);
    }

    // AWA-07
    [Fact]
    public async Task Awa07_GetProfile_ForbidsViewingAnotherStudent_WithoutViewAllStudentRecordsPermission()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        // Same college as the caller so this test isolates the missing-permission branch —
        // a college mismatch alone would also Forbid and mask a broken permission check.
        var student = NewUser(AccountType.Student, teacher.CollegeId);
        db.Users.AddRange(teacher, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.GetProfile(student.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-07: this endpoint is scoped to student records specifically — an Admin with
    // view_all_student_records still can't use it to browse another employee's profile.
    [Fact]
    public async Task Awa07_GetProfile_ForbidsViewingNonStudentAccount_EvenWithPermission()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(admin, teacher);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(teacher.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-07: student records are college-tenant data — an Admin's permission doesn't
    // extend across colleges.
    [Fact]
    public async Task Awa07_GetProfile_ForbidsViewingStudentFromAnotherCollege()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var otherCollegeStudent = NewUser(AccountType.Student);
        db.Users.AddRange(admin, otherCollegeStudent);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(otherCollegeStudent.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-07: acceptance-critical — "record includes remarks... even if the submitting
    // teacher is no longer active".
    [Fact]
    public async Task Awa07_GetProfile_IncludesRemarks_FromADeactivatedTeacher()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student, admin.CollegeId);
        var deactivatedTeacher = NewUser(AccountType.Teacher, isActive: false);
        db.Users.AddRange(admin, student, deactivatedTeacher);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        db.TeacherReports.Add(new TeacherReport
        {
            Id = Guid.NewGuid(),
            TeacherId = deactivatedTeacher.Id,
            StudentId = student.Id,
            Content = "Missed three assignments in a row.",
            SubmittedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<Contracts.StudentRecordDto>(ok.Value);
        var remark = Assert.Single(dto.Remarks);
        Assert.Equal("Missed three assignments in a row.", remark.Content);
        Assert.Equal(deactivatedTeacher.Id, remark.TeacherId);
        Assert.Equal(deactivatedTeacher.FullName, remark.TeacherName);
    }

    // AWA-07: system-generated reports (AIS-01 browsing summary, AIS-07 suspicious flag)
    // surface alongside remarks.
    [Fact]
    public async Task Awa07_GetProfile_IncludesBrowsingSummariesAndSuspiciousFlags()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student, admin.CollegeId);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        db.BrowsingHistorySummaries.Add(new BrowsingHistorySummary
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            SummaryText = "Mostly educational sites this week.",
            GeneratedAt = DateTime.UtcNow,
        });
        db.SuspiciousFlags.Add(new SuspiciousFlag
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ConfidenceScore = 0.87m,
            FlaggedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<Contracts.StudentRecordDto>(ok.Value);
        Assert.Single(dto.BrowsingSummaries);
        Assert.Single(dto.SuspiciousFlags);
    }

    // AWA-08: "data matches what the student sees in SDA-15, not a separate copy" —
    // published-only, same as SDA-15/PRT-02's own rule.
    [Fact]
    public async Task Awa08_GetProfile_IncludesOnlyPublishedMarks()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student, admin.CollegeId);
        var department = new Department { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = "CS" };
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro to CS" };
        db.Users.AddRange(admin, student);
        db.Departments.Add(department);
        db.Subjects.Add(subject);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        db.InternalMarks.AddRange(
            new InternalMark { Id = Guid.NewGuid(), StudentId = student.Id, SubjectId = subject.Id, Marks = 88, Published = true, PublishedAt = DateTime.UtcNow },
            new InternalMark { Id = Guid.NewGuid(), StudentId = student.Id, SubjectId = subject.Id, Marks = 40, Published = false });
        db.ExternalMarks.Add(new ExternalMark { Id = Guid.NewGuid(), StudentId = student.Id, SubjectId = subject.Id, Grade = "A", Published = true, ApprovedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<Contracts.StudentRecordDto>(ok.Value);
        var internalMark = Assert.Single(dto.InternalMarks);
        Assert.Equal(88, internalMark.Marks);
        var externalMark = Assert.Single(dto.ExternalMarks);
        Assert.Equal("A", externalMark.Grade);
    }

    // AWA-07
    [Fact]
    public async Task Awa07_GetProfile_ReturnsNotFound_ForUnknownUser()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        db.Users.Add(admin);
        db.PermissionGrants.Add(GrantViewAllStudentRecords(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.GetProfile(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }
}

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

public class FeesControllerTests
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

    private static PermissionGrant GrantManageFees(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PermissionCode = "manage_fees",
        Granted = true,
        GrantedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private static FeesController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new FeesController(db, new PermissionService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_ForbidsCallersWithoutManageFeesPermission()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 5000m, new DateOnly(2026, 8, 1)));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-04
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Awa04_CreateLink_RejectsNonPositiveAmount(decimal amount)
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, amount, new DateOnly(2026, 8, 1)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_RejectsWhenTargetIsNotAStudentAccount()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var notAStudent = NewUser(AccountType.Teacher);
        db.Users.AddRange(admin, notAStudent);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(notAStudent.Id, 5000m, new DateOnly(2026, 8, 1)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_RejectsUnknownStudentId()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        db.Users.Add(admin);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(Guid.NewGuid(), 5000m, new DateOnly(2026, 8, 1)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_RejectsAbsurdlyLargeAmount()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 50_000_000m, new DateOnly(2026, 8, 1)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04
    [Theory]
    [InlineData(2020, 1, 1)] // clearly in the past
    public async Task Awa04_CreateLink_RejectsPastDueDate(int year, int month, int day)
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 5000m, new DateOnly(year, month, day)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_RejectsDefaultDueDate()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 5000m, default));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // AWA-04: acceptance-critical — the link must resolve to exactly the amount/period
    // it was generated for, which is why each call mints its own FeeRecord rather than
    // reusing/mutating an existing one.
    [Fact]
    public async Task Awa04_CreateLink_CreatesFeeRecordWithLinkBoundToExactAmountAndDueDate()
    {
        await using var db = NewDb();
        var finance = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(finance, student);
        db.PermissionGrants.Add(GrantManageFees(finance.Id));
        await db.SaveChangesAsync();

        var dueDate = new DateOnly(2026, 8, 1);
        var controller = ControllerAs(db, finance);
        var result = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 7500m, dueDate));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<FeeLinkResponse>(ok.Value);
        Assert.Equal(7500m, response.Amount);
        Assert.Equal(dueDate, response.DueDate);
        Assert.Equal("Pending", response.Status);
        Assert.Contains(response.FeeRecordId.ToString(), response.PaymentLink);

        var stored = await db.FeeRecords.FindAsync(response.FeeRecordId);
        Assert.NotNull(stored);
        Assert.Equal(student.Id, stored!.StudentId);
        Assert.Equal(7500m, stored.Amount);
        Assert.Equal(dueDate, stored.DueDate);
        Assert.Equal(response.PaymentLink, stored.PaymentLink);
    }

    // AWA-04
    [Fact]
    public async Task Awa04_CreateLink_TwoLinksForSameStudent_GetIndependentRecordsAndLinks()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        db.Users.AddRange(admin, student);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var first = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 1000m, new DateOnly(2026, 8, 1)));
        var second = await controller.CreateLink(new CreateFeeLinkRequest(student.Id, 2000m, new DateOnly(2026, 9, 1)));

        var firstResponse = (FeeLinkResponse)((OkObjectResult)first.Result!).Value!;
        var secondResponse = (FeeLinkResponse)((OkObjectResult)second.Result!).Value!;
        Assert.NotEqual(firstResponse.FeeRecordId, secondResponse.FeeRecordId);
        Assert.NotEqual(firstResponse.PaymentLink, secondResponse.PaymentLink);
        Assert.Equal(2, await db.FeeRecords.CountAsync(f => f.StudentId == student.Id));
    }

    // AWA-05: "reminder fires at a configurable number of days before the due date".
    [Fact]
    public async Task Awa05_SendPaymentReminders_ForbidsCallersWithoutManageFeesPermission()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SendPaymentReminders();

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Awa05_SendPaymentReminders_NotifiesParentsOfFeesDueOnTheTargetDate()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        var parent = NewUser(AccountType.Parent);
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        db.Users.AddRange(admin, student, parent);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = parent.Id, StudentId = student.Id });
        db.FeeRecords.Add(new FeeRecord { Id = Guid.NewGuid(), StudentId = student.Id, Amount = 5000m, DueDate = dueDate, Status = FeeStatus.Pending });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.SendPaymentReminders(daysBefore: 7);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SendFeeRemindersResponse>(ok.Value);
        Assert.Equal(1, response.FeesDueSoon);
        Assert.Equal(parent.Id, Assert.Single(response.NotifiedParentIds));
        Assert.Single(await db.Notifications.Where(n => n.RecipientId == parent.Id).ToListAsync());
    }

    [Fact]
    public async Task Awa05_SendPaymentReminders_DoesNotDuplicateAnAlreadySentReminder()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        var parent = NewUser(AccountType.Parent);
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var fee = new FeeRecord { Id = Guid.NewGuid(), StudentId = student.Id, Amount = 5000m, DueDate = dueDate, Status = FeeStatus.Pending };
        db.Users.AddRange(admin, student, parent);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = parent.Id, StudentId = student.Id });
        db.FeeRecords.Add(fee);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        await controller.SendPaymentReminders(daysBefore: 7);
        await controller.SendPaymentReminders(daysBefore: 7);

        Assert.Single(await db.Notifications.Where(n => n.RecipientId == parent.Id).ToListAsync());
    }

    [Fact]
    public async Task Awa05_SendPaymentReminders_IgnoresFeesNotDueOnTheTargetDate()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var student = NewUser(AccountType.Student);
        var parent = NewUser(AccountType.Parent);
        db.Users.AddRange(admin, student, parent);
        db.PermissionGrants.Add(GrantManageFees(admin.Id));
        db.ParentWards.Add(new ParentWard { Id = Guid.NewGuid(), ParentUserId = parent.Id, StudentId = student.Id });
        db.FeeRecords.Add(new FeeRecord { Id = Guid.NewGuid(), StudentId = student.Id, Amount = 5000m, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Status = FeeStatus.Pending });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.SendPaymentReminders(daysBefore: 7);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SendFeeRemindersResponse>(ok.Value);
        Assert.Equal(0, response.FeesDueSoon);
        Assert.Empty(await db.Notifications.ToListAsync());
    }
}

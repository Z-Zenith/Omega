using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests;

public class BrowsingControllerTests
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

    private static BrowsingController ControllerAs(AppDbContext db, User user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new BrowsingController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    // SDA-03
    [Fact]
    public async Task Sda03_GetWhitelist_OnlyReturnsSitesForCallersCollege()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        db.WhitelistSites.AddRange(
            new WhitelistSite { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Url = "https://allowed.example", ApprovedAt = DateTime.UtcNow },
            new WhitelistSite { Id = Guid.NewGuid(), CollegeId = Guid.NewGuid(), Url = "https://otherclass.example", ApprovedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.GetWhitelist();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WhitelistResponse>(ok.Value);
        Assert.Single(response.Sites);
        Assert.Equal("https://allowed.example", response.Sites[0].Url);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_RequestWhitelist_CreatesPendingRequest_ForValidUrl()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.RequestWhitelist(new CreateWhitelistRequestRequest("https://newsite.example"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<WhitelistRequestDto>(ok.Value);
        Assert.Equal("Pending", dto.Status);
        Assert.Single(await db.WhitelistRequests.ToListAsync());
    }

    // SDA-04
    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://wrong-scheme.example")]
    public async Task Sda04_RequestWhitelist_RejectsInvalidUrl(string invalidUrl)
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.RequestWhitelist(new CreateWhitelistRequestRequest(invalidUrl));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_RequestWhitelist_ReturnsConflict_WhenUrlAlreadyWhitelisted()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        db.WhitelistSites.Add(new WhitelistSite { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Url = "https://already.example", ApprovedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.RequestWhitelist(new CreateWhitelistRequestRequest("https://already.example"));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_RequestWhitelist_IsIdempotent_ReturnsExistingPendingRequestForSameCollegeAndUrl()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var first = await controller.RequestWhitelist(new CreateWhitelistRequestRequest("https://dup.example"));
        var second = await controller.RequestWhitelist(new CreateWhitelistRequestRequest("https://dup.example"));

        var firstDto = (WhitelistRequestDto)((OkObjectResult)first.Result!).Value!;
        var secondDto = (WhitelistRequestDto)((OkObjectResult)second.Result!).Value!;
        Assert.Equal(firstDto.Id, secondDto.Id);
        Assert.Single(await db.WhitelistRequests.ToListAsync());
    }

    // SDA-04
    [Fact]
    public async Task Sda04_ListPendingRequests_ForbidsStudents()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.ListPendingRequests();

        Assert.IsType<ForbidResult>(result.Result);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_ListPendingRequests_OnlyReturnsPendingForReviewersCollege()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var sameCollegeStudent = NewUser(AccountType.Student, teacher.CollegeId);
        var otherCollegeStudent = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, sameCollegeStudent, otherCollegeStudent);
        db.WhitelistRequests.AddRange(
            new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://mine.example", RequestedBy = sameCollegeStudent.Id, Status = WhitelistRequestStatus.Pending },
            new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://other-college.example", RequestedBy = otherCollegeStudent.Id, Status = WhitelistRequestStatus.Pending },
            new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://already-approved.example", RequestedBy = sameCollegeStudent.Id, Status = WhitelistRequestStatus.Approved });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ListPendingRequests();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<WhitelistRequestDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("https://mine.example", list[0].Url);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_ApproveWhitelistRequest_ForbidsStudents()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var requester = NewUser(AccountType.Student);
        db.Users.AddRange(student, requester);
        var request = new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://x.example", RequestedBy = requester.Id, Status = WhitelistRequestStatus.Pending };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.ApproveWhitelistRequest(request.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // SDA-04: this is the acceptance-critical path — approval must create an
    // institution-wide site, not something scoped to the requesting student's class.
    [Fact]
    public async Task Sda04_ApproveWhitelistRequest_CreatesSiteForRequestersCollege_AndMarksApproved()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var requester = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, requester);
        var request = new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://approve-me.example", RequestedBy = requester.Id, Status = WhitelistRequestStatus.Pending };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ApproveWhitelistRequest(request.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApproveWhitelistRequestResponse>(ok.Value);
        Assert.Equal("Approved", response.Status);

        var reloaded = await db.WhitelistRequests.FindAsync(request.Id);
        Assert.Equal(WhitelistRequestStatus.Approved, reloaded!.Status);
        Assert.Equal(teacher.Id, reloaded.ReviewedBy);

        var site = Assert.Single(await db.WhitelistSites.Where(s => s.CollegeId == requester.CollegeId).ToListAsync());
        Assert.Equal("https://approve-me.example", site.Url);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_ApproveWhitelistRequest_ReturnsConflict_WhenAlreadyReviewed()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var requester = NewUser(AccountType.Student);
        db.Users.AddRange(teacher, requester);
        var request = new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://x.example", RequestedBy = requester.Id, Status = WhitelistRequestStatus.Approved, ReviewedBy = teacher.Id };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ApproveWhitelistRequest(request.Id);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    // SDA-04
    [Fact]
    public async Task Sda04_ApproveWhitelistRequest_ReturnsNotFound_ForUnknownId()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ApproveWhitelistRequest(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // SDA-04: two pending requests for the same URL both get approved without violating
    // the (college_id, url) unique index on whitelist_sites.
    [Fact]
    public async Task Sda04_ApproveWhitelistRequest_DoesNotDuplicateSite_WhenAlreadyApprovedByAnotherRequest()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        var requesterA = NewUser(AccountType.Student);
        var requesterB = NewUser(AccountType.Student, requesterA.CollegeId);
        db.Users.AddRange(teacher, requesterA, requesterB);
        db.WhitelistSites.Add(new WhitelistSite { Id = Guid.NewGuid(), CollegeId = requesterA.CollegeId, Url = "https://shared.example", ApprovedAt = DateTime.UtcNow });
        var request = new WhitelistRequest { Id = Guid.NewGuid(), Url = "https://shared.example", RequestedBy = requesterB.Id, Status = WhitelistRequestStatus.Pending };
        db.WhitelistRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ApproveWhitelistRequest(request.Id);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(await db.WhitelistSites.Where(s => s.CollegeId == requesterA.CollegeId).ToListAsync());
    }
}

using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BackendApi.Tests.Controllers;

public class CommunityControllerTests
{
    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private class FakePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionCode) => Task.FromResult(false);
        public Task<Guid?> GetDepartmentScopeAsync(Guid userId) => Task.FromResult<Guid?>(null);
    }

    private static IConfiguration ConfigWithAllowedHosts(params string[] hosts)
    {
        var data = hosts.Select((h, i) => new KeyValuePair<string, string?>($"MaterialStorage:AllowedHosts:{i}", h));
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static CommunityController ControllerAs(AppDbContext db, Guid userId, IConfiguration? configuration = null) =>
        new(db, new FakePermissionService(), configuration ?? ConfigWithAllowedHosts("storage.campus.local"))
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

    private static async Task<User> SeedTeacherAsync(AppDbContext db)
    {
        var teacher = new User
        {
            Id = Guid.NewGuid(),
            CollegeId = Guid.NewGuid(),
            Identifier = "teacher-1",
            PasswordHash = "hash",
            FullName = "Teacher",
            IsActive = true,
            AccountType = AccountType.Teacher,
        };
        db.Users.Add(teacher);
        await db.SaveChangesAsync();
        return teacher;
    }

    // #136: FileUrl previously only had to be an absolute http(s) URL — any external domain
    // (e.g. a phishing site) was accepted and later handed straight to Redirect().
    [Fact]
    public async Task UploadMaterial_RejectsUrl_WhenHostNotOnAllowlist()
    {
        using var db = NewDb();
        var teacher = await SeedTeacherAsync(db);
        var controller = ControllerAs(db, teacher.Id);

        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://evil-phish.example/x.pdf", null, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Materials.ToListAsync());
    }

    [Fact]
    public async Task UploadMaterial_Succeeds_WhenHostIsOnAllowlist()
    {
        using var db = NewDb();
        var teacher = await SeedTeacherAsync(db);
        var subjectId = Guid.NewGuid();
        db.Departments.Add(new Department { Id = Guid.NewGuid(), CollegeId = teacher.CollegeId, Name = "CS" });
        db.Subjects.Add(new Subject { Id = subjectId, DepartmentId = db.Departments.Local.First().Id, Code = "CS101", Name = "Intro", TeacherId = teacher.Id });
        await db.SaveChangesAsync();
        var controller = ControllerAs(db, teacher.Id);

        var result = await controller.UploadMaterial(new CreateMaterialRequest("Notes", "https://storage.campus.local/x.pdf", subjectId, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<MaterialDto>(ok.Value);
    }

    [Fact]
    public async Task DownloadMaterial_Redirects_WhenFileHostIsOnAllowlist()
    {
        using var db = NewDb();
        var teacher = await SeedTeacherAsync(db);
        var material = new Material
        {
            Id = Guid.NewGuid(),
            Title = "Notes",
            FileUrl = "https://storage.campus.local/x.pdf",
            UploadedBy = teacher.Id,
            UploadedAt = DateTime.UtcNow,
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var controller = ControllerAs(db, teacher.Id);

        var result = await controller.DownloadMaterial(material.Id);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(material.FileUrl, redirect.Url);
    }

    // #136: defense in depth for a row whose FileUrl predates the allowlist (or reached the
    // table by some other path) — must never redirect to a disallowed host.
    [Fact]
    public async Task DownloadMaterial_RefusesToRedirect_WhenFileHostIsNotOnAllowlist()
    {
        using var db = NewDb();
        var teacher = await SeedTeacherAsync(db);
        var material = new Material
        {
            Id = Guid.NewGuid(),
            Title = "Notes",
            FileUrl = "https://evil-phish.example/x.pdf",
            UploadedBy = teacher.Id,
            UploadedAt = DateTime.UtcNow,
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        var controller = ControllerAs(db, teacher.Id);

        var result = await controller.DownloadMaterial(material.Id);

        Assert.IsNotType<RedirectResult>(result);
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}

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

public class CommunityControllerTests
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

    // Mirrors PermissionService's actual resolution (role_default_permissions), but as a
    // direct grant so tests don't need to seed roles/role-bindings just to exercise the
    // controller's permission check.
    private static PermissionGrant GrantCreateGroup(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PermissionCode = "create_group",
        Granted = true,
        GrantedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    // Mirrors PermissionService's actual resolution, as a direct grant so tests don't need
    // to seed roles/role-bindings just to exercise the controller's permission check.
    private static PermissionGrant GrantViewAllGroups(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PermissionCode = "view_all_groups",
        Granted = true,
        GrantedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
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

    // TWA-05, AWA-12
    [Fact]
    public async Task Twa05_CreateGroup_ForbidsUsersWithoutCreateGroupPermission()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.CreateGroup(new CreateGroupRequest("Chess Club", GroupType.Club, null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // TWA-05
    [Fact]
    public async Task Twa05_CreateGroup_RejectsClassType_ReservedForApi02AutoProvisioning()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        db.PermissionGrants.Add(GrantCreateGroup(teacher.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.CreateGroup(new CreateGroupRequest("Not a real class group", GroupType.Class, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // TWA-05, AWA-12
    [Fact]
    public async Task Twa05_CreateGroup_CreatesGroupAndAddsCreatorAsMember()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        db.PermissionGrants.Add(GrantCreateGroup(teacher.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.CreateGroup(new CreateGroupRequest("Staff Room", GroupType.TeacherOnly, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.Equal("Staff Room", dto.Name);
        Assert.True(await db.GroupMembers.AnyAsync(m => m.GroupId == dto.Id && m.UserId == teacher.Id));
    }

    // TWA-05
    [Fact]
    public async Task Twa05_CreateGroup_RejectsUnknownSectionId()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        db.PermissionGrants.Add(GrantCreateGroup(teacher.Id));
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.CreateGroup(new CreateGroupRequest("Ghost Section Group", GroupType.SubjectSection, Guid.NewGuid()));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-16
    [Fact]
    public async Task Sda16_MyGroups_OnlyReturnsGroupsCallerIsAMemberOf()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var otherStudent = NewUser(AccountType.Student);
        db.Users.AddRange(student, otherStudent);
        var myGroup = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Mine", Type = GroupType.Club };
        var otherGroup = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Not Mine", Type = GroupType.Club };
        db.Groups.AddRange(myGroup, otherGroup);
        db.GroupMembers.AddRange(
            new GroupMember { Id = Guid.NewGuid(), GroupId = myGroup.Id, UserId = student.Id },
            new GroupMember { Id = Guid.NewGuid(), GroupId = otherGroup.Id, UserId = otherStudent.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.MyGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MyGroupsResponse>(ok.Value);
        Assert.Single(response.Groups);
        Assert.Equal("Mine", response.Groups[0].Name);
    }

    // SDA-16: acceptance-critical — "a teacher-only group is not visible to any student".
    [Fact]
    public async Task Sda16_MyGroups_ExcludesTeacherOnlyGroups_ForStudentAccounts_EvenIfSomehowAMember()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var teacherOnlyGroup = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Staff Only", Type = GroupType.TeacherOnly };
        db.Groups.Add(teacherOnlyGroup);
        db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = teacherOnlyGroup.Id, UserId = student.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.MyGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MyGroupsResponse>(ok.Value);
        Assert.Empty(response.Groups);
    }

    // SDA-16
    [Fact]
    public async Task Sda16_CreatePost_ForbidsNonMembers()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.CreatePost(group.Id, new CreatePostRequest("hello"));

        Assert.IsType<ForbidResult>(result.Result);
    }

    // SDA-16
    [Fact]
    public async Task Sda16_CreatePost_RejectsEmptyContent()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = student.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.CreatePost(group.Id, new CreatePostRequest("   "));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // SDA-16
    [Fact]
    public async Task Sda16_CreatePost_CreatesPost_ForGroupMember()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        var group = new Group { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "Club", Type = GroupType.Club };
        db.Groups.Add(group);
        db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = student.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.CreatePost(group.Id, new CreatePostRequest("First post!"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GroupPostDto>(ok.Value);
        Assert.Equal("First post!", dto.Content);
        Assert.Equal(student.Id, dto.AuthorId);
        Assert.Single(await db.GroupPosts.Where(p => p.GroupId == group.Id).ToListAsync());
    }

    // AWA-06
    [Fact]
    public async Task Awa06_AllGroups_ForbidsUsersWithoutViewAllGroupsPermission()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.AllGroups();

        Assert.IsType<ForbidResult>(result.Result);
    }

    // AWA-06: acceptance-critical — "no group is excluded from Admin's view regardless of
    // who created it". Covers a group the admin never joined and one created by someone else.
    [Fact]
    public async Task Awa06_AllGroups_ReturnsEveryGroupInCollege_RegardlessOfCreatorOrMembership()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var teacher = NewUser(AccountType.Teacher, admin.CollegeId);
        db.Users.AddRange(admin, teacher);
        db.PermissionGrants.Add(GrantViewAllGroups(admin.Id));
        var teacherCreated = new Group { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = "Chess Club", Type = GroupType.Club, CreatedBy = teacher.Id };
        var autoProvisioned = new Group { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = "CSE-2A", Type = GroupType.Class, CreatedBy = null };
        db.Groups.AddRange(teacherCreated, autoProvisioned);
        // Admin is not a member of either group — membership must not gate this endpoint.
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.AllGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AllGroupsResponse>(ok.Value);
        Assert.Equal(2, response.Groups.Count);
        Assert.Contains(response.Groups, g => g.Id == teacherCreated.Id && g.CreatedBy == teacher.Id);
        Assert.Contains(response.Groups, g => g.Id == autoProvisioned.Id && g.CreatedBy == null);
    }

    // AWA-06: groups are college-tenant data — another college's groups must not leak in.
    [Fact]
    public async Task Awa06_AllGroups_ExcludesGroupsFromOtherColleges()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        db.Users.Add(admin);
        db.PermissionGrants.Add(GrantViewAllGroups(admin.Id));
        var ownCollegeGroup = new Group { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = "Ours", Type = GroupType.Club };
        var otherCollegeGroup = new Group { Id = Guid.NewGuid(), CollegeId = Guid.NewGuid(), Name = "Theirs", Type = GroupType.Club };
        db.Groups.AddRange(ownCollegeGroup, otherCollegeGroup);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.AllGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AllGroupsResponse>(ok.Value);
        Assert.Single(response.Groups);
        Assert.Equal("Ours", response.Groups[0].Name);
    }
}

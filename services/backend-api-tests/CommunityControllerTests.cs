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

    private static Section NewSection(Guid collegeId, string name = "3rd Year CSE - A")
    {
        var department = new Department { Id = Guid.NewGuid(), CollegeId = collegeId, Name = "CSE" };
        return new Section { Id = Guid.NewGuid(), DepartmentId = department.Id, Department = department, Year = 3, Name = name };
    }

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

    // API-02
    [Fact]
    public async Task Api02_ProvisionClassGroups_ForbidsNonAdmin()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.ProvisionClassGroups();

        Assert.IsType<ForbidResult>(result.Result);
    }

    // API-02: acceptance-critical — "every class has exactly one auto-created group ...
    // and enroll its students".
    [Fact]
    public async Task Api02_ProvisionClassGroups_CreatesOneClassGroupPerSection_AndEnrollsItsStudents()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var section = NewSection(admin.CollegeId);
        var student1 = NewUser(AccountType.Student, admin.CollegeId);
        var student2 = NewUser(AccountType.Student, admin.CollegeId);
        db.Users.AddRange(admin, student1, student2);
        db.Departments.Add(section.Department);
        db.Sections.Add(section);
        db.SectionEnrollments.AddRange(
            new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student1.Id },
            new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student2.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.ProvisionClassGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ProvisionClassGroupsResponse>(ok.Value);
        Assert.Equal(1, response.GroupsCreated);
        Assert.Equal(0, response.GroupsAlreadyExisted);
        Assert.Equal(2, response.StudentsEnrolled);

        var group = await db.Groups.SingleAsync(g => g.SectionId == section.Id);
        Assert.Equal(GroupType.Class, group.Type);
        Assert.Null(group.CreatedBy);
        Assert.Equal(section.Name, group.Name);
        var memberIds = await db.GroupMembers.Where(m => m.GroupId == group.Id).Select(m => m.UserId).ToListAsync();
        Assert.Equal(new[] { student1.Id, student2.Id }.OrderBy(id => id), memberIds.OrderBy(id => id));
    }

    // API-02: running provisioning twice (e.g. a retry) must not create a second class
    // group for the same section or duplicate memberships — only top up newly-enrolled
    // students.
    [Fact]
    public async Task Api02_ProvisionClassGroups_IsIdempotent_TopsUpNewlyEnrolledStudents_WithoutDuplicating()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var section = NewSection(admin.CollegeId);
        var student1 = NewUser(AccountType.Student, admin.CollegeId);
        var student2 = NewUser(AccountType.Student, admin.CollegeId);
        db.Users.AddRange(admin, student1, student2);
        db.Departments.Add(section.Department);
        db.Sections.Add(section);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student1.Id });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var firstResult = await controller.ProvisionClassGroups();
        var firstOk = Assert.IsType<OkObjectResult>(firstResult.Result);
        var firstRun = Assert.IsType<ProvisionClassGroupsResponse>(firstOk.Value);
        Assert.Equal(1, firstRun.GroupsCreated);
        Assert.Equal(1, firstRun.StudentsEnrolled);

        // A second student enrolls after the first provisioning run.
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student2.Id });
        await db.SaveChangesAsync();

        var secondResult = await controller.ProvisionClassGroups();
        var secondOk = Assert.IsType<OkObjectResult>(secondResult.Result);
        var secondRun = Assert.IsType<ProvisionClassGroupsResponse>(secondOk.Value);
        Assert.Equal(0, secondRun.GroupsCreated);
        Assert.Equal(1, secondRun.GroupsAlreadyExisted);
        Assert.Equal(1, secondRun.StudentsEnrolled); // only the newly-enrolled student, not student1 again

        Assert.Single(await db.Groups.Where(g => g.SectionId == section.Id).ToListAsync());
        var group = await db.Groups.SingleAsync(g => g.SectionId == section.Id);
        Assert.Equal(2, await db.GroupMembers.Where(m => m.GroupId == group.Id).CountAsync());
    }

    // API-02: multi-tenant boundary — an Admin must not provision (or even see) another
    // college's sections.
    [Fact]
    public async Task Api02_ProvisionClassGroups_DoesNotTouchSectionsInAnotherCollege()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var otherCollegeSection = NewSection(Guid.NewGuid(), "Other College Section");
        db.Users.Add(admin);
        db.Departments.Add(otherCollegeSection.Department);
        db.Sections.Add(otherCollegeSection);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, admin);
        var result = await controller.ProvisionClassGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ProvisionClassGroupsResponse>(ok.Value);
        Assert.Equal(0, response.GroupsCreated);
        Assert.False(await db.Groups.AnyAsync(g => g.SectionId == otherCollegeSection.Id));
    }

    // API-02: no DB-level unique constraint stops two Class groups from ever existing for the
    // same section (e.g. a race between two concurrent provisioning calls) — this proves the
    // endpoint degrades to picking one deterministically instead of throwing on the resulting
    // duplicate key, so one bad section can't take down provisioning for the whole college.
    [Fact]
    public async Task Api02_ProvisionClassGroups_ToleratesPreExistingDuplicateClassGroups_ForSameSection()
    {
        await using var db = NewDb();
        var admin = NewUser(AccountType.AdminTier);
        var section = NewSection(admin.CollegeId);
        var student = NewUser(AccountType.Student, admin.CollegeId);
        var olderDuplicate = new Group { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = section.Name, Type = GroupType.Class, SectionId = section.Id };
        var newerDuplicate = new Group { Id = Guid.NewGuid(), CollegeId = admin.CollegeId, Name = section.Name, Type = GroupType.Class, SectionId = section.Id };
        db.Users.AddRange(admin, student);
        db.Departments.Add(section.Department);
        db.Sections.Add(section);
        db.Groups.AddRange(olderDuplicate, newerDuplicate);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student.Id });
        await db.SaveChangesAsync();

        var result = await ControllerAs(db, admin).ProvisionClassGroups();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ProvisionClassGroupsResponse>(ok.Value);
        Assert.Equal(0, response.GroupsCreated);
        Assert.Equal(1, response.GroupsAlreadyExisted);
        // Both duplicates still exist (cleaning them up is a data-repair job, not this
        // endpoint's concern) — but only one was topped up with the student.
        Assert.Equal(2, await db.Groups.CountAsync(g => g.SectionId == section.Id));
        Assert.Equal(1, await db.GroupMembers.CountAsync(m => m.UserId == student.Id));
    }
}

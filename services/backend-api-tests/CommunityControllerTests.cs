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

    // SDA-18: "every enrolled subject has a non-empty course-info and teacher-info entry".
    [Fact]
    public async Task Sda18_MySubjects_ReturnsCourseAndTeacherInfo_ForEnrolledSubjectsOnly()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        var department = new Department { Id = Guid.NewGuid(), CollegeId = student.CollegeId, Name = "CS" };
        var enrolledSection = new Section { Id = Guid.NewGuid(), DepartmentId = department.Id, Year = 3, Name = "3rd Year CSE - A" };
        var otherSection = new Section { Id = Guid.NewGuid(), DepartmentId = department.Id, Year = 3, Name = "3rd Year CSE - B" };
        var enrolledSubject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS101", Name = "Intro to CS", TeacherId = teacher.Id };
        var otherSubject = new Subject { Id = Guid.NewGuid(), DepartmentId = department.Id, Code = "CS202", Name = "Not Mine", TeacherId = teacher.Id };
        db.Users.AddRange(student, teacher);
        db.Departments.Add(department);
        db.Sections.AddRange(enrolledSection, otherSection);
        db.Subjects.AddRange(enrolledSubject, otherSubject);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = enrolledSection.Id, StudentId = student.Id });
        db.TimetableSlots.AddRange(
            new TimetableSlot { Id = Guid.NewGuid(), SectionId = enrolledSection.Id, SubjectId = enrolledSubject.Id, TeacherId = teacher.Id, DayOfWeek = 1, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) },
            new TimetableSlot { Id = Guid.NewGuid(), SectionId = otherSection.Id, SubjectId = otherSubject.Id, TeacherId = teacher.Id, DayOfWeek = 1, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.MySubjects();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var courses = Assert.IsType<List<CourseInfoDto>>(ok.Value);
        var course = Assert.Single(courses);
        Assert.Equal("Intro to CS", course.Name);
        Assert.Equal(teacher.FullName, course.TeacherName);
    }

    // SDA-17: "feedback is attributable to the course/teacher it was submitted against".
    [Fact]
    public async Task Sda17_SubmitTeacherFeedback_ForbidsNonStudents()
    {
        await using var db = NewDb();
        var teacher = NewUser(AccountType.Teacher);
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var result = await controller.SubmitTeacherFeedback(new SubmitTeacherFeedbackRequest(teacher.Id, 5, "Great"));

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Sda17_SubmitTeacherFeedback_RejectsRatingOutsideOneToFive(int rating)
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitTeacherFeedback(new SubmitTeacherFeedbackRequest(teacher.Id, rating, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Sda17_SubmitTeacherFeedback_RejectsUnknownTeacher()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitTeacherFeedback(new SubmitTeacherFeedbackRequest(Guid.NewGuid(), 4, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Sda17_SubmitTeacherFeedback_CreatesFeedback_AttributableToTheTeacher()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var result = await controller.SubmitTeacherFeedback(new SubmitTeacherFeedbackRequest(teacher.Id, 4, "Clear explanations"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TeacherFeedbackDto>(ok.Value);
        Assert.Equal(teacher.Id, dto.TeacherId);
        Assert.Equal(student.Id, dto.StudentId);
        Assert.Equal(4, dto.Rating);
        Assert.Single(await db.TeacherFeedbacks.Where(f => f.TeacherId == teacher.Id).ToListAsync());
    }
}

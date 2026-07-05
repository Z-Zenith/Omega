using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests;

public class MessagingControllerTests
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

    // DMS-01
    [Fact]
    public async Task Dms01_CreateThread_CreatesNewThread_ForValidStudentTeacherPair()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, teacher);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.CreateThread(new CreateThreadRequest(student.Id, teacher.Id));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<MessageThreadResponse>(created.Value);
        Assert.Equal(student.Id, response.StudentId);
        Assert.Equal(teacher.Id, response.TeacherId);
        Assert.Single(await db.MessageThreads.ToListAsync());
    }

    // DMS-01 — the DB's unique (student_id, teacher_id) index means at most one
    // thread ever exists for a pair; the endpoint must be idempotent, not error.
    [Fact]
    public async Task Dms01_CreateThread_IsIdempotent_ReturnsExistingThreadOnDuplicateCall()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(student, teacher);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var first = await controller.CreateThread(new CreateThreadRequest(student.Id, teacher.Id));
        var second = await controller.CreateThread(new CreateThreadRequest(student.Id, teacher.Id));

        var firstId = ((MessageThreadResponse)((CreatedAtActionResult)first.Result!).Value!).Id;
        var secondOk = Assert.IsType<OkObjectResult>(second.Result);
        var secondResponse = Assert.IsType<MessageThreadResponse>(secondOk.Value);
        Assert.Equal(firstId, secondResponse.Id);
        Assert.Single(await db.MessageThreads.ToListAsync());
    }

    // DMS-01
    [Fact]
    public async Task Dms01_CreateThread_RejectsWhenStudentIdIsNotAStudentAccount()
    {
        await using var db = NewDb();
        var notAStudent = NewUser(AccountType.Teacher);
        var teacher = NewUser(AccountType.Teacher);
        db.Users.AddRange(notAStudent, teacher);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.CreateThread(new CreateThreadRequest(notAStudent.Id, teacher.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_CreateThread_RejectsWhenTeacherIdIsNotATeacherAccount()
    {
        await using var db = NewDb();
        var student = NewUser(AccountType.Student);
        var notATeacher = NewUser(AccountType.Student);
        db.Users.AddRange(student, notATeacher);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.CreateThread(new CreateThreadRequest(student.Id, notATeacher.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_CreateThread_RejectsSameUserForBothRoles()
    {
        await using var db = NewDb();
        var user = NewUser(AccountType.Student);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.CreateThread(new CreateThreadRequest(user.Id, user.Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_SendMessage_PersistsMessage_WhenSenderIsThreadParticipant()
    {
        await using var db = NewDb();
        var (thread, student, _) = await SeedThreadAsync(db);

        var controller = new MessagingController(db);
        var result = await controller.SendMessage(thread.Id, new SendMessageRequest(student.Id, "hello"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<MessageResponse>(created.Value);
        Assert.Equal("hello", response.Content);
        Assert.Equal(student.Id, response.SenderId);
        Assert.Single(await db.Messages.ToListAsync());
    }

    // DMS-01
    [Fact]
    public async Task Dms01_SendMessage_ReturnsNotFound_WhenThreadDoesNotExist()
    {
        await using var db = NewDb();
        var controller = new MessagingController(db);

        var result = await controller.SendMessage(Guid.NewGuid(), new SendMessageRequest(Guid.NewGuid(), "hi"));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_SendMessage_RejectsSenderNotInThread()
    {
        await using var db = NewDb();
        var (thread, _, _) = await SeedThreadAsync(db);
        var outsider = NewUser(AccountType.Student);
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.SendMessage(thread.Id, new SendMessageRequest(outsider.Id, "hi"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // DMS-01
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Dms01_SendMessage_RejectsEmptyContent(string content)
    {
        await using var db = NewDb();
        var (thread, student, _) = await SeedThreadAsync(db);

        var controller = new MessagingController(db);
        var result = await controller.SendMessage(thread.Id, new SendMessageRequest(student.Id, content));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_ListMessages_ReturnsMessagesInChronologicalOrder()
    {
        await using var db = NewDb();
        var (thread, student, teacher) = await SeedThreadAsync(db);
        var now = DateTime.UtcNow;
        db.Messages.AddRange(
            new Message { Id = Guid.NewGuid(), ThreadId = thread.Id, SenderId = teacher.Id, Content = "second", SentAt = now.AddMinutes(1) },
            new Message { Id = Guid.NewGuid(), ThreadId = thread.Id, SenderId = student.Id, Content = "first", SentAt = now }
        );
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.ListMessages(thread.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var messages = Assert.IsType<List<MessageResponse>>(ok.Value);
        Assert.Equal(["first", "second"], messages.Select(m => m.Content));
    }

    // DMS-01
    [Fact]
    public async Task Dms01_ListMessages_ReturnsNotFound_WhenThreadDoesNotExist()
    {
        await using var db = NewDb();
        var controller = new MessagingController(db);

        var result = await controller.ListMessages(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_ListThreads_ReturnsOnlyThreadsForGivenUser_AsStudentOrTeacher()
    {
        await using var db = NewDb();
        var (myThread, student, _) = await SeedThreadAsync(db);
        var (otherThread, _, _) = await SeedThreadAsync(db);

        var controller = new MessagingController(db);
        var result = await controller.ListThreads(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var threads = Assert.IsType<List<ThreadSummaryResponse>>(ok.Value);
        Assert.Single(threads);
        Assert.Equal(myThread.Id, threads[0].Id);
        Assert.DoesNotContain(threads, t => t.Id == otherThread.Id);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_ListThreads_IncludesLastMessage()
    {
        await using var db = NewDb();
        var (thread, student, teacher) = await SeedThreadAsync(db);
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            SenderId = teacher.Id,
            Content = "latest",
            SentAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = new MessagingController(db);
        var result = await controller.ListThreads(student.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var threads = Assert.IsType<List<ThreadSummaryResponse>>(ok.Value);
        Assert.Equal("latest", threads[0].LastMessage?.Content);
    }

    // DMS-01
    [Fact]
    public async Task Dms01_ListThreads_RejectsMissingUserId()
    {
        await using var db = NewDb();
        var controller = new MessagingController(db);

        var result = await controller.ListThreads(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static async Task<(MessageThread Thread, User Student, User Teacher)> SeedThreadAsync(AppDbContext db)
    {
        var student = NewUser(AccountType.Student);
        var teacher = NewUser(AccountType.Teacher);
        var thread = new MessageThread
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            TeacherId = teacher.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.AddRange(student, teacher);
        db.MessageThreads.Add(thread);
        await db.SaveChangesAsync();
        return (thread, student, teacher);
    }
}

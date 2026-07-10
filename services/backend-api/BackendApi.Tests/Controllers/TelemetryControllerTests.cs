using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Controllers;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Tests.Controllers;

// SDA-25
public class TelemetryControllerTests
{
    // Stub the AI Services HTTP dependency so tests don't need a live service running,
    // while still exercising the real HttpClient plumbing (headers, serialization).
    private class StubHandler(HttpStatusCode status, object? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body);
            }
            return Task.FromResult(response);
        }
    }

    private class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("http://ai-services.test") };
    }

    private static AppDbContext NewDb() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewStudent() => new()
    {
        Id = Guid.NewGuid(),
        CollegeId = Guid.NewGuid(),
        Identifier = $"student-{Guid.NewGuid():N}",
        PasswordHash = "hash",
        FullName = "Test Student",
        AccountType = AccountType.Student,
        IsActive = true,
    };

    private static TelemetryController ControllerAs(AppDbContext db, User user, HttpMessageHandler? handler = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "TestAuth"));
        return new TelemetryController(db, new StubHttpClientFactory(handler ?? new StubHandler(HttpStatusCode.OK, new { flags = Array.Empty<object>() })), NullLogger<TelemetryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
        };
    }

    // Seeds a timetable slot that is in session at `now` for `student`, so
    // ClassSessionLookup.FindOrStartActiveSessionAsync resolves a real active session.
    private static async Task SeedActiveClassSessionAsync(AppDbContext db, User student, DateTime now)
    {
        var collegeId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var teacher = new User { Id = Guid.NewGuid(), CollegeId = collegeId, Identifier = "t1", PasswordHash = "hash", FullName = "Teacher", AccountType = AccountType.Teacher, IsActive = true };
        var section = new Section { Id = Guid.NewGuid(), DepartmentId = departmentId, Year = 1, Name = "Sec A" };
        var subject = new Subject { Id = Guid.NewGuid(), DepartmentId = departmentId, Code = "C1", Name = "Course" };
        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = (int)now.DayOfWeek,
            StartTime = TimeOnly.FromDateTime(now.AddMinutes(-10)),
            EndTime = TimeOnly.FromDateTime(now.AddMinutes(50)),
        };
        db.Users.Add(teacher);
        db.Sections.Add(section);
        db.Subjects.Add(subject);
        db.TimetableSlots.Add(slot);
        db.SectionEnrollments.Add(new SectionEnrollment { Id = Guid.NewGuid(), SectionId = section.Id, StudentId = student.Id });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SubmitUsage_RejectsEventsWithNoActiveWindow()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", null, null, DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await db.UsageTelemetries.CountAsync());
    }

    [Fact]
    public async Task SubmitUsage_ForbidsNonStudentCallers()
    {
        await using var db = NewDb();
        var teacher = new User { Id = Guid.NewGuid(), CollegeId = Guid.NewGuid(), Identifier = "t1", PasswordHash = "hash", FullName = "Teacher", AccountType = AccountType.Teacher, IsActive = true };
        db.Users.Add(teacher);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, teacher);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", null, Guid.NewGuid(), DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task SubmitUsage_AcceptsEventsTaggedWithAKnownAssignmentId()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();
        var assignmentId = Guid.NewGuid();

        var controller = ControllerAs(db, student);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", new Dictionary<string, object>(), assignmentId, DateTime.UtcNow),
            new TelemetryEventRequest("paste", new Dictionary<string, object> { ["char_count"] = 200 }, assignmentId, DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SubmitTelemetryResponse>(ok.Value);
        Assert.Equal(2, response.EventsRecorded);
        var records = await db.UsageTelemetries.Where(t => t.StudentId == student.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(assignmentId, r.AssignmentId));
    }

    [Fact]
    public async Task SubmitUsage_ResolvesClassSessionServerSideWhenNoAssignmentIdGiven()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();
        // The controller resolves the active session against the real server clock
        // (DateTime.UtcNow), not the client-supplied RecordedAt — seed the slot to be
        // in session right now rather than at a fixed historical date.
        var now = DateTime.UtcNow;
        await SeedActiveClassSessionAsync(db, student, now);

        var controller = ControllerAs(db, student);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", null, null, now),
        ]);
        var result = await controller.SubmitUsage(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SubmitTelemetryResponse>(ok.Value);
        Assert.Equal(1, response.EventsRecorded);

        var record = Assert.Single(await db.UsageTelemetries.ToListAsync());
        Assert.NotNull(record.ClassSessionId);
        Assert.Single(await db.ClassSessions.ToListAsync());
    }

    [Fact]
    public async Task SubmitUsage_RejectsWhenNoAssignmentIdAndNoActiveClassSession()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, student);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", null, null, DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await db.UsageTelemetries.CountAsync());
    }

    [Fact]
    public async Task SubmitUsage_PersistsFlagsReturnedByAiServices()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();
        var assignmentId = Guid.NewGuid();

        var handler = new StubHandler(HttpStatusCode.OK, new
        {
            flags = new[]
            {
                new { student_id = student.Id.ToString(), class_session_id = (string?)null, assignment_id = assignmentId.ToString(), confidence_score = 0.85, reasons = new[] { "uniform_event_timing" } },
            },
        });
        var controller = ControllerAs(db, student, handler);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("keystroke", new Dictionary<string, object>(), assignmentId, DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SubmitTelemetryResponse>(ok.Value);
        Assert.Equal(1, response.FlagsRaised);

        var flag = Assert.Single(await db.SuspiciousFlags.ToListAsync());
        Assert.Equal(student.Id, flag.StudentId);
        Assert.Equal(assignmentId, flag.AssignmentId);
        Assert.Equal(0.85m, flag.ConfidenceScore);
    }

    [Fact]
    public async Task SubmitUsage_StillPersistsTelemetryWhenAiServicesIsUnreachable()
    {
        await using var db = NewDb();
        var student = NewStudent();
        db.Users.Add(student);
        await db.SaveChangesAsync();

        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, null);
        var controller = ControllerAs(db, student, handler);
        var request = new SubmitTelemetryRequest([
            new TelemetryEventRequest("window_blur", null, Guid.NewGuid(), DateTime.UtcNow),
        ]);
        var result = await controller.SubmitUsage(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SubmitTelemetryResponse>(ok.Value);
        Assert.Equal(1, response.EventsRecorded);
        Assert.Equal(0, response.FlagsRaised);
        Assert.Equal(1, await db.UsageTelemetries.CountAsync());
    }
}

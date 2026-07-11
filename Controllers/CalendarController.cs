using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class CalendarController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // TWA-15, AWA-11
    [HttpPost("events")]
    public async Task<ActionResult<EventDto>> CreateEvent(CreateEventRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "create_event"))
        {
            return Forbid();
        }

        var creator = await db.Users.FindAsync(userId);
        if (creator is null)
        {
            return Unauthorized();
        }

        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            CollegeId = creator.CollegeId,
            Title = request.Title,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedBy = userId,
            RestrictedYears = request.RestrictedYears,
            RestrictedDepartments = request.RestrictedDepartments,
        };
        db.Events.Add(newEvent);
        await db.SaveChangesAsync();

        return Ok(new EventDto(newEvent.Id, newEvent.Title, newEvent.StartTime, newEvent.EndTime, false));
    }

    // SDA-20
    [HttpGet("events")]
    public async Task<ActionResult<List<EventDto>>> ListEvents()
    {
        var (student, section) = await CurrentStudentSectionAsync();
        if (student is null || section is null)
        {
            return Forbid();
        }

        var registeredEventIds = await db.EventRegistrations
            .Where(r => r.StudentId == student.Id)
            .Select(r => r.EventId)
            .ToListAsync();

        var events = await EligibleEventsQuery(student.CollegeId, section).ToListAsync();
        return Ok(events.Select(e => new EventDto(e.Id, e.Title, e.StartTime, e.EndTime, registeredEventIds.Contains(e.Id))).ToList());
    }

    // SDA-20
    [HttpPost("events/{id}/register")]
    public async Task<ActionResult<RegisterForEventResponse>> RegisterForEvent(Guid id)
    {
        var (student, section) = await CurrentStudentSectionAsync();
        if (student is null || section is null)
        {
            return Forbid();
        }

        var isEligible = await EligibleEventsQuery(student.CollegeId, section).AnyAsync(e => e.Id == id);
        if (!isEligible)
        {
            return Forbid();
        }

        var existing = await db.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.StudentId == student.Id);
        if (existing is not null)
        {
            return Ok(new RegisterForEventResponse(existing.EventId, existing.StudentId, existing.RegisteredAt));
        }

        var registration = new EventRegistration { Id = Guid.NewGuid(), EventId = id, StudentId = student.Id };
        db.EventRegistrations.Add(registration);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // #94: two concurrent registration requests for the same (event, student) can both
            // pass the existence check above before either commits — the losing request hits
            // the unique constraint on (event_id, student_id) instead of a clean response.
            // Mirrors BrowsingController.ApproveWhitelistRequest's identical race: drop the
            // speculative insert and return the row the other request actually persisted.
            db.Entry(registration).State = EntityState.Detached;
            registration = await db.EventRegistrations.SingleAsync(r => r.EventId == id && r.StudentId == student.Id);
        }

        return Ok(new RegisterForEventResponse(registration.EventId, registration.StudentId, registration.RegisteredAt));
    }

    // SDA-14
    [HttpGet("calendar/mine")]
    public async Task<ActionResult<MyCalendarResponse>> MyCalendar()
    {
        var (student, section) = await CurrentStudentSectionAsync();
        if (student is null)
        {
            return Forbid();
        }

        var items = new List<CalendarItemDto>();

        var registeredEventIds = await db.EventRegistrations
            .Where(r => r.StudentId == student.Id)
            .Select(r => r.EventId)
            .ToListAsync();

        if (section is not null)
        {
            var events = await EligibleEventsQuery(student.CollegeId, section).ToListAsync();
            items.AddRange(events.Select(e => new CalendarItemDto(
                "college_event", e.Id, e.Title, e.StartTime, e.EndTime,
                registeredEventIds.Contains(e.Id) ? "registered=true" : null)));
        }

        var todos = await db.Todos.Where(t => t.StudentId == student.Id).ToListAsync();
        items.AddRange(todos.Select(t => new CalendarItemDto(
            "todo", t.Id, t.Title, t.DueDate ?? DateTime.MinValue, t.DueDate ?? DateTime.MinValue,
            t.Completed ? "completed=true" : null)));

        var customEntries = await db.CustomCalendarEntries.Where(c => c.StudentId == student.Id).ToListAsync();
        items.AddRange(customEntries.Select(c =>
        {
            var start = c.EntryDate.ToDateTime(TimeOnly.MinValue);
            return new CalendarItemDto("custom_entry", c.Id, c.Title, start, start, null);
        }));

        if (section is not null)
        {
            items.AddRange(await ThisWeeksClassSessionsAsync(section.Id));
        }

        return Ok(new MyCalendarResponse(items));
    }

    private async Task<List<CalendarItemDto>> ThisWeeksClassSessionsAsync(Guid sectionId)
    {
        var slots = await db.TimetableSlots
            .Where(s => s.SectionId == sectionId)
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .ToListAsync();

        // TODO: DateTime.UtcNow is used for "today" here; this can shift the
        // weekly boundary for non-UTC colleges near midnight. Needs a
        // College.TimeZone column (schema change - requires sign-off) to fix
        // properly. Tracked as a follow-up, not blocking this PR.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monday = today.AddDays(-((int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1));

        return slots.Select(s =>
        {
            var sessionDate = monday.AddDays(s.DayOfWeek - 1);
            return new CalendarItemDto(
                "class_session",
                s.Id,
                s.Subject.Name,
                sessionDate.ToDateTime(s.StartTime),
                sessionDate.ToDateTime(s.EndTime),
                $"teacher={s.Teacher.FullName};room={s.Room}");
        }).ToList();
    }

    private IQueryable<Event> EligibleEventsQuery(Guid collegeId, Section section) =>
        db.Events.Where(e => e.CollegeId == collegeId &&
            (e.RestrictedYears == null || e.RestrictedYears.Contains(section.Year)) &&
            (e.RestrictedDepartments == null || e.RestrictedDepartments.Contains(section.DepartmentId)));

    private async Task<(User? Student, Section? Section)> CurrentStudentSectionAsync()
    {
        var userId = CurrentUserId();
        var student = await db.Users.FindAsync(userId);
        if (student is null || student.AccountType != AccountType.Student)
        {
            return (null, null);
        }

        var enrollment = await db.SectionEnrollments
            .Include(e => e.Section)
            .FirstOrDefaultAsync(e => e.StudentId == userId);
        return (student, enrollment?.Section);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

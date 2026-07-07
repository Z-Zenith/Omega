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
public class TimetableController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // 5 weekdays x 6 one-hour periods starting 9am. MVP scheduling heuristic, not a
    // constraint solver — enough to satisfy AWA-01/AWA-02's stated acceptance criteria.
    private static readonly (int Day, TimeOnly Start, TimeOnly End)[] Grid =
        Enumerable.Range(1, 5)
            .SelectMany(day => Enumerable.Range(0, 6).Select(period =>
                (day, new TimeOnly(9 + period, 0), new TimeOnly(10 + period, 0))))
            .ToArray();

    // AWA-01, AWA-02
    [HttpPost("timetable/generate")]
    public async Task<ActionResult<List<TimetableSlotDto>>> Generate(GenerateTimetableRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "create_timetable"))
        {
            return Forbid();
        }

        var departmentScope = await permissions.GetDepartmentScopeAsync(userId) ?? request.DepartmentId;

        var assignmentsQuery = db.TeacherSectionAssignments
            .Include(a => a.Section)
            .Include(a => a.Subject)
            .Include(a => a.Teacher)
            .AsQueryable();
        if (departmentScope is not null)
        {
            assignmentsQuery = assignmentsQuery.Where(a => a.Section.DepartmentId == departmentScope);
        }
        var assignments = await assignmentsQuery.ToListAsync();

        var sectionIds = assignments.Select(a => a.SectionId).Distinct().ToList();
        var existingSlots = await db.TimetableSlots
            .Where(s => sectionIds.Contains(s.SectionId))
            .ToListAsync();

        var negativeFeedbackPairs = (await db.SectionFeedbacks
                .Where(f => sectionIds.Contains(f.SectionId) && f.Rating <= 2)
                .Select(f => new { f.TeacherId, f.SectionId })
                .ToListAsync())
            .Select(f => (f.TeacherId, f.SectionId))
            .ToHashSet();

        // Manually-edited slots persist through regeneration (AWA-03) — remove only the
        // auto-generated ones for sections in scope before recomputing.
        var toRemove = existingSlots.Where(s => !s.ManuallyEdited).ToList();
        db.TimetableSlots.RemoveRange(toRemove);

        // An assignment already satisfied by a manually-edited slot shouldn't get a second,
        // auto-generated slot alongside it. Keyed by (section, subject) rather than also
        // including teacher, because PatchSlot can reassign a manual slot's teacher — if the
        // key included the original teacher, that reassignment would make the original
        // assignment look uncovered and spawn a duplicate auto-generated slot for it.
        var manuallyCoveredAssignments = existingSlots
            .Where(s => s.ManuallyEdited)
            .Select(s => (s.SectionId, s.SubjectId))
            .ToHashSet();

        var occupiedCells = existingSlots
            .Where(s => s.ManuallyEdited)
            .Select(s => (s.SectionId, s.DayOfWeek, s.StartTime))
            .ToHashSet();

        // Teacher-conflict checks must not be limited to sectionIds in scope: a teacher may
        // also teach sections outside this department, and slots there aren't being touched
        // by this generation run (department-scoped regeneration otherwise risks double-
        // booking that teacher into an out-of-scope commitment).
        var relevantTeacherIds = assignments.Select(a => a.TeacherId).Distinct().ToList();
        var teacherBusy = (await db.TimetableSlots
                .Where(s => relevantTeacherIds.Contains(s.TeacherId) && (s.ManuallyEdited || !sectionIds.Contains(s.SectionId)))
                .Select(s => new { s.TeacherId, s.DayOfWeek, s.StartTime })
                .ToListAsync())
            .Select(s => (s.TeacherId, s.DayOfWeek, s.StartTime))
            .ToHashSet();

        var newSlots = new List<TimetableSlot>();
        foreach (var assignment in assignments)
        {
            if (negativeFeedbackPairs.Contains((assignment.TeacherId, assignment.SectionId)))
            {
                continue;
            }
            if (manuallyCoveredAssignments.Contains((assignment.SectionId, assignment.SubjectId)))
            {
                continue;
            }

            var found = false;
            var cell = (Day: 0, Start: default(TimeOnly), End: default(TimeOnly));
            foreach (var candidate in Grid)
            {
                if (occupiedCells.Contains((assignment.SectionId, candidate.Day, candidate.Start)) ||
                    teacherBusy.Contains((assignment.TeacherId, candidate.Day, candidate.Start)))
                {
                    continue;
                }
                cell = candidate;
                found = true;
                break;
            }
            if (!found)
            {
                continue;
            }

            var slot = new TimetableSlot
            {
                Id = Guid.NewGuid(),
                SectionId = assignment.SectionId,
                SubjectId = assignment.SubjectId,
                TeacherId = assignment.TeacherId,
                DayOfWeek = cell.Day,
                StartTime = cell.Start,
                EndTime = cell.End,
                ManuallyEdited = false,
            };
            newSlots.Add(slot);
            occupiedCells.Add((slot.SectionId, slot.DayOfWeek, slot.StartTime));
            teacherBusy.Add((slot.TeacherId, slot.DayOfWeek, slot.StartTime));
        }

        db.TimetableSlots.AddRange(newSlots);
        await db.SaveChangesAsync();

        var result = await db.TimetableSlots
            .Where(s => sectionIds.Contains(s.SectionId))
            .Include(s => s.Section)
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();
        return Ok(result.Select(ToDto).ToList());
    }

    // AWA-03
    [HttpPatch("timetable/slots/{id}")]
    public async Task<ActionResult<TimetableSlotDto>> PatchSlot(Guid id, PatchSlotRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "create_timetable"))
        {
            return Forbid();
        }

        var slot = await db.TimetableSlots
            .Include(s => s.Section)
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null)
        {
            return NotFound();
        }

        var departmentScope = await permissions.GetDepartmentScopeAsync(userId);
        if (departmentScope is not null && slot.Section.DepartmentId != departmentScope)
        {
            return Forbid();
        }

        if (request.TeacherId is { } teacherId)
        {
            slot.TeacherId = teacherId;
            slot.Teacher = await db.Users.FindAsync(teacherId) ?? slot.Teacher;
        }
        if (request.DayOfWeek is { } dayOfWeek) slot.DayOfWeek = dayOfWeek;
        if (request.StartTime is { } startTime) slot.StartTime = startTime;
        if (request.EndTime is { } endTime) slot.EndTime = endTime;
        if (request.Room is not null) slot.Room = request.Room;
        slot.ManuallyEdited = true;

        await db.SaveChangesAsync();
        return Ok(ToDto(slot));
    }

    // TWA-10
    [HttpGet("timetable/mine")]
    public async Task<ActionResult<List<TimetableSlotDto>>> Mine()
    {
        var userId = CurrentUserId();
        var slots = await db.TimetableSlots
            .Where(s => s.TeacherId == userId)
            .Include(s => s.Section)
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();
        return Ok(slots.Select(ToDto).ToList());
    }

    // TWA-13
    [HttpPost("timetable/change-requests")]
    public async Task<ActionResult<ChangeRequestDto>> CreateChangeRequest(CreateChangeRequestRequest request)
    {
        var userId = CurrentUserId();
        var changeRequest = new TimetableChangeRequest
        {
            Id = Guid.NewGuid(),
            TeacherId = userId,
            Description = request.Description,
            Status = "pending",
        };
        db.TimetableChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        return Ok(new ChangeRequestDto(changeRequest.Id, changeRequest.Description, changeRequest.Status, changeRequest.RequestedAt));
    }

    // TWA-08 — roster for the attendance-marking form; scoped the same way as marking
    // itself, so a teacher can only see the section they're assigned to teach this slot.
    [HttpGet("timetable/slots/{id}/roster")]
    public async Task<ActionResult<List<RosterStudentDto>>> Roster(Guid id)
    {
        var userId = CurrentUserId();

        var slot = await db.TimetableSlots.FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null)
        {
            return NotFound();
        }
        if (slot.TeacherId != userId)
        {
            return Forbid();
        }

        var students = await db.SectionEnrollments
            .Where(e => e.SectionId == slot.SectionId)
            .Include(e => e.Student)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new RosterStudentDto(e.StudentId, e.Student.FullName))
            .ToListAsync();
        return Ok(students);
    }

    // TWA-12 — a teacher rates a section they've taught. Written in the exact shape
    // Generate() above reads for AWA-02 (feedback-based teacher exclusion): one row
    // per submission in section_feedback, keyed by (teacher_id, section_id, rating).
    // No dedicated permission code exists for this in the RBAC catalog (adding one
    // would be an authz-model change requiring the contract-change sign-off per
    // CLAUDE.md); instead this endpoint verifies the caller actually has a
    // TeacherSectionAssignment for the section, which both confirms they're a teacher
    // and that they taught that specific section.
    [HttpPost("timetable/sections/{sectionId}/feedback")]
    public async Task<ActionResult<SectionFeedbackDto>> SubmitSectionFeedback(Guid sectionId, SubmitSectionFeedbackRequest request)
    {
        var userId = CurrentUserId();

        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { error = "rating must be between 1 and 5" });
        }

        var taughtSection = await db.TeacherSectionAssignments
            .AnyAsync(a => a.TeacherId == userId && a.SectionId == sectionId);
        if (!taughtSection)
        {
            return Forbid();
        }

        var section = await db.Sections.FindAsync(sectionId);
        if (section is null)
        {
            return NotFound();
        }

        var feedback = new SectionFeedback
        {
            Id = Guid.NewGuid(),
            TeacherId = userId,
            SectionId = sectionId,
            Rating = request.Rating,
            Comments = request.Comments,
            SubmittedAt = DateTime.UtcNow,
        };
        db.SectionFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        return Ok(new SectionFeedbackDto(feedback.Id, feedback.SectionId, section.Name, feedback.Rating, feedback.Comments, feedback.SubmittedAt));
    }

    // TWA-08
    [HttpPost("attendance")]
    public async Task<ActionResult<MarkAttendanceResponse>> MarkAttendance(MarkAttendanceRequest request)
    {
        var userId = CurrentUserId();

        var caller = await db.Users.FindAsync(userId);
        if (caller is null || caller.AccountType != AccountType.Teacher)
        {
            return Forbid();
        }

        var slot = await db.TimetableSlots.FirstOrDefaultAsync(s => s.Id == request.TimetableSlotId);
        if (slot is null)
        {
            return NotFound();
        }

        // Scoped to the teacher's own assigned section: only the teacher timetabled for
        // this slot may mark attendance for its sessions.
        if (slot.TeacherId != userId)
        {
            return Forbid();
        }

        var sessionDate = request.SessionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var session = await db.ClassSessions
            .FirstOrDefaultAsync(s => s.TimetableSlotId == slot.Id && s.SessionDate == sessionDate);
        if (session is null)
        {
            session = new ClassSession
            {
                Id = Guid.NewGuid(),
                TimetableSlotId = slot.Id,
                SessionDate = sessionDate,
                ActualTeacherId = userId,
            };
            db.ClassSessions.Add(session);
        }

        var sectionId = slot.SectionId;
        var enrolledStudentIds = await db.SectionEnrollments
            .Where(e => e.SectionId == sectionId)
            .Select(e => e.StudentId)
            .ToListAsync();
        if (enrolledStudentIds.Count == 0)
        {
            return BadRequest(new { error = "no_enrolled_students", message = "This section has no enrolled students." });
        }

        var entries = request.Entries ?? [];
        var providedIds = entries.Select(e => e.StudentId).ToList();
        if (providedIds.Count != providedIds.Distinct().Count())
        {
            return BadRequest(new { error = "duplicate_student", message = "Each student may only have one attendance entry." });
        }

        var enrolledSet = enrolledStudentIds.ToHashSet();
        var unknown = providedIds.Where(id => !enrolledSet.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            return BadRequest(new { error = "unknown_student", message = "One or more students are not enrolled in this section.", studentIds = unknown });
        }

        // AC: every enrolled student must have a status set after marking completes.
        var providedSet = providedIds.ToHashSet();
        var missing = enrolledStudentIds.Where(id => !providedSet.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            return BadRequest(new { error = "incomplete_attendance", message = "Every enrolled student must have an attendance status.", studentIds = missing });
        }

        var statusByStudent = new Dictionary<Guid, AttendanceStatus>();
        foreach (var entry in entries)
        {
            if (!Enum.TryParse<AttendanceStatus>(entry.Status, ignoreCase: true, out var status))
            {
                return BadRequest(new { error = "invalid_status", message = $"'{entry.Status}' is not a valid attendance status." });
            }
            statusByStudent[entry.StudentId] = status;
        }

        var existingRecords = await db.AttendanceRecords
            .Where(r => r.ClassSessionId == session.Id)
            .ToListAsync();
        var existingByStudent = existingRecords.ToDictionary(r => r.StudentId);

        var now = DateTime.UtcNow;
        foreach (var (studentId, status) in statusByStudent)
        {
            if (existingByStudent.TryGetValue(studentId, out var record))
            {
                record.Status = status;
                record.MarkedAt = now;
                record.MarkedBy = userId;
            }
            else
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = session.Id,
                    StudentId = studentId,
                    Status = status,
                    MarkedAt = now,
                    MarkedBy = userId,
                });
            }
        }

        await db.SaveChangesAsync();

        var studentNames = await db.Users
            .Where(u => enrolledSet.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var records = statusByStudent
            .Select(kv => new MarkedAttendanceDto(kv.Key, studentNames.GetValueOrDefault(kv.Key, ""), kv.Value.ToString()))
            .OrderBy(r => r.StudentName)
            .ToList();

        return Ok(new MarkAttendanceResponse(session.Id, session.SessionDate, sectionId, records));
    }

    // TWA-09
    [HttpGet("attendance/alerts")]
    public IActionResult AttendanceAlerts() => StatusCode(501, new { feature = "TWA-09", status = "not_implemented" });

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static TimetableSlotDto ToDto(TimetableSlot s) => new(
        s.Id, s.DayOfWeek, s.StartTime, s.EndTime,
        s.SectionId, s.Section.Name,
        s.SubjectId, s.Subject.Name,
        s.TeacherId, s.Teacher.FullName,
        s.Room, s.ManuallyEdited);
}

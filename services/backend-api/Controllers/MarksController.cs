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
[Route("api/v1/marks")]
public class MarksController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    private const string AddExternalMarksPermission = "add_external_marks";

    // TWA-16. Internal marks are direct-publish (no approval gate, unlike TWA-17/TWA-20) —
    // publishing just requires the teacher's own explicit action via the Publish flag.
    [HttpPost("internal")]
    [Authorize]
    public async Task<ActionResult<InternalMarkRecordDto>> CreateInternal(CreateInternalMarkRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "add_internal_marks"))
        {
            return Forbid();
        }

        if (request.Marks < 0)
        {
            return BadRequest(new { error = "invalid_marks", message = "Marks must not be negative." });
        }

        // Scope to the caller's own section/subject: the teacher must be assigned to teach
        // this subject to a section the student is actually enrolled in.
        var teacherSectionIds = await db.TeacherSectionAssignments
            .Where(a => a.TeacherId == userId && a.SubjectId == request.SubjectId)
            .Select(a => a.SectionId)
            .ToListAsync();
        if (teacherSectionIds.Count == 0)
        {
            return Forbid();
        }

        var studentEnrolled = await db.SectionEnrollments
            .AnyAsync(e => e.StudentId == request.StudentId && teacherSectionIds.Contains(e.SectionId));
        if (!studentEnrolled)
        {
            return Forbid();
        }

        if (request.AssignmentId is { } assignmentId)
        {
            var assignment = await db.Assignments.FindAsync(assignmentId);
            if (assignment is null || assignment.SubjectId != request.SubjectId)
            {
                return BadRequest(new { error = "invalid_assignment", message = "Assignment does not belong to this subject." });
            }
        }

        // Upsert: re-submitting for the same student/subject/assignment updates the existing
        // row instead of creating a duplicate. An already-published mark's Published state is
        // never cleared by a Publish=false request — only an explicit publish action changes it.
        var mark = await db.InternalMarks.FirstOrDefaultAsync(m =>
            m.StudentId == request.StudentId &&
            m.SubjectId == request.SubjectId &&
            m.AssignmentId == request.AssignmentId);

        if (mark is null)
        {
            mark = new Data.Entities.InternalMark
            {
                Id = Guid.NewGuid(),
                StudentId = request.StudentId,
                SubjectId = request.SubjectId,
                AssignmentId = request.AssignmentId,
            };
            db.InternalMarks.Add(mark);
        }

        mark.Marks = request.Marks;
        if (request.Publish)
        {
            mark.Published = true;
            mark.PublishedAt = DateTime.UtcNow;
            mark.PublishedBy = userId;
        }

        await db.SaveChangesAsync();

        return Ok(new InternalMarkRecordDto(mark.Id, mark.StudentId, mark.SubjectId, mark.AssignmentId, mark.Marks, mark.Published, mark.PublishedAt));
    }

    // TWA-16 support endpoint — lets the marks-entry screen list the students the caller
    // may actually enter marks for, instead of requiring student ids to be typed blind.
    [HttpGet("internal/roster")]
    [Authorize]
    public async Task<ActionResult<List<InternalMarksRosterEntryDto>>> InternalRoster([FromQuery] Guid subjectId, [FromQuery] Guid? assignmentId)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "add_internal_marks"))
        {
            return Forbid();
        }

        var teacherSectionIds = await db.TeacherSectionAssignments
            .Where(a => a.TeacherId == userId && a.SubjectId == subjectId)
            .Select(a => a.SectionId)
            .ToListAsync();
        if (teacherSectionIds.Count == 0)
        {
            return Forbid();
        }

        var students = await db.SectionEnrollments
            .Where(e => teacherSectionIds.Contains(e.SectionId))
            .Select(e => e.Student)
            .Distinct()
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var existingMarks = await db.InternalMarks
            .Where(m => m.SubjectId == subjectId && m.AssignmentId == assignmentId)
            .ToListAsync();
        var marksByStudent = existingMarks.ToDictionary(m => m.StudentId);

        var roster = students.Select(s => marksByStudent.TryGetValue(s.Id, out var mark)
                ? new InternalMarksRosterEntryDto(s.Id, s.FullName, mark.Marks, mark.Published, mark.PublishedAt)
                : new InternalMarksRosterEntryDto(s.Id, s.FullName, null, false, null))
            .ToList();

        return Ok(roster);
    }

    // TWA-17. Gated by an active, non-expired add_external_marks PermissionGrant — this
    // permission has no role-default bundle (see db/init/02_seed_roles_and_permissions.sql),
    // so HasPermissionAsync effectively only returns true for a live, unexpired grant row.
    // Submissions land here unapproved; TWA-20 is the only path that flips Approved/Published,
    // so a submitted mark is never directly visible to the student/parent until then.
    [HttpPost("external")]
    [Authorize]
    public async Task<ActionResult<ExternalMarkSubmissionResponse>> CreateExternal(CreateExternalMarkRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, AddExternalMarksPermission))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Grade))
        {
            return BadRequest(new { error = "grade_required", message = "Grade must not be empty." });
        }

        var student = await db.Users.FindAsync(request.StudentId);
        if (student is null || student.AccountType != AccountType.Student)
        {
            return BadRequest(new { error = "unknown_student", message = "No student exists with that id." });
        }

        var subject = await db.Subjects.FindAsync(request.SubjectId);
        if (subject is null)
        {
            return BadRequest(new { error = "unknown_subject", message = "No subject exists with that id." });
        }

        var externalMark = new ExternalMark
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            Grade = request.Grade.Trim(),
            SubmittedBy = userId,
            SubmittedAt = DateTime.UtcNow,
            Approved = false,
            Published = false,
        };
        db.ExternalMarks.Add(externalMark);
        await db.SaveChangesAsync();

        return Ok(new ExternalMarkSubmissionResponse(
            externalMark.Id,
            externalMark.StudentId,
            externalMark.SubjectId,
            externalMark.Grade,
            "pending_approval",
            externalMark.SubmittedAt));
    }

    // TWA-17 — read-only check the teacher-web UI polls to decide whether the "submit
    // external marks" option should render at all. Reads permission_grants directly
    // rather than depending on AWA-13's grant-management endpoints (owned/implemented
    // separately), matching the same "is there a live, unexpired grant" rule enforced above.
    [HttpGet("external/permission-status")]
    [Authorize]
    public async Task<ActionResult<ExternalMarksPermissionStatusResponse>> ExternalMarksPermissionStatus()
    {
        var userId = CurrentUserId();
        var now = DateTime.UtcNow;

        var activeGrant = await db.PermissionGrants
            .Where(g => g.UserId == userId && g.PermissionCode == AddExternalMarksPermission)
            .Where(g => g.ExpiresAt == null || g.ExpiresAt > now)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        var granted = activeGrant is { Granted: true };
        return Ok(new ExternalMarksPermissionStatusResponse(granted, granted ? activeGrant!.ExpiresAt : null));
    }

    // TWA-20
    [HttpPost("external/{id}/approve")]
    public IActionResult ApproveExternal(Guid id) => StatusCode(501, new { feature = "TWA-20", status = "not_implemented" });

    // SDA-15 — published marks only, mirrors PRT-02's filtering logic scoped to the logged-in student.
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<MyMarksResponse>> Mine()
    {
        var studentId = CurrentUserId();

        var internalMarks = await PublishedMarksQueries.GetPublishedInternalMarksAsync(db, studentId);
        var externalMarks = await PublishedMarksQueries.GetPublishedExternalMarksAsync(db, studentId);

        return Ok(new MyMarksResponse(internalMarks, externalMarks));
    }

    // PRT-02 — attendance + published marks only, matching SDA-15's publish rule.
    [HttpGet("ward/{studentId}")]
    [Authorize]
    [ServiceFilter(typeof(WardAccessFilter))]
    public async Task<ActionResult<WardRecordResponse>> Ward(Guid studentId)
    {
        var student = await db.Users.FindAsync(studentId);
        if (student is null)
        {
            return NotFound();
        }

        var attendance = await db.AttendanceRecords
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.ClassSession.SessionDate)
            .Select(a => new AttendanceRecordDto(
                a.ClassSession.SessionDate,
                a.ClassSession.TimetableSlot.SubjectId,
                a.ClassSession.TimetableSlot.Subject.Name,
                a.Status.ToString()))
            .ToListAsync();

        var internalMarks = await PublishedMarksQueries.GetPublishedInternalMarksAsync(db, studentId);
        var externalMarks = await PublishedMarksQueries.GetPublishedExternalMarksAsync(db, studentId);

        return Ok(new WardRecordResponse(student.Id, student.FullName, attendance, internalMarks, externalMarks));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

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

    // TWA-16
    [HttpPost("internal")]
    public IActionResult CreateInternal() => StatusCode(501, new { feature = "TWA-16", status = "not_implemented" });

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

    // SDA-15
    [HttpGet("mine")]
    public IActionResult Mine() => StatusCode(501, new { feature = "SDA-15", status = "not_implemented" });

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

        var internalMarks = await db.InternalMarks
            .Where(m => m.StudentId == studentId && m.Published)
            .Select(m => new InternalMarkDto(m.SubjectId, m.Subject.Name, m.Marks, m.PublishedAt))
            .ToListAsync();

        var externalMarks = await db.ExternalMarks
            .Where(m => m.StudentId == studentId && m.Published)
            .Select(m => new ExternalMarkDto(m.SubjectId, m.Subject.Name, m.Grade, m.ApprovedAt))
            .ToListAsync();

        return Ok(new WardRecordResponse(student.Id, student.FullName, attendance, internalMarks, externalMarks));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

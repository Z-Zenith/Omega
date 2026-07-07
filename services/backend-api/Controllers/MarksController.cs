using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/marks")]
public class MarksController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // TWA-16
    [HttpPost("internal")]
    public IActionResult CreateInternal() => StatusCode(501, new { feature = "TWA-16", status = "not_implemented" });

    // TWA-17
    [HttpPost("external")]
    public IActionResult CreateExternal() => StatusCode(501, new { feature = "TWA-17", status = "not_implemented" });

    // TWA-20 — approval queue for holders of the approve_external_marks permission.
    // HoD grants are department-scoped (architecture doc Section 9), so a HoD only sees
    // pending marks for subjects in their own department; a global grant (e.g. Admin via
    // a PermissionGrant) sees everything still pending.
    [HttpGet("external/pending")]
    [Authorize]
    public async Task<ActionResult<List<PendingExternalMarkDto>>> PendingExternal()
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "approve_external_marks"))
        {
            return Forbid();
        }

        var departmentScope = await permissions.GetDepartmentScopeAsync(userId);

        var query = db.ExternalMarks.Where(m => !m.Approved);
        if (departmentScope is not null)
        {
            query = query.Where(m => m.Subject.DepartmentId == departmentScope);
        }

        var pending = await query
            .OrderBy(m => m.SubmittedAt)
            .Select(m => new PendingExternalMarkDto(
                m.Id,
                m.StudentId,
                m.Student.FullName,
                m.SubjectId,
                m.Subject.Name,
                m.Grade,
                m.SubmittedBy,
                m.SubmittedByNavigation.FullName,
                m.SubmittedAt))
            .ToListAsync();

        return Ok(pending);
    }

    // TWA-20 — marks stay invisible to the student (SDA-15 / PRT-02) until a holder of
    // approve_external_marks approves them here. External marks have no separate
    // direct-publish step like TWA-16's internal marks, so approval both flips `approved`
    // and `published` in one atomic update — that's the entire visibility gate for this flow.
    [HttpPost("external/{id}/approve")]
    [Authorize]
    public async Task<ActionResult<ApproveExternalMarkResponse>> ApproveExternal(Guid id)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "approve_external_marks"))
        {
            return Forbid();
        }

        var mark = await db.ExternalMarks.FindAsync(id);
        if (mark is null)
        {
            return NotFound();
        }

        var departmentScope = await permissions.GetDepartmentScopeAsync(userId);
        if (departmentScope is not null)
        {
            var subjectDepartmentId = await db.Subjects
                .Where(s => s.Id == mark.SubjectId)
                .Select(s => s.DepartmentId)
                .FirstOrDefaultAsync();
            if (subjectDepartmentId != departmentScope)
            {
                return Forbid();
            }
        }

        var approvedAt = DateTime.UtcNow;
        // Atomic conditional update closes the check-then-act race between concurrent
        // approve requests for the same row — only the request that actually flips
        // `approved` runs the state transition; a losing concurrent request sees 0 rows.
        var rowsUpdated = await db.ExternalMarks
            .Where(m => m.Id == id && !m.Approved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Approved, true)
                .SetProperty(m => m.ApprovedBy, userId)
                .SetProperty(m => m.ApprovedAt, approvedAt)
                .SetProperty(m => m.Published, true));

        if (rowsUpdated == 0)
        {
            return Conflict(new { error = "already_approved", message = "This external mark has already been approved." });
        }

        return Ok(new ApproveExternalMarkResponse(id, userId, approvedAt));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

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
}

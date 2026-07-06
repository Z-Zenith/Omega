using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/marks")]
public class MarksController(AppDbContext db) : ControllerBase
{
    // TWA-16
    [HttpPost("internal")]
    public IActionResult CreateInternal() => StatusCode(501, new { feature = "TWA-16", status = "not_implemented" });

    // TWA-17
    [HttpPost("external")]
    public IActionResult CreateExternal() => StatusCode(501, new { feature = "TWA-17", status = "not_implemented" });

    // TWA-20
    [HttpPost("external/{id}/approve")]
    public IActionResult ApproveExternal(Guid id) => StatusCode(501, new { feature = "TWA-20", status = "not_implemented" });

    // SDA-15
    [HttpGet("mine")]
    public IActionResult Mine() => StatusCode(501, new { feature = "SDA-15", status = "not_implemented" });

    // PRT-02 — attendance + published marks only, matching SDA-15's publish rule.
    [HttpGet("ward/{studentId}")]
    [Authorize]
    public async Task<ActionResult<WardRecordResponse>> Ward(Guid studentId)
    {
        if (await ParentWardAccess.GetAuthorizedParentIdAsync(db, User, studentId) is null)
        {
            return Forbid();
        }

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

using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// TWA-11: a teacher submits a report about a section or student, routed to Admin.
// There's no dedicated permission code for this (see db/init/02_seed_roles_and_permissions.sql's
// anti-drift note — the permission catalog mirrors architecture doc Section 9 verbatim and
// isn't extended casually), so access is gated directly off role_bindings, mirroring how
// PermissionService.GetDepartmentScopeAsync checks the "hod" role code directly.
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] TeacherRoleCodes = ["lecturer", "hod"];

    // TWA-11
    [HttpPost]
    public async Task<ActionResult<TeacherReportDto>> Create(CreateReportRequest request)
    {
        var userId = CurrentUserId();
        if (!await HasAnyRoleAsync(userId, TeacherRoleCodes))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "content is required" });
        }
        if (request.SectionId is null && request.StudentId is null)
        {
            return BadRequest(new { error = "sectionId or studentId is required" });
        }

        var report = new TeacherReport
        {
            Id = Guid.NewGuid(),
            TeacherId = userId,
            SectionId = request.SectionId,
            StudentId = request.StudentId,
            Content = request.Content,
            SubmittedAt = DateTime.UtcNow,
        };
        db.TeacherReports.Add(report);
        await db.SaveChangesAsync();

        var saved = await db.TeacherReports
            .Include(r => r.Teacher)
            .Include(r => r.Section)
            .Include(r => r.Student)
            .FirstAsync(r => r.Id == report.Id);
        return Ok(ToDto(saved));
    }

    // TWA-11 — Admin inbox
    [HttpGet]
    public async Task<ActionResult<List<TeacherReportDto>>> List()
    {
        var userId = CurrentUserId();
        if (!await HasAnyRoleAsync(userId, ["admin"]))
        {
            return Forbid();
        }

        var reports = await db.TeacherReports
            .Include(r => r.Teacher)
            .Include(r => r.Section)
            .Include(r => r.Student)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
        return Ok(reports.Select(ToDto).ToList());
    }

    private async Task<bool> HasAnyRoleAsync(Guid userId, IReadOnlyCollection<string> roleCodes) =>
        await db.RoleBindings.AnyAsync(b => b.UserId == userId && roleCodes.Contains(b.RoleCode));

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static TeacherReportDto ToDto(TeacherReport r) => new(
        r.Id, r.TeacherId, r.Teacher.FullName,
        r.SectionId, r.Section?.Name,
        r.StudentId, r.Student?.FullName,
        r.Content, r.SubmittedAt);
}

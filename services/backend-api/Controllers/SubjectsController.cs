using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/subjects")]
[Authorize]
public class SubjectsController(AppDbContext db) : ControllerBase
{
    // SDA-18: course + teacher info for every subject taught to a section the caller is
    // enrolled in. There's no independent section-curriculum table in this schema — a
    // subject only "belongs" to a section via a TeacherSectionAssignment row, so that's
    // also this endpoint's definition of "enrolled subject" (a subject a section is meant
    // to take but hasn't yet been staffed with a teacher has no representation here; fixing
    // that would need a schema change, out of SDA-18's scope).
    //
    // Teacher preference: Subject.TeacherId is treated as the canonical assigned teacher
    // elsewhere (AssignmentsController.cs gates assignment-creation on it and attributes
    // assignments to it), so this endpoint prefers the same field for consistency — a
    // student shouldn't be told a different teacher here than the one assignments for the
    // same subject come from. It falls back to the TeacherSectionAssignment row's own
    // TeacherId only when Subject.TeacherId is null, since that field is nullable but the
    // acceptance criterion requires a non-empty teacher-info entry for every result here.
    [HttpGet("mine")]
    public async Task<ActionResult<List<MySubjectDto>>> Mine()
    {
        var studentId = CurrentUserId();

        var sectionIds = await db.SectionEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.SectionId)
            .ToListAsync();

        var assignments = await db.TeacherSectionAssignments
            .Where(a => sectionIds.Contains(a.SectionId))
            .Select(a => new
            {
                a.SubjectId,
                a.Subject.Code,
                a.Subject.Name,
                SubjectTeacherId = a.Subject.TeacherId,
                SubjectTeacherName = a.Subject.Teacher != null ? a.Subject.Teacher.FullName : null,
                AssignmentTeacherId = a.TeacherId,
                AssignmentTeacherName = a.Teacher.FullName,
            })
            .Distinct()
            .ToListAsync();

        var subjects = assignments
            .Select(a => new MySubjectDto(
                a.SubjectId,
                a.Code,
                a.Name,
                a.SubjectTeacherId ?? a.AssignmentTeacherId,
                a.SubjectTeacherName ?? a.AssignmentTeacherName))
            .ToList();

        return Ok(subjects);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

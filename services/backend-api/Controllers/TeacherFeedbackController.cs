using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/teacher-feedback")]
[Authorize]
public class TeacherFeedbackController(AppDbContext db) : ControllerBase
{
    // SDA-17 support endpoint — lets the feedback screen list the teachers/subjects the
    // caller can give feedback about, instead of requiring a teacher id to be typed blind
    // (same reasoning as MarksController's internal/roster). Subject is shown for context
    // only — it isn't persisted with the feedback itself, see SubmitFeedback below.
    [HttpGet("my-teachers")]
    public async Task<ActionResult<List<MyTeacherDto>>> MyTeachers()
    {
        var studentId = CurrentUserId();

        var sectionIds = await db.SectionEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.SectionId)
            .ToListAsync();

        var teachers = await db.TeacherSectionAssignments
            .Where(a => sectionIds.Contains(a.SectionId))
            .Select(a => new MyTeacherDto(a.TeacherId, a.Teacher.FullName, a.SubjectId, a.Subject.Name))
            .Distinct()
            .ToListAsync();

        return Ok(teachers);
    }

    // SDA-17. The teacher_feedback table (docs/campus-platform-db-api-schema.md §1.12)
    // has no subject_id column, only teacher_id — adding one would be a DB schema change
    // requiring the contract-change sign-off (CLAUDE.md), so "attributable to the
    // course/teacher" is satisfied here at the teacher level: a student can only submit
    // feedback about a teacher who actually teaches one of their enrolled sections,
    // verified via the same SectionEnrollment -> TeacherSectionAssignment join
    // MyTeachers() uses (mirrors TWA-12's SubmitSectionFeedback pattern, which verifies
    // the caller taught the section rather than adding an authz-model change).
    [HttpPost]
    public async Task<ActionResult<TeacherFeedbackDto>> SubmitFeedback(SubmitTeacherFeedbackRequest request)
    {
        var studentId = CurrentUserId();

        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { error = "rating must be between 1 and 5" });
        }

        var sectionIds = await db.SectionEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.SectionId)
            .ToListAsync();

        var isTaughtByTeacher = await db.TeacherSectionAssignments
            .AnyAsync(a => a.TeacherId == request.TeacherId && sectionIds.Contains(a.SectionId));
        if (!isTaughtByTeacher)
        {
            return Forbid();
        }

        var feedback = new TeacherFeedback
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TeacherId = request.TeacherId,
            Rating = request.Rating,
            Comments = request.Comments,
            SubmittedAt = DateTime.UtcNow,
        };
        db.TeacherFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        return Ok(new TeacherFeedbackDto(feedback.Id, feedback.TeacherId, feedback.Rating, feedback.Comments, feedback.SubmittedAt));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

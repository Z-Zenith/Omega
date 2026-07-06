using System.Security.Claims;
using System.Text.Json;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// Track 2 surface (assignments, AI services) — stubbed here only to keep the shared
// API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
[Authorize]
public class AssignmentsController(AppDbContext db) : ControllerBase
{
    // TWA-07. Gated by "caller teaches this subject" rather than a permission code — no
    // "create_assignment" code exists in the seeded catalog, and adding one is an
    // OpenFGA/permission-catalog change needing separate sign-off.
    [HttpPost("assignments")]
    public async Task<ActionResult<AssignmentDto>> Create(CreateAssignmentRequest request)
    {
        var userId = CurrentUserId();
        var caller = await db.Users.FindAsync(userId);
        if (caller is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "title_required", message = "Assignment title must not be empty." });
        }
        // An assignment with no due date cannot be published (TWA-07 acceptance
        // criterion) — DueDate is non-nullable, so the only way a caller can "omit" it
        // is by leaving it at the JSON default, which we reject explicitly here.
        if (request.DueDate == default)
        {
            return BadRequest(new { error = "due_date_required", message = "Assignment must have a due date." });
        }
        if (request.SubmissionWindowStart >= request.SubmissionWindowEnd)
        {
            return BadRequest(new { error = "invalid_window", message = "Submission window start must be before its end." });
        }
        if (request.TypeSpecificSettings is not null && !IsValidJson(request.TypeSpecificSettings))
        {
            return BadRequest(new { error = "invalid_settings", message = "typeSpecificSettings must be valid JSON." });
        }

        var subject = await db.Subjects.FindAsync(request.SubjectId);
        if (subject is null)
        {
            return BadRequest(new { error = "unknown_subject", message = "No subject exists with that id." });
        }
        if (caller.AccountType is not AccountType.AdminTier && subject.TeacherId != caller.Id)
        {
            return Forbid();
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            SubjectId = request.SubjectId,
            TeacherId = subject.TeacherId ?? caller.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            Type = request.Type,
            DueDate = request.DueDate,
            SubmissionWindowStart = request.SubmissionWindowStart,
            SubmissionWindowEnd = request.SubmissionWindowEnd,
            TypeSpecificSettings = request.TypeSpecificSettings,
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return Ok(ToDto(assignment));
    }

    // SDA-10. SDA-11 (auto-submit on exit) is Track 1 — a different trigger path
    // (student-desktop exit detection) that would call this same table with
    // IsAutosubmitted=true, not something this endpoint decides on its own.
    [HttpPost("assignments/{id}/submissions")]
    public async Task<ActionResult<SubmissionDto>> Submit(Guid id, SubmitAssignmentRequest request)
    {
        var userId = CurrentUserId();
        var student = await db.Users.FindAsync(userId);
        if (student is null)
        {
            return Unauthorized();
        }
        if (student.AccountType != AccountType.Student)
        {
            return Forbid();
        }

        var assignment = await db.Assignments.FindAsync(id);
        if (assignment is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.ContentUrl))
        {
            return BadRequest(new { error = "content_required", message = "Submission content must not be empty." });
        }
        // Acceptance criterion: "a quiz-type assignment cannot be submitted as a file
        // upload" — generalized to any format mismatch, not just the quiz/file case.
        if (request.SubmissionFormat != assignment.Type)
        {
            return BadRequest(new
            {
                error = "format_mismatch",
                message = $"This assignment requires a {assignment.Type} submission, not {request.SubmissionFormat}.",
            });
        }

        var submittedAt = DateTime.UtcNow;
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = id,
            StudentId = userId,
            ContentUrl = request.ContentUrl.Trim(),
            SubmittedAt = submittedAt,
            IsLate = submittedAt > assignment.DueDate,
            IsAutosubmitted = false,
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        return Ok(ToSubmissionDto(submission));
    }

    [HttpGet("submissions/{id}/plagiarism-report")]
    public IActionResult PlagiarismReport(Guid id) => StatusCode(501, new { feature = "AIS-02", status = "not_implemented" });

    [HttpGet("assignments/{id}/copy-check")]
    public IActionResult CopyCheck(Guid id) => StatusCode(501, new { feature = "AIS-03", status = "not_implemented" });

    [HttpGet("submissions/{id}/ai-detection")]
    public IActionResult AiDetection(Guid id) => StatusCode(501, new { feature = "AIS-05", status = "not_implemented" });

    [HttpGet("submissions/{id}/autograde-suggestion")]
    public IActionResult AutogradeSuggestion(Guid id) => StatusCode(501, new { feature = "AIS-04", status = "not_implemented" });

    [HttpPost("submissions/{id}/grade")]
    public IActionResult Grade(Guid id) => StatusCode(501, new { feature = "grade-confirm", status = "not_implemented" });

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AssignmentDto ToDto(Assignment a) => new(
        a.Id, a.SubjectId, a.Title, a.Description, a.Type.ToString(),
        a.DueDate, a.SubmissionWindowStart, a.SubmissionWindowEnd, a.TypeSpecificSettings);

    private static SubmissionDto ToSubmissionDto(Submission s) => new(
        s.Id, s.AssignmentId, s.StudentId, s.ContentUrl, s.SubmittedAt, s.IsLate, s.IsAutosubmitted);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// Track 2 surface (community/groups/materials) — stubbed here only to keep the shared
// API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
[Authorize]
public class CommunityController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // TWA-05, AWA-12. The auto-provisioned class group (API-02) is not created through
    // this endpoint — GroupType.Class is reserved for that automation, so a caller can't
    // hand-create a second "class group" for a section.
    [HttpPost("groups")]
    public async Task<ActionResult<GroupDto>> CreateGroup(CreateGroupRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "create_group"))
        {
            return Forbid();
        }

        if (request.Type == GroupType.Class)
        {
            return BadRequest(new { error = "reserved_group_type", message = "Class groups are auto-provisioned (API-02), not created directly." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "name_required", message = "Group name must not be empty." });
        }

        if (request.SectionId is not null && !await db.Sections.AnyAsync(s => s.Id == request.SectionId))
        {
            return BadRequest(new { error = "unknown_section", message = "No section exists with that id." });
        }

        var creator = await db.Users.FindAsync(userId);
        if (creator is null)
        {
            return Unauthorized();
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            CollegeId = creator.CollegeId,
            Name = request.Name.Trim(),
            Type = request.Type,
            SectionId = request.SectionId,
            CreatedBy = userId,
        };
        db.Groups.Add(group);
        db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = userId });
        await db.SaveChangesAsync();

        return Ok(ToDto(group));
    }

    // SDA-16
    [HttpGet("groups/mine")]
    public async Task<ActionResult<MyGroupsResponse>> MyGroups()
    {
        var userId = CurrentUserId();
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var groups = await db.GroupMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.Group)
            // Defense-in-depth: a teacher-only group must never be visible to a student,
            // even if a membership row existed for one by mistake.
            .Where(g => user.AccountType != AccountType.Student || g.Type != GroupType.TeacherOnly)
            .ToListAsync();

        return Ok(new MyGroupsResponse(groups.Select(ToDto).ToList()));
    }

    // SDA-16
    [HttpPost("groups/{id}/posts")]
    public async Task<ActionResult<GroupPostDto>> CreatePost(Guid id, CreatePostRequest request)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "content_required", message = "Post content must not be empty." });
        }

        var isMember = await db.GroupMembers.AnyAsync(m => m.GroupId == id && m.UserId == userId);
        if (!isMember)
        {
            return Forbid();
        }

        var post = new GroupPost
        {
            Id = Guid.NewGuid(),
            GroupId = id,
            AuthorId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.GroupPosts.Add(post);
        await db.SaveChangesAsync();

        return Ok(new GroupPostDto(post.Id, post.GroupId, post.AuthorId, post.Content, post.CreatedAt));
    }

    // TWA-06. Gated by AccountType rather than a permission code — no "upload_material"
    // code exists in the seeded catalog, and adding one is an OpenFGA/permission-catalog
    // contract change that needs separate sign-off (CLAUDE.md contract-change rule).
    [HttpPost("materials")]
    public async Task<ActionResult<MaterialDto>> UploadMaterial(CreateMaterialRequest request)
    {
        var uploader = await CurrentUserAsync();
        if (uploader is null)
        {
            return Unauthorized();
        }
        if (uploader.AccountType is not (AccountType.Teacher or AccountType.AdminTier))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "title_required", message = "Material title must not be empty." });
        }
        if (!TryValidateUrl(request.FileUrl))
        {
            return BadRequest(new { error = "invalid_url", message = "fileUrl must be an absolute http:// or https:// address." });
        }
        if (request.SubjectId is null && request.GroupId is null)
        {
            return BadRequest(new { error = "target_required", message = "Attach material to a subject, a group, or both." });
        }
        if (request.SubjectId is not null && !await db.Subjects.AnyAsync(s => s.Id == request.SubjectId))
        {
            return BadRequest(new { error = "unknown_subject", message = "No subject exists with that id." });
        }
        if (request.GroupId is not null && !await db.Groups.AnyAsync(g => g.Id == request.GroupId))
        {
            return BadRequest(new { error = "unknown_group", message = "No group exists with that id." });
        }

        var material = new Material
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            FileUrl = request.FileUrl.Trim(),
            SubjectId = request.SubjectId,
            GroupId = request.GroupId,
            UploadedBy = uploader.Id,
            UploadedAt = DateTime.UtcNow,
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        return Ok(ToDto(material));
    }

    // API-03: this endpoint is the authorization gate in front of file_url — it redirects
    // rather than proxying bytes, since file_url already points at wherever the file is
    // actually hosted. That keeps "byte-identical regardless of which app requested it"
    // trivially true (every app is handed the same underlying file).
    [HttpGet("materials/{id}/download")]
    public async Task<IActionResult> DownloadMaterial(Guid id)
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }

        var material = await db.Materials.FindAsync(id);
        if (material is null)
        {
            return NotFound();
        }

        if (!await CanViewMaterialAsync(material, caller))
        {
            return Forbid();
        }

        return Redirect(material.FileUrl);
    }

    // SDA-18: "every enrolled subject has a non-empty course-info and teacher-info
    // entry" — enrollment is derived from section_enrollments -> timetable_slots ->
    // subjects, since that's the only place "which subjects does this student take" is
    // actually recorded (there's no direct student-subject table).
    [HttpGet("subjects/mine")]
    public async Task<ActionResult<List<CourseInfoDto>>> MySubjects()
    {
        var studentId = CurrentUserId();

        var sectionIds = await db.SectionEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.SectionId)
            .ToListAsync();

        var subjectIds = await db.TimetableSlots
            .Where(t => sectionIds.Contains(t.SectionId))
            .Select(t => t.SubjectId)
            .Distinct()
            .ToListAsync();

        var courses = await db.Subjects
            .Where(s => subjectIds.Contains(s.Id))
            .Select(s => new CourseInfoDto(s.Id, s.Code, s.Name, s.TeacherId, s.Teacher != null ? s.Teacher.FullName : null))
            .ToListAsync();

        return Ok(courses);
    }

    // SDA-17: "feedback is attributable to the course/teacher it was submitted
    // against" — attributed via TeacherId (teacher_feedback has no subject_id column;
    // SDA-18's course list is what resolves a teacher back to a specific course for the
    // student submitting feedback).
    [HttpPost("teacher-feedback")]
    public async Task<ActionResult<TeacherFeedbackDto>> SubmitTeacherFeedback(SubmitTeacherFeedbackRequest request)
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }
        if (caller.AccountType != AccountType.Student)
        {
            return Forbid();
        }
        if (request.Rating is < 1 or > 5)
        {
            return BadRequest(new { error = "invalid_rating", message = "Rating must be between 1 and 5." });
        }

        var teacher = await db.Users.FindAsync(request.TeacherId);
        if (teacher is null || teacher.AccountType != AccountType.Teacher)
        {
            return BadRequest(new { error = "unknown_teacher", message = "No teacher exists with that id." });
        }

        var feedback = new TeacherFeedback
        {
            Id = Guid.NewGuid(),
            StudentId = caller.Id,
            TeacherId = request.TeacherId,
            Rating = request.Rating,
            Comments = request.Comments,
            SubmittedAt = DateTime.UtcNow,
        };
        db.TeacherFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        return Ok(new TeacherFeedbackDto(feedback.Id, feedback.StudentId, feedback.TeacherId, feedback.Rating, feedback.Comments, feedback.SubmittedAt));
    }

    private async Task<bool> CanViewMaterialAsync(Material material, User caller)
    {
        if (material.UploadedBy == caller.Id || caller.AccountType == AccountType.AdminTier)
        {
            return true;
        }

        if (material.GroupId is not null)
        {
            return await db.GroupMembers.AnyAsync(m => m.GroupId == material.GroupId && m.UserId == caller.Id);
        }

        if (material.SubjectId is not null)
        {
            // Check the subject's own assigned teacher directly, not just TimetableSlots —
            // a newly assigned subject teacher has no timetable slot yet until scheduling
            // runs, but should still be able to view their own subject's material.
            var isSubjectTeacher = await db.Subjects
                .AnyAsync(s => s.Id == material.SubjectId && s.TeacherId == caller.Id);
            var teachesSubject = isSubjectTeacher || await db.TimetableSlots
                .AnyAsync(t => t.SubjectId == material.SubjectId && t.TeacherId == caller.Id);
            if (teachesSubject)
            {
                return true;
            }

            var callerSectionIds = await db.SectionEnrollments
                .Where(e => e.StudentId == caller.Id)
                .Select(e => e.SectionId)
                .ToListAsync();

            return await db.TimetableSlots
                .AnyAsync(t => t.SubjectId == material.SubjectId && callerSectionIds.Contains(t.SectionId));
        }

        return false;
    }

    private static MaterialDto ToDto(Material m) =>
        new(m.Id, m.Title, m.FileUrl, m.SubjectId, m.GroupId, m.UploadedBy, m.UploadedAt);

    private static bool TryValidateUrl(string url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task<User?> CurrentUserAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        return await db.Users.FindAsync(userId);
    }

    private static GroupDto ToDto(Group g) => new(g.Id, g.Name, g.Type.ToString(), g.SectionId);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

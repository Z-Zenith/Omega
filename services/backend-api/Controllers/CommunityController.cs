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

    // API-02. Not gated by "create_group" — that permission is for Lecturer/HoD/Admin to
    // hand-create individual groups, whereas this provisions every section's auto-managed
    // class group in one call, so it's restricted to Admin only. Scoped to the caller's own
    // college (multi-tenant boundary — mirrors CollegeId scoping elsewhere, e.g.
    // BrowsingController). Idempotent: safe to call more than once for the same semester
    // (e.g. a retry, or a newly-enrolled student joining after the first run) — an existing
    // class group is topped up with any newly-enrolled students instead of duplicated.
    // Membership is additive only: a student later dropped from section_enrollments is not
    // removed from the group by this endpoint. That's a deliberate scope decision, not an
    // oversight — "enroll its students" (the acceptance criterion) doesn't require reconciling
    // drops, and removal is a materially different (and riskier) operation to bundle in here.
    //
    // Known scope gap: the architecture doc's requirement is that this happen automatically
    // when a new semester starts, with "no manual step required." No scheduler/cron
    // infrastructure exists anywhere in this codebase yet, so this ships as a callable,
    // idempotent building block that an Admin triggers by hand — the automatic-trigger half
    // of API-02 is an open follow-up, not something this endpoint claims to satisfy on its own.
    //
    // Known race: nothing at the DB level enforces "at most one Class group per section" (see
    // docs/campus-platform-db-api-schema.md Open Items) — two concurrent calls to this
    // endpoint could each see no existing group for a section and both create one. A partial
    // unique index would close that, but adding one is a DB schema change that needs the
    // contract-change sign-off (CLAUDE.md), not something to slip in unilaterally here. Until
    // that lands, the grouping below picks one group deterministically instead of crashing if
    // it ever finds a duplicate.
    [HttpPost("groups/provision")]
    public async Task<ActionResult<ProvisionClassGroupsResponse>> ProvisionClassGroups()
    {
        var caller = await CurrentUserAsync();
        if (caller is null)
        {
            return Unauthorized();
        }
        if (caller.AccountType is not AccountType.AdminTier)
        {
            return Forbid();
        }

        var sections = await db.Sections
            .Where(s => s.Department.CollegeId == caller.CollegeId)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        var sectionIds = sections.Select(s => s.Id).ToList();

        // Scoped by section membership rather than a separate `g.CollegeId == caller.CollegeId`
        // filter — that would be a second, independently-maintained source of truth for "which
        // college" alongside the sections query above, and the two could drift apart (e.g. a
        // department reassigned to a different college after its class group already exists).
        var existingClassGroups = (await db.Groups
                .Where(g => g.Type == GroupType.Class && g.SectionId != null && sectionIds.Contains(g.SectionId.Value))
                .ToListAsync())
            .GroupBy(g => g.SectionId!.Value)
            .ToDictionary(grp => grp.Key, grp => grp.OrderBy(g => g.Id).First());

        var enrollmentsBySection = (await db.SectionEnrollments
                .Where(e => sectionIds.Contains(e.SectionId))
                .ToListAsync())
            .ToLookup(e => e.SectionId, e => e.StudentId);

        var existingGroupIds = existingClassGroups.Values.Select(g => g.Id).ToList();
        var existingMembersByGroup = (await db.GroupMembers
                .Where(m => existingGroupIds.Contains(m.GroupId))
                .ToListAsync())
            .ToLookup(m => m.GroupId, m => m.UserId);

        var groupsCreated = 0;
        var studentsEnrolled = 0;
        var now = DateTime.UtcNow;

        foreach (var section in sections)
        {
            if (!existingClassGroups.TryGetValue(section.Id, out var group))
            {
                group = new Group
                {
                    Id = Guid.NewGuid(),
                    CollegeId = caller.CollegeId,
                    Name = section.Name.Trim(),
                    Type = GroupType.Class,
                    SectionId = section.Id,
                    CreatedBy = null, // null marks it as auto-created, per the groups table's own convention
                };
                db.Groups.Add(group);
                groupsCreated++;
            }

            var memberIds = existingMembersByGroup[group.Id].ToHashSet();
            foreach (var studentId in enrollmentsBySection[section.Id])
            {
                if (!memberIds.Add(studentId))
                {
                    continue;
                }
                db.GroupMembers.Add(new GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    UserId = studentId,
                    JoinedAt = now,
                });
                studentsEnrolled++;
            }
        }

        await db.SaveChangesAsync();

        var groupsAlreadyExisted = sections.Count - groupsCreated;
        return Ok(new ProvisionClassGroupsResponse(groupsCreated, groupsAlreadyExisted, studentsEnrolled));
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

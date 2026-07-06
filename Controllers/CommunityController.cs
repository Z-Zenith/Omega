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

    [HttpPost("materials")]
    public IActionResult UploadMaterial() => StatusCode(501, new { feature = "TWA-06", status = "not_implemented" });

    [HttpGet("materials/{id}/download")]
    public IActionResult DownloadMaterial(Guid id) => StatusCode(501, new { feature = "API-03", status = "not_implemented" });

    private static GroupDto ToDto(Group g) => new(g.Id, g.Name, g.Type.ToString(), g.SectionId);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

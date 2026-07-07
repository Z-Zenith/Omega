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
[Route("api/v1")]
[Authorize]
public class RolesController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    // AWA-13
    [HttpGet("role-bindings")]
    public async Task<ActionResult<List<RoleBindingDto>>> ListRoleBindings()
    {
        if (!await permissions.HasPermissionAsync(CurrentUserId(), "manage_roles_and_permissions"))
        {
            return Forbid();
        }

        var bindings = await db.RoleBindings
            .Include(b => b.User)
            .OrderByDescending(b => b.GrantedAt)
            .ToListAsync();
        return Ok(bindings.Select(ToDto).ToList());
    }

    // AWA-13
    [HttpPost("role-bindings")]
    public async Task<ActionResult<RoleBindingDto>> CreateRoleBinding(CreateRoleBindingRequest request)
    {
        if (!await permissions.HasPermissionAsync(CurrentUserId(), "manage_roles_and_permissions"))
        {
            return Forbid();
        }

        var user = await db.Users.FindAsync(request.UserId);
        if (user is null)
        {
            return NotFound("User not found.");
        }
        if (!await db.Roles.AnyAsync(r => r.Code == request.RoleCode))
        {
            return BadRequest("Unknown role code.");
        }
        if (request.ScopeType == ScopeKind.Department && request.DepartmentId is null)
        {
            return BadRequest("DepartmentId is required for a department-scoped binding.");
        }
        if (request.ScopeType == ScopeKind.Global && request.DepartmentId is not null)
        {
            return BadRequest("DepartmentId must be null for a global-scoped binding.");
        }

        var binding = new RoleBinding
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            RoleCode = request.RoleCode,
            ScopeType = request.ScopeType,
            DepartmentId = request.DepartmentId,
            GrantedAt = DateTime.UtcNow,
        };
        db.RoleBindings.Add(binding);
        await db.SaveChangesAsync();

        binding.User = user;
        return Ok(ToDto(binding));
    }

    // AWA-13
    [HttpGet("permission-grants")]
    public async Task<ActionResult<List<PermissionGrantDto>>> ListPermissionGrants()
    {
        if (!await permissions.HasPermissionAsync(CurrentUserId(), "manage_roles_and_permissions"))
        {
            return Forbid();
        }

        var grants = await db.PermissionGrants
            .Include(g => g.User)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
        return Ok(grants.Select(ToDto).ToList());
    }

    // AWA-13
    [HttpPost("permission-grants")]
    public async Task<ActionResult<PermissionGrantDto>> CreatePermissionGrant(CreatePermissionGrantRequest request)
    {
        var currentUserId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(currentUserId, "manage_roles_and_permissions"))
        {
            return Forbid();
        }

        var user = await db.Users.FindAsync(request.UserId);
        if (user is null)
        {
            return NotFound("User not found.");
        }
        if (!await db.Permissions.AnyAsync(p => p.Code == request.PermissionCode))
        {
            return BadRequest("Unknown permission code.");
        }
        if (request.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            return BadRequest("ExpiresAt must be in the future.");
        }

        var grant = new PermissionGrant
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PermissionCode = request.PermissionCode,
            Granted = request.Granted,
            ExpiresAt = request.ExpiresAt,
            GrantedBy = currentUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.PermissionGrants.Add(grant);
        await db.SaveChangesAsync();

        grant.User = user;
        return Ok(ToDto(grant));
    }

    // AWA-13 — deleting the override row is the revoke: PermissionService (IPermissionService)
    // reads permission_grants live on every check, so removal takes effect on the caller's very
    // next request without needing to re-login, satisfying the AC.
    [HttpDelete("permission-grants/{id}")]
    public async Task<IActionResult> DeletePermissionGrant(Guid id)
    {
        if (!await permissions.HasPermissionAsync(CurrentUserId(), "manage_roles_and_permissions"))
        {
            return Forbid();
        }

        var grant = await db.PermissionGrants.FindAsync(id);
        if (grant is null)
        {
            return NotFound();
        }

        db.PermissionGrants.Remove(grant);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // AWA-14
    [HttpPost("departments")]
    public IActionResult CreateDepartment() => StatusCode(501, new { feature = "AWA-14", status = "not_implemented" });

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static RoleBindingDto ToDto(RoleBinding b) => new(
        b.Id, b.UserId, b.User.FullName, b.RoleCode, b.ScopeType, b.DepartmentId, b.GrantedAt);

    private static PermissionGrantDto ToDto(PermissionGrant g) => new(
        g.Id, g.UserId, g.User.FullName, g.PermissionCode, g.Granted, g.ExpiresAt, g.GrantedBy, g.CreatedAt);
}

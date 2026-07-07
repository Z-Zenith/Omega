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
    [HttpPost("role-bindings")]
    public IActionResult CreateRoleBinding() => StatusCode(501, new { feature = "AWA-13", status = "not_implemented" });

    // AWA-13
    [HttpPost("permission-grants")]
    public IActionResult CreatePermissionGrant() => StatusCode(501, new { feature = "AWA-13", status = "not_implemented" });

    // AWA-13
    [HttpDelete("permission-grants/{id}")]
    public IActionResult DeletePermissionGrant(Guid id) => StatusCode(501, new { feature = "AWA-13", status = "not_implemented" });

    // AWA-14: create a department. Gated to whoever holds manage_departments
    // (Admin by default; IT can be granted it via a PermissionGrant per Section 9).
    [HttpPost("departments")]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment(CreateDepartmentRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "manage_departments"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var collegeExists = await db.Colleges.AnyAsync(c => c.Id == request.CollegeId);
        if (!collegeExists)
        {
            return BadRequest("Unknown college.");
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            CollegeId = request.CollegeId,
            Name = request.Name,
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(CreateDepartment), new { id = department.Id }, ToDto(department));
    }

    // AWA-14: assign (or reassign) the Head of Department. A department has at most one
    // active HoD binding at a time — departments.hod_role_binding_id is the single pointer
    // to the active binding, so reassigning must both point it at a freshly-created binding
    // and remove the previous binding row in the same transaction. Removing it (rather than
    // just repointing the FK) matters because PermissionService/GetDepartmentScopeAsync read
    // role_bindings directly — a stale row left behind would keep granting the old HoD's
    // department-scoped permissions even after they were replaced.
    [HttpPost("departments/{id}/hod")]
    public async Task<ActionResult<DepartmentDto>> AssignHod(Guid id, AssignHodRequest request)
    {
        var userId = CurrentUserId();
        if (!await permissions.HasPermissionAsync(userId, "manage_departments"))
        {
            return Forbid();
        }

        var department = await db.Departments.FindAsync(id);
        if (department is null)
        {
            return NotFound();
        }

        var candidate = await db.Users.FindAsync(request.UserId);
        if (candidate is null)
        {
            return BadRequest("Unknown user.");
        }
        if (candidate.CollegeId != department.CollegeId)
        {
            return BadRequest("The user must belong to the department's college.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var previousBindingId = department.HodRoleBindingId;

        var newBinding = new RoleBinding
        {
            Id = Guid.NewGuid(),
            UserId = candidate.Id,
            RoleCode = "hod",
            ScopeType = ScopeKind.Department,
            DepartmentId = department.Id,
            GrantedAt = DateTime.UtcNow,
        };
        db.RoleBindings.Add(newBinding);
        await db.SaveChangesAsync();

        department.HodRoleBindingId = newBinding.Id;
        await db.SaveChangesAsync();

        if (previousBindingId is { } oldId && oldId != newBinding.Id)
        {
            var previousBinding = await db.RoleBindings.FindAsync(oldId);
            if (previousBinding is not null)
            {
                db.RoleBindings.Remove(previousBinding);
                await db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();

        return Ok(ToDto(department, newBinding.UserId));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static DepartmentDto ToDto(Department d, Guid? hodUserId = null) =>
        new(d.Id, d.CollegeId, d.Name, d.HodRoleBindingId, hodUserId);
}

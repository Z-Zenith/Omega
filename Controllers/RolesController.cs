using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
public class RolesController : ControllerBase
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

    // AWA-14
    [HttpPost("departments")]
    public IActionResult CreateDepartment() => StatusCode(501, new { feature = "AWA-14", status = "not_implemented" });
}

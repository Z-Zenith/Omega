using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/parent")]
public class ParentController : ControllerBase
{
    // PRT-01 — roll number + DOB only, no TOTP.
    [HttpPost("login")]
    public IActionResult Login() => StatusCode(501, new { feature = "PRT-01", status = "not_implemented" });
}

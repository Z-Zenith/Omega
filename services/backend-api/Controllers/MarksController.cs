using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/marks")]
public class MarksController : ControllerBase
{
    // TWA-16
    [HttpPost("internal")]
    public IActionResult CreateInternal() => StatusCode(501, new { feature = "TWA-16", status = "not_implemented" });

    // TWA-17
    [HttpPost("external")]
    public IActionResult CreateExternal() => StatusCode(501, new { feature = "TWA-17", status = "not_implemented" });

    // TWA-20
    [HttpPost("external/{id}/approve")]
    public IActionResult ApproveExternal(Guid id) => StatusCode(501, new { feature = "TWA-20", status = "not_implemented" });

    // SDA-15
    [HttpGet("mine")]
    public IActionResult Mine() => StatusCode(501, new { feature = "SDA-15", status = "not_implemented" });

    // PRT-02
    [HttpGet("ward/{studentId}")]
    public IActionResult Ward(Guid studentId) => StatusCode(501, new { feature = "PRT-02", status = "not_implemented" });
}

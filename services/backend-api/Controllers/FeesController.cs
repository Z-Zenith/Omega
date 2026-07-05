using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1/fees")]
public class FeesController : ControllerBase
{
    // AWA-04 (Track 2)
    [HttpPost("links")]
    public IActionResult CreateLink() => StatusCode(501, new { feature = "AWA-04", status = "not_implemented" });

    // PRT-03
    [HttpPost("{id}/pay")]
    public IActionResult Pay(Guid id) => StatusCode(501, new { feature = "PRT-03", status = "not_implemented" });

    // PRT-02
    [HttpGet("ward/{studentId}")]
    public IActionResult Ward(Guid studentId) => StatusCode(501, new { feature = "PRT-02", status = "not_implemented" });
}

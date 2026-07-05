using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

// Track 2 surface (community/groups/materials) — stubbed here only to keep the shared
// API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
public class CommunityController : ControllerBase
{
    [HttpPost("groups")]
    public IActionResult CreateGroup() => StatusCode(501, new { feature = "TWA-05/AWA-12", status = "not_implemented" });

    [HttpGet("groups/mine")]
    public IActionResult MyGroups() => StatusCode(501, new { feature = "SDA-16", status = "not_implemented" });

    [HttpPost("groups/{id}/posts")]
    public IActionResult CreatePost(Guid id) => StatusCode(501, new { feature = "SDA-16", status = "not_implemented" });

    [HttpPost("materials")]
    public IActionResult UploadMaterial() => StatusCode(501, new { feature = "TWA-06", status = "not_implemented" });

    [HttpGet("materials/{id}/download")]
    public IActionResult DownloadMaterial(Guid id) => StatusCode(501, new { feature = "API-03", status = "not_implemented" });
}

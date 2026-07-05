using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

// Track 2 surface (whitelisted browser, telemetry, AI services) — stubbed here only to
// keep the shared API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
public class BrowsingController : ControllerBase
{
    [HttpGet("whitelist")]
    public IActionResult GetWhitelist() => StatusCode(501, new { feature = "SDA-03", status = "not_implemented" });

    [HttpPost("whitelist/requests")]
    public IActionResult RequestWhitelist() => StatusCode(501, new { feature = "SDA-04", status = "not_implemented" });

    [HttpPost("whitelist/requests/{id}/approve")]
    public IActionResult ApproveWhitelistRequest(Guid id) => StatusCode(501, new { feature = "SDA-04", status = "not_implemented" });

    [HttpGet("students/{id}/browsing-summary")]
    public IActionResult BrowsingSummary(Guid id) => StatusCode(501, new { feature = "AIS-01", status = "not_implemented" });

    [HttpPost("telemetry")]
    public IActionResult PostTelemetry() => StatusCode(501, new { feature = "SDA-25", status = "not_implemented" });

    [HttpGet("suspicious-flags")]
    public IActionResult SuspiciousFlags() => StatusCode(501, new { feature = "AIS-07", status = "not_implemented" });
}

using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

// Track 2 surface (assignments, AI services) — stubbed here only to keep the shared
// API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
public class AssignmentsController : ControllerBase
{
    [HttpPost("assignments")]
    public IActionResult Create() => StatusCode(501, new { feature = "TWA-07", status = "not_implemented" });

    [HttpPost("assignments/{id}/submissions")]
    public IActionResult Submit(Guid id) => StatusCode(501, new { feature = "SDA-10/11", status = "not_implemented" });

    [HttpGet("submissions/{id}/plagiarism-report")]
    public IActionResult PlagiarismReport(Guid id) => StatusCode(501, new { feature = "AIS-02", status = "not_implemented" });

    [HttpGet("assignments/{id}/copy-check")]
    public IActionResult CopyCheck(Guid id) => StatusCode(501, new { feature = "AIS-03", status = "not_implemented" });

    [HttpGet("submissions/{id}/ai-detection")]
    public IActionResult AiDetection(Guid id) => StatusCode(501, new { feature = "AIS-05", status = "not_implemented" });

    [HttpGet("submissions/{id}/autograde-suggestion")]
    public IActionResult AutogradeSuggestion(Guid id) => StatusCode(501, new { feature = "AIS-04", status = "not_implemented" });

    [HttpPost("submissions/{id}/grade")]
    public IActionResult Grade(Guid id) => StatusCode(501, new { feature = "grade-confirm", status = "not_implemented" });
}

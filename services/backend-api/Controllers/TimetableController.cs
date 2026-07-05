using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
public class TimetableController : ControllerBase
{
    // AWA-01, AWA-02
    [HttpPost("timetable/generate")]
    public IActionResult Generate() => StatusCode(501, new { feature = "AWA-01/02", status = "not_implemented" });

    // AWA-03
    [HttpPatch("timetable/slots/{id}")]
    public IActionResult PatchSlot(Guid id) => StatusCode(501, new { feature = "AWA-03", status = "not_implemented" });

    // TWA-10
    [HttpGet("timetable/mine")]
    public IActionResult Mine() => StatusCode(501, new { feature = "TWA-10", status = "not_implemented" });

    // TWA-13
    [HttpPost("timetable/change-requests")]
    public IActionResult CreateChangeRequest() => StatusCode(501, new { feature = "TWA-13", status = "not_implemented" });

    // TWA-08
    [HttpPost("attendance")]
    public IActionResult MarkAttendance() => StatusCode(501, new { feature = "TWA-08", status = "not_implemented" });

    // TWA-09
    [HttpGet("attendance/alerts")]
    public IActionResult AttendanceAlerts() => StatusCode(501, new { feature = "TWA-09", status = "not_implemented" });
}

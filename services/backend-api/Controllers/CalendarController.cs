using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
public class CalendarController : ControllerBase
{
    // TWA-15, AWA-11
    [HttpPost("events")]
    public IActionResult CreateEvent() => StatusCode(501, new { feature = "TWA-15/AWA-11", status = "not_implemented" });

    // SDA-20
    [HttpGet("events")]
    public IActionResult ListEvents() => StatusCode(501, new { feature = "SDA-20", status = "not_implemented" });

    // SDA-20
    [HttpPost("events/{id}/register")]
    public IActionResult RegisterForEvent(Guid id) => StatusCode(501, new { feature = "SDA-20", status = "not_implemented" });

    // SDA-14
    [HttpGet("calendar/mine")]
    public IActionResult MyCalendar() => StatusCode(501, new { feature = "SDA-14", status = "not_implemented" });
}

using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
public class MessagingController : ControllerBase
{
    // DMS-01 (Track 2)
    [HttpPost("messages/threads/{id}/messages")]
    public IActionResult SendMessage(Guid id) => StatusCode(501, new { feature = "DMS-01", status = "not_implemented" });

    // DMS-01 (Track 2)
    [HttpGet("messages/threads")]
    public IActionResult ListThreads() => StatusCode(501, new { feature = "DMS-01", status = "not_implemented" });

    // Notification Router (shared)
    [HttpGet("notifications")]
    public IActionResult ListNotifications() => StatusCode(501, new { feature = "Notification Router", status = "not_implemented" });
}

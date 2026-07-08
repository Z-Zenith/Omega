using System.Text.Json;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// AIS-02: receives Copyleaks' scan-completion callback (see CopyleaksClient.SubmitScanAsync,
// which registers a URL of this shape as the scan's webhook). Unauthenticated by necessity
// — Copyleaks itself calls this, not a signed-in user — so the `secret` query parameter
// (must match Copyleaks:WebhookSecret) is the only thing stopping an arbitrary caller from
// injecting fake plagiarism results into a submission's report.
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public class WebhooksController(AppDbContext db, IConfiguration configuration, ICopyleaksClient copyleaks) : ControllerBase
{
    [HttpPost("copyleaks/{scanId}/{status}")]
    public async Task<IActionResult> CopyleaksResult(string scanId, string status, [FromQuery] string? secret, [FromBody] JsonElement payload)
    {
        var expectedSecret = configuration["Copyleaks:WebhookSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
        {
            return Unauthorized();
        }

        if (!Guid.TryParseExact(scanId, "N", out var submissionId))
        {
            return BadRequest(new { error = "invalid_scan_id", message = "scanId does not map to a known submission." });
        }

        // Copyleaks also calls back with "error"/"creditsUsage" statuses this integration
        // doesn't act on — only a completed scan produces a report.
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return Ok();
        }

        var submissionExists = await db.Submissions.AnyAsync(s => s.Id == submissionId);
        if (!submissionExists)
        {
            return NotFound();
        }

        PlagiarismScanResult result;
        try
        {
            result = copyleaks.ParseWebhookResult(payload);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = "malformed_payload", message = "Unrecognized Copyleaks webhook payload shape." });
        }

        // Re-scanning (e.g. a teacher retriggers after a submission edit) replaces the
        // previous report rather than accumulating stale ones — same re-check idempotency
        // AIS-03's copy-check already follows.
        var existing = await db.PlagiarismReports.Where(r => r.SubmissionId == submissionId).ToListAsync();
        db.PlagiarismReports.RemoveRange(existing);

        db.PlagiarismReports.Add(new PlagiarismReport
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            SimilarityScore = (decimal)result.SimilarityScore,
            CopyleaksScanId = scanId,
            MatchedSources = JsonSerializer.Serialize(result.MatchedSourceUrls),
            CheckedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return Ok();
    }
}

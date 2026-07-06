using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// Track 2 surface (whitelisted browser, telemetry, AI services) — stubbed here only to
// keep the shared API contract complete; implementation belongs to Track 2.
[ApiController]
[Route("api/v1")]
[Authorize]
public class BrowsingController(AppDbContext db) : ControllerBase
{
    // SDA-03: whitelist_sites is college-scoped (not per-class) — this is the already-
    // decided design that SDA-04's "approval applies institution-wide" acceptance
    // criterion depends on, so every class in the college shares one list.
    [HttpGet("whitelist")]
    public async Task<ActionResult<WhitelistResponse>> GetWhitelist()
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var sites = await db.WhitelistSites
            .Where(s => s.CollegeId == user.CollegeId)
            .OrderBy(s => s.Url)
            .ToListAsync();

        return Ok(new WhitelistResponse(sites.Select(s => new WhitelistSiteDto(s.Id, s.Url, s.ApprovedAt)).ToList()));
    }

    // SDA-04
    [HttpPost("whitelist/requests")]
    public async Task<ActionResult<WhitelistRequestDto>> RequestWhitelist(CreateWhitelistRequestRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!TryNormalizeUrl(request.Url, out var normalizedUrl))
        {
            return BadRequest(new { error = "invalid_url", message = "URL must be an absolute http:// or https:// address." });
        }

        var alreadyWhitelisted = await db.WhitelistSites
            .AnyAsync(s => s.CollegeId == user.CollegeId && s.Url == normalizedUrl);
        if (alreadyWhitelisted)
        {
            return Conflict(new { error = "already_whitelisted", message = "This site is already approved for your college." });
        }

        var existingPending = await db.WhitelistRequests
            .Include(r => r.RequestedByNavigation)
            .FirstOrDefaultAsync(r => r.Url == normalizedUrl
                && r.Status == WhitelistRequestStatus.Pending
                && r.RequestedByNavigation.CollegeId == user.CollegeId);
        if (existingPending is not null)
        {
            return Ok(ToDto(existingPending));
        }

        var newRequest = new WhitelistRequest
        {
            Id = Guid.NewGuid(),
            Url = normalizedUrl,
            RequestedBy = user.Id,
            Status = WhitelistRequestStatus.Pending,
        };
        db.WhitelistRequests.Add(newRequest);
        await db.SaveChangesAsync();

        return Ok(ToDto(newRequest));
    }

    // SDA-04: not in the documented API map, but "Teacher sees a pending-request queue"
    // (acceptance criterion) has no way to be satisfied without a list endpoint.
    [HttpGet("whitelist/requests")]
    public async Task<ActionResult<List<WhitelistRequestDto>>> ListPendingRequests()
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }
        if (user.AccountType is not (AccountType.Teacher or AccountType.AdminTier))
        {
            return Forbid();
        }

        var pending = await db.WhitelistRequests
            .Include(r => r.RequestedByNavigation)
            .Where(r => r.Status == WhitelistRequestStatus.Pending && r.RequestedByNavigation.CollegeId == user.CollegeId)
            .ToListAsync();

        return Ok(pending.Select(ToDto).ToList());
    }

    // SDA-04
    [HttpPost("whitelist/requests/{id}/approve")]
    public async Task<ActionResult<ApproveWhitelistRequestResponse>> ApproveWhitelistRequest(Guid id)
    {
        var reviewer = await CurrentUserAsync();
        if (reviewer is null)
        {
            return Unauthorized();
        }
        if (reviewer.AccountType is not (AccountType.Teacher or AccountType.AdminTier))
        {
            return Forbid();
        }

        var whitelistRequest = await db.WhitelistRequests
            .Include(r => r.RequestedByNavigation)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (whitelistRequest is null)
        {
            return NotFound();
        }
        if (whitelistRequest.Status != WhitelistRequestStatus.Pending)
        {
            return Conflict(new { error = "already_reviewed", message = "This request has already been reviewed." });
        }

        whitelistRequest.Status = WhitelistRequestStatus.Approved;
        whitelistRequest.ReviewedBy = reviewer.Id;

        var requesterCollegeId = whitelistRequest.RequestedByNavigation.CollegeId;
        var site = await db.WhitelistSites
            .FirstOrDefaultAsync(s => s.CollegeId == requesterCollegeId && s.Url == whitelistRequest.Url);
        if (site is null)
        {
            site = new WhitelistSite
            {
                Id = Guid.NewGuid(),
                CollegeId = requesterCollegeId,
                Url = whitelistRequest.Url,
                ApprovedAt = DateTime.UtcNow,
            };
            db.WhitelistSites.Add(site);
        }

        await db.SaveChangesAsync();

        return Ok(new ApproveWhitelistRequestResponse(
            whitelistRequest.Id,
            whitelistRequest.Status.ToString(),
            new WhitelistSiteDto(site.Id, site.Url, site.ApprovedAt)));
    }

    [HttpGet("students/{id}/browsing-summary")]
    public IActionResult BrowsingSummary(Guid id) => StatusCode(501, new { feature = "AIS-01", status = "not_implemented" });

    [HttpPost("telemetry")]
    public IActionResult PostTelemetry() => StatusCode(501, new { feature = "SDA-25", status = "not_implemented" });

    [HttpGet("suspicious-flags")]
    public IActionResult SuspiciousFlags() => StatusCode(501, new { feature = "AIS-07", status = "not_implemented" });

    private static WhitelistRequestDto ToDto(WhitelistRequest r) =>
        new(r.Id, r.Url, r.RequestedBy, r.Status.ToString(), r.ReviewedBy);

    private static bool TryNormalizeUrl(string url, out string normalized)
    {
        normalized = url?.Trim() ?? "";
        return normalized.Length > 0
            && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task<User?> CurrentUserAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        return await db.Users.FindAsync(userId);
    }
}

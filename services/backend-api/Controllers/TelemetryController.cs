using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using BackendApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendApi.Controllers;

// SDA-25: usage-pattern telemetry reported by the student desktop app, gathered only
// while a class session or assignment window is active (SDA-01/11/12/13). The client only
// ever claims an AssignmentId; for events without one, this endpoint resolves the active
// class session itself via ClassSessionLookup — the same server-side authority SDA-12/
// TWA-08 use — rather than trusting a client-supplied session id. AC: "no telemetry
// reported outside active windows" is enforced here by rejecting any event that has
// neither an AssignmentId nor a currently-active class session, not just by the client's
// own gating (which the client also does, but a server can't rely on that alone).
[ApiController]
[Route("api/v1/telemetry")]
[Authorize]
public class TelemetryController(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<TelemetryController> logger) : ControllerBase
{
    [HttpPost("usage")]
    public async Task<ActionResult<SubmitTelemetryResponse>> SubmitUsage(SubmitTelemetryRequest request)
    {
        var studentId = CurrentUserId();
        var student = await db.Users.FindAsync(studentId);
        if (student is null || student.AccountType != AccountType.Student)
        {
            return Forbid();
        }

        var events = request.Events ?? [];
        foreach (var e in events)
        {
            if (string.IsNullOrWhiteSpace(e.EventType))
            {
                return BadRequest(new { error = "event_type_required" });
            }
        }

        // Resolved once per request (all events in a batch share "now"), not per-event —
        // avoids redundant lookups/ClassSession creation when a batch has several
        // class-window events queued from the same short polling interval.
        var activeSession = await ClassSessionLookup.FindOrStartActiveSessionAsync(db, studentId, DateTime.UtcNow);

        var records = new List<UsageTelemetry>();
        var resolvedEvents = new List<(TelemetryEventRequest Event, Guid? ClassSessionId)>();
        foreach (var e in events)
        {
            var classSessionId = e.AssignmentId is null ? activeSession?.ClassSessionId : null;
            if (e.AssignmentId is null && classSessionId is null)
            {
                return BadRequest(new { error = "window_required", message = "No active class session or assignment for this event." });
            }

            records.Add(new UsageTelemetry
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                ClassSessionId = classSessionId,
                AssignmentId = e.AssignmentId,
                EventType = e.EventType,
                Metadata = JsonSerializer.Serialize(e.Metadata ?? []),
                RecordedAt = e.RecordedAt,
            });
            resolvedEvents.Add((e, classSessionId));
        }

        db.UsageTelemetries.AddRange(records);
        await db.SaveChangesAsync();

        // AI Services is a Track-2-owned stub — forwarding is best-effort. If it's
        // unreachable, the raw telemetry is still safely persisted above for a later
        // batch pass; a student's request must never fail just because the anomaly
        // service happens to be down.
        var flagsRaised = await TryFlagSuspiciousBehaviourAsync(studentId, resolvedEvents);

        return Ok(new SubmitTelemetryResponse(records.Count, flagsRaised));
    }

    private async Task<int> TryFlagSuspiciousBehaviourAsync(Guid studentId, List<(TelemetryEventRequest Event, Guid? ClassSessionId)> events)
    {
        try
        {
            var client = httpClientFactory.CreateClient("AiServices");
            var payload = new
            {
                events = events.Select(e => new
                {
                    student_id = studentId.ToString(),
                    class_session_id = e.ClassSessionId?.ToString(),
                    assignment_id = e.Event.AssignmentId?.ToString(),
                    event_type = e.Event.EventType,
                    metadata = e.Event.Metadata ?? [],
                    recorded_at = e.Event.RecordedAt,
                }),
            };

            var response = await client.PostAsJsonAsync("/api/v1/suspicious-behaviour", payload);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI Services suspicious-behaviour check returned {Status}", response.StatusCode);
                return 0;
            }

            var result = await response.Content.ReadFromJsonAsync<SuspiciousBehaviourResponse>(SnakeCaseJsonOptions);
            if (result?.Flags is not { Count: > 0 } flags)
            {
                return 0;
            }

            foreach (var flag in flags)
            {
                db.SuspiciousFlags.Add(new SuspiciousFlag
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    ClassSessionId = flag.ClassSessionId is { } csid ? Guid.Parse(csid) : null,
                    AssignmentId = flag.AssignmentId is { } aid ? Guid.Parse(aid) : null,
                    ConfidenceScore = (decimal)flag.ConfidenceScore,
                    FlaggedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
            return flags.Count;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not reach AI Services for suspicious-behaviour analysis.");
            return 0;
        }
    }

    // AI Services (Python/FastAPI) emits snake_case JSON field names verbatim from its
    // Pydantic schemas (no camelCase alias generator) — map explicitly rather than rely
    // on a naming policy that only handles casing, not underscore removal.
    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record SuspiciousBehaviourResponse([property: JsonPropertyName("flags")] List<SuspiciousFlagResponse> Flags);

    private record SuspiciousFlagResponse(
        [property: JsonPropertyName("student_id")] string StudentId,
        [property: JsonPropertyName("class_session_id")] string? ClassSessionId,
        [property: JsonPropertyName("assignment_id")] string? AssignmentId,
        [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
        [property: JsonPropertyName("reasons")] List<string> Reasons);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

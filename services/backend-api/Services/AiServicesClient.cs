using System.Net.Http.Json;

namespace BackendApi.Services;

public record RubricCriterion(string Name, IReadOnlyList<string> Keywords, double Weight);

public record AutogradeSuggestionResult(double SuggestedGrade, double MaxScore, double Confidence, IReadOnlyList<string> MatchedCriteria, IReadOnlyList<string> Feedback);

public record SimilarityMatchResult(string SubmissionAId, string SubmissionBId, double SimilarityScore);

public record TelemetryEventInput(string StudentId, string? ClassSessionId, string? AssignmentId, string EventType, object Metadata, DateTime RecordedAt);

public record SuspiciousFlagResult(string StudentId, string? ClassSessionId, string? AssignmentId, double ConfidenceScore, IReadOnlyList<string> Reasons);

// AIS-03/04/07: thin HTTP client for the self-hosted AI Services container
// (services/ai-services — FastAPI, no external credentials). AIS-01 isn't wired here yet
// — it needs a raw browsing-visit-log table that doesn't exist in the schema, a DB
// change out of scope for a unilateral PR (CLAUDE.md: ask before changing the DB
// schema). AIS-02/05 (Copyleaks/Pangram) are external-API-only and out of scope too —
// no client credentials are available in this environment.
public interface IAiServicesClient
{
    Task<IReadOnlyList<SimilarityMatchResult>> CheckSimilarityAsync(
        IReadOnlyList<(string Id, string Content)> submissions, double threshold, CancellationToken ct = default);

    Task<AutogradeSuggestionResult> SuggestAutogradeAsync(
        string content, IReadOnlyList<RubricCriterion> rubric, double maxScore, CancellationToken ct = default);

    Task<IReadOnlyList<SuspiciousFlagResult>> CheckSuspiciousBehaviourAsync(
        IReadOnlyList<TelemetryEventInput> events, double minConfidence, CancellationToken ct = default);
}

public class AiServicesClient(HttpClient http) : IAiServicesClient
{
    private sealed record SimilarityRequestBody(IReadOnlyList<SubmissionItemBody> Submissions, double Threshold);
    private sealed record SubmissionItemBody(string Id, string Content);
    private sealed record SimilarityResponseBody(List<SimilarityMatchResult> Matches);

    public async Task<IReadOnlyList<SimilarityMatchResult>> CheckSimilarityAsync(
        IReadOnlyList<(string Id, string Content)> submissions, double threshold, CancellationToken ct = default)
    {
        var body = new SimilarityRequestBody(
            submissions.Select(s => new SubmissionItemBody(s.Id, s.Content)).ToList(), threshold);
        var response = await http.PostAsJsonAsync("/api/v1/similarity", body, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SimilarityResponseBody>(cancellationToken: ct);
        return result?.Matches ?? [];
    }

    private sealed record AutogradeRequestBody(string Content, IReadOnlyList<RubricCriterionBody> Rubric, double MaxScore);
    private sealed record RubricCriterionBody(string Name, IReadOnlyList<string> Keywords, double Weight);

    public async Task<AutogradeSuggestionResult> SuggestAutogradeAsync(
        string content, IReadOnlyList<RubricCriterion> rubric, double maxScore, CancellationToken ct = default)
    {
        var body = new AutogradeRequestBody(
            content, rubric.Select(r => new RubricCriterionBody(r.Name, r.Keywords, r.Weight)).ToList(), maxScore);
        var response = await http.PostAsJsonAsync("/api/v1/autograde", body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AutogradeSuggestionResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AI Services returned an empty autograde response.");
    }

    private sealed record SuspiciousBehaviourRequestBody(IReadOnlyList<TelemetryEventInput> Events, double MinConfidence);
    private sealed record SuspiciousBehaviourResponseBody(List<SuspiciousFlagResult> Flags);

    public async Task<IReadOnlyList<SuspiciousFlagResult>> CheckSuspiciousBehaviourAsync(
        IReadOnlyList<TelemetryEventInput> events, double minConfidence, CancellationToken ct = default)
    {
        var body = new SuspiciousBehaviourRequestBody(events, minConfidence);
        var response = await http.PostAsJsonAsync("/api/v1/suspicious-behaviour", body, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SuspiciousBehaviourResponseBody>(cancellationToken: ct);
        return result?.Flags ?? [];
    }

}

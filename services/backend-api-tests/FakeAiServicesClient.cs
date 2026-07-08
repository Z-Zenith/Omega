using BackendApi.Services;

namespace BackendApi.Tests;

// AIS-01/03/04/07: a real HTTP call to services/ai-services isn't available in unit
// tests (no Docker/network dependency), so controller tests configure canned responses
// here instead of hitting the real self-hosted service.
public class FakeAiServicesClient : IAiServicesClient
{
    public IReadOnlyList<SimilarityMatchResult> SimilarityMatches { get; set; } = [];
    public AutogradeSuggestionResult AutogradeResult { get; set; } = new(0, 100, 0, [], []);
    public IReadOnlyList<SuspiciousFlagResult> SuspiciousFlagResults { get; set; } = [];
    public string BrowsingSummaryText { get; set; } = "";

    public Task<IReadOnlyList<SimilarityMatchResult>> CheckSimilarityAsync(
        IReadOnlyList<(string Id, string Content)> submissions, double threshold, CancellationToken ct = default)
        => Task.FromResult(SimilarityMatches);

    public Task<AutogradeSuggestionResult> SuggestAutogradeAsync(
        string content, IReadOnlyList<RubricCriterion> rubric, double maxScore, CancellationToken ct = default)
        => Task.FromResult(AutogradeResult);

    public Task<IReadOnlyList<SuspiciousFlagResult>> CheckSuspiciousBehaviourAsync(
        IReadOnlyList<TelemetryEventInput> events, double minConfidence, CancellationToken ct = default)
        => Task.FromResult(SuspiciousFlagResults);

    public Task<string> SummarizeBrowsingAsync(IReadOnlyList<BrowsingVisitInput> visits, CancellationToken ct = default)
        => Task.FromResult(BrowsingSummaryText);
}

using System.Net.Http.Json;

namespace BackendApi.Services;

public record AiContentDetectionResult(double AiLikelihoodScore, string? PredictionId);

// AIS-05: thin client for Pangram's external AI-generated-content-detection API. Unlike
// Copyleaks (AIS-02), Pangram's public API is a single synchronous scoring call — no
// webhook/async flow needed, the likelihood score comes back in the same request.
//
// Wire format below is best-effort against Pangram's public API docs — this environment
// has no live Pangram sandbox or credentials to verify against (same disclosed gap as
// AIS-02/CopyleaksClient, and the one flagged when AIS-03/04/07 were wired: "AIS-02/05
// are external-API-only and out of scope — no credentials available").
public interface IPangramClient
{
    Task<AiContentDetectionResult> DetectAsync(string content, CancellationToken ct = default);
}

public class PangramClient(HttpClient http, IConfiguration configuration) : IPangramClient
{
    private sealed record PangramRequestBody(string Text);
    private sealed record PangramResponseBody(double AiLikelihood, string? PredictionId);

    public async Task<AiContentDetectionResult> DetectAsync(string content, CancellationToken ct = default)
    {
        var apiKey = configuration["Pangram:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ExternalServiceNotConfiguredException("Pangram");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/predict")
        {
            Content = JsonContent.Create(new PangramRequestBody(content)),
        };
        request.Headers.Add("x-api-key", apiKey);

        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PangramResponseBody>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Pangram returned an empty response.");

        return new AiContentDetectionResult(body.AiLikelihood, body.PredictionId);
    }
}

using System.Text.Json;
using BackendApi.Services;

namespace BackendApi.Tests;

// AIS-02: a real call to Copyleaks isn't available in unit tests (no credentials/network
// dependency), so controller tests configure canned behavior here instead.
public class FakeCopyleaksClient : ICopyleaksClient
{
    public bool Configured { get; set; } = true;
    public string? LastScanId { get; private set; }
    public string? LastContent { get; private set; }
    public string? LastWebhookUrlTemplate { get; private set; }
    public PlagiarismScanResult WebhookResult { get; set; } = new(0, []);

    public Task SubmitScanAsync(string scanId, string content, string webhookUrlTemplate, CancellationToken ct = default)
    {
        if (!Configured)
        {
            throw new ExternalServiceNotConfiguredException("Copyleaks");
        }

        LastScanId = scanId;
        LastContent = content;
        LastWebhookUrlTemplate = webhookUrlTemplate;
        return Task.CompletedTask;
    }

    public PlagiarismScanResult ParseWebhookResult(JsonElement payload) => WebhookResult;
}

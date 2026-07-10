using BackendApi.Services;

namespace BackendApi.Tests;

// AIS-05: a real call to Pangram isn't available in unit tests (no credentials/network
// dependency), so controller tests configure canned behavior here instead.
public class FakePangramClient : IPangramClient
{
    public bool Configured { get; set; } = true;
    public string? LastContent { get; private set; }
    public AiContentDetectionResult Result { get; set; } = new(0, null);

    public Task<AiContentDetectionResult> DetectAsync(string content, CancellationToken ct = default)
    {
        if (!Configured)
        {
            throw new ExternalServiceNotConfiguredException("Pangram");
        }

        LastContent = content;
        return Task.FromResult(Result);
    }
}

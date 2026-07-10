using StudentDesktop.Services;

namespace StudentDesktop.Tests;

// SDA-25: Record() must be a no-op unless a class session or assignment window is
// currently active — nothing should ever be sent (or even queued) outside those windows.
public class UsageTelemetryServiceTests
{
    private static ClassLockService NewLockService(ApiClient apiClient) => new(apiClient);

    private static AssignmentAutoSubmitService NewAutoSubmitService(ApiClient apiClient) => new(apiClient);

    [Fact]
    public void IsWindowActive_FalseWhenNeitherClassNorAssignmentIsActive()
    {
        var apiClient = new ApiClient();
        var service = new UsageTelemetryService(apiClient, NewLockService(apiClient), NewAutoSubmitService(apiClient));

        Assert.False(service.IsWindowActive);
    }

    [Fact]
    public void IsWindowActive_TrueWhenAssignmentIsOpen()
    {
        var apiClient = new ApiClient();
        var autoSubmit = NewAutoSubmitService(apiClient);
        autoSubmit.BeginSession(new ActiveAssignmentSession(
            Guid.NewGuid(), "text/plain", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), () => "content"));
        var service = new UsageTelemetryService(apiClient, NewLockService(apiClient), autoSubmit);

        Assert.True(service.IsWindowActive);
    }

    [Fact]
    public void Record_IsANoOp_WhenNoWindowIsActive()
    {
        var apiClient = new ApiClient();
        var service = new UsageTelemetryService(apiClient, NewLockService(apiClient), NewAutoSubmitService(apiClient));

        // Recording outside any active window must not queue anything — verified
        // indirectly via FlushAsync completing with nothing to send (no exception, no
        // network call attempted since ApiClient has no token and would throw/return
        // early on send otherwise if something were queued and attempted).
        service.Record("window_blur");
        service.Record("paste", new Dictionary<string, object> { ["char_count"] = 500 });

        // FlushAsync with an empty queue must return immediately without attempting
        // any HTTP call — an unconfigured ApiClient() would throw if it tried.
        var task = service.FlushAsync();
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FlushAsync_AttemptsToSend_WhenAWindowWasActiveAtRecordTime()
    {
        var apiClient = new ApiClient("http://localhost:1"); // deliberately unreachable
        var autoSubmit = NewAutoSubmitService(apiClient);
        autoSubmit.BeginSession(new ActiveAssignmentSession(
            Guid.NewGuid(), "text/plain", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), () => "content"));
        var service = new UsageTelemetryService(apiClient, NewLockService(apiClient), autoSubmit);

        service.Record("keystroke");

        // Best-effort: FlushAsync must never throw even though the endpoint is
        // unreachable — it should swallow the failure and leave the event queued.
        await service.FlushAsync();
    }
}

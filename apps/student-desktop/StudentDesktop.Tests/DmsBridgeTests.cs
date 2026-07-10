using System.Text.Json;
using StudentDesktop.Services;

namespace StudentDesktop.Tests;

public class DmsBridgeTests
{
    private static (DmsBridge Bridge, Func<string?> LastScript) NewBridge()
    {
        string? lastScript = null;
        var bridge = new DmsBridge(new ApiClient("http://localhost:0"))
        {
            InvokeScript = script =>
            {
                lastScript = script;
                return Task.CompletedTask;
            },
        };
        return (bridge, () => lastScript);
    }

    // SDA-24: no reachable server, so every request must fail closed with a mapped
    // DmsError rather than throwing out of HandleMessageAsync and crashing the WebView
    // message pump.
    [Fact]
    public async Task Sda24_HandleMessage_ListThreads_WithNoReachableServer_RespondsWithNetworkError()
    {
        var (bridge, lastScript) = NewBridge();
        var payload = JsonSerializer.Serialize(new { requestId = "list-1", method = "listThreads", payload = new { } });

        await bridge.HandleMessageAsync(payload);

        var script = lastScript();
        Assert.NotNull(script);
        Assert.Contains("list-1", script);
        Assert.Contains("\"ok\":false", script);
        Assert.Contains("network_error", script);
    }

    [Fact]
    public async Task Sda24_HandleMessage_ListMessages_WithNoReachableServer_RespondsWithNetworkError()
    {
        var (bridge, lastScript) = NewBridge();
        var payload = JsonSerializer.Serialize(new { requestId = "msgs-1", method = "listMessages", payload = new { threadId = Guid.NewGuid() } });

        await bridge.HandleMessageAsync(payload);

        var script = lastScript();
        Assert.NotNull(script);
        Assert.Contains("msgs-1", script);
        Assert.Contains("\"ok\":false", script);
    }

    [Fact]
    public async Task Sda24_HandleMessage_SendMessage_WithNoReachableServer_RespondsWithNetworkError()
    {
        var (bridge, lastScript) = NewBridge();
        var payload = JsonSerializer.Serialize(new
        {
            requestId = "send-1",
            method = "sendMessage",
            payload = new { threadId = Guid.NewGuid(), content = "hi" },
        });

        await bridge.HandleMessageAsync(payload);

        var script = lastScript();
        Assert.NotNull(script);
        Assert.Contains("send-1", script);
        Assert.Contains("\"ok\":false", script);
    }

    [Fact]
    public async Task Sda24_HandleMessage_UnknownMethod_RespondsWithValidationError()
    {
        var (bridge, lastScript) = NewBridge();
        var payload = JsonSerializer.Serialize(new { requestId = "req-1", method = "bogus", payload = new { } });

        await bridge.HandleMessageAsync(payload);

        var script = lastScript();
        Assert.NotNull(script);
        Assert.Contains("req-1", script);
        Assert.Contains("validation_error", script);
    }

    [Fact]
    public async Task Sda24_MountInbox_WithNoInvokeScriptWired_DoesNotThrow()
    {
        var bridge = new DmsBridge(new ApiClient("http://localhost:0"));

        var exception = await Record.ExceptionAsync(() => bridge.MountInboxAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Sda24_MountInbox_InvokesHostMountWithUserContext()
    {
        var (bridge, lastScript) = NewBridge();
        var userId = Guid.NewGuid();

        await bridge.MountInboxAsync(userId);

        var script = lastScript();
        Assert.NotNull(script);
        Assert.Contains("__dmsHostMount", script);
        Assert.Contains(userId.ToString(), script);
        Assert.Contains("\"role\":\"student\"", script);
    }
}

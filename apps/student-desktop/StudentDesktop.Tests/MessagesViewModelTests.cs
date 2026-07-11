using StudentDesktop.Services;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class MessagesViewModelTests
{
    [Fact]
    public void Sda24_Construction_WithNoReachableServer_DoesNotThrow()
    {
        var exception = Record.Exception(() => new MessagesViewModel(new ApiClient("http://localhost:0"), Guid.NewGuid()));

        Assert.Null(exception);
    }

    // SDA-24: MountAsync is deferred to the View wiring InvokeScript after DataContext is
    // set (see MessagesViewModel), not called from the constructor — this guards against
    // the mount call silently no-op'ing before InvokeScript exists.
    [Fact]
    public async Task Sda24_MountAsync_WithInvokeScriptWired_InvokesHostMount()
    {
        var viewModel = new MessagesViewModel(new ApiClient("http://localhost:0"), Guid.NewGuid());
        string? lastScript = null;
        viewModel.Bridge.InvokeScript = script =>
        {
            lastScript = script;
            return Task.CompletedTask;
        };

        await viewModel.MountAsync();

        Assert.NotNull(lastScript);
        Assert.Contains("__dmsHostMount", lastScript);
    }
}

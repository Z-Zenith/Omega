using StudentDesktop.Services;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class NotesViewModelTests
{
    // SDA-19: no reachable server, so the initial note-list load must fail closed with an
    // ErrorMessage rather than throwing out of the constructor's fire-and-forget load.
    [Fact]
    public void Sda19_Construction_WithNoReachableServer_DoesNotThrow()
    {
        var exception = Record.Exception(() => new NotesViewModel(new ApiClient("http://localhost:0"), Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public void Sda19_Construction_StartsWithNoSelectedNote()
    {
        var viewModel = new NotesViewModel(new ApiClient("http://localhost:0"), Guid.NewGuid());

        Assert.Null(viewModel.SelectedNote);
        Assert.Empty(viewModel.Notes);
    }

    [Fact]
    public async Task Sda19_NewNote_ClearsSelectionAndMountsABlankEditor()
    {
        var viewModel = new NotesViewModel(new ApiClient("http://localhost:0"), Guid.NewGuid());
        string? lastScript = null;
        viewModel.Bridge.InvokeScript = script =>
        {
            lastScript = script;
            return Task.CompletedTask;
        };
        viewModel.SelectedNote = new Models.NoteSummaryDto(Guid.NewGuid(), "Some note", DateTime.UtcNow);

        await viewModel.NewNoteCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedNote);
        Assert.NotNull(lastScript);
        Assert.Contains("\"currentNote\":null", lastScript);
    }
}

using StudentDesktop.Services;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class CommunityViewModelTests
{
    // SDA-16: no reachable server, so the initial groups load must fail closed with an
    // ErrorMessage rather than throwing out of the constructor's fire-and-forget load.
    [Fact]
    public void Sda16_Construction_WithNoReachableServer_DoesNotThrow()
    {
        var exception = Record.Exception(() => new CommunityViewModel(new ApiClient("http://localhost:0")));

        Assert.Null(exception);
    }

    [Fact]
    public void Sda16_Construction_StartsWithNoSelectedGroup()
    {
        var viewModel = new CommunityViewModel(new ApiClient("http://localhost:0"));

        Assert.Null(viewModel.SelectedGroup);
        Assert.Empty(viewModel.Groups);
    }

    [Fact]
    public async Task Sda16_Post_WithNoSelectedGroup_DoesNothing()
    {
        var viewModel = new CommunityViewModel(new ApiClient("http://localhost:0")) { NewPostContent = "hello" };

        var exception = await Record.ExceptionAsync(() => viewModel.PostCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Empty(viewModel.Posts);
    }

    [Fact]
    public async Task Sda16_Post_WithBlankContent_DoesNothing()
    {
        var viewModel = new CommunityViewModel(new ApiClient("http://localhost:0"))
        {
            SelectedGroup = new Models.GroupDto(Guid.NewGuid(), "Club", "Club", null),
            NewPostContent = "   ",
        };

        await viewModel.PostCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Posts);
    }
}

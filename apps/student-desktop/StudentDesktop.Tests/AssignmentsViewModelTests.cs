using StudentDesktop.Services;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class AssignmentsViewModelTests
{
    [Fact]
    public void Sda10_StartsWithNoLastSubmissionOrError()
    {
        var viewModel = new AssignmentsViewModel(new ApiClient("http://localhost:0"));

        Assert.Null(viewModel.LastSubmission);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Sda10_Submit_RejectsInvalidAssignmentId()
    {
        var viewModel = new AssignmentsViewModel(new ApiClient("http://localhost:0"))
        {
            AssignmentId = "not-a-guid",
            ContentUrl = "print('hi')",
        };

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Enter a valid assignment ID.", viewModel.ErrorMessage);
        Assert.Null(viewModel.LastSubmission);
    }

    [Fact]
    public async Task Sda10_Submit_RejectsBlankContent()
    {
        var viewModel = new AssignmentsViewModel(new ApiClient("http://localhost:0"))
        {
            AssignmentId = Guid.NewGuid().ToString(),
            ContentUrl = "   ",
        };

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Enter your submission content.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Sda10_Submit_WithNoReachableServer_SurfacesNetworkError()
    {
        var viewModel = new AssignmentsViewModel(new ApiClient("http://localhost:0"))
        {
            AssignmentId = Guid.NewGuid().ToString(),
            ContentUrl = "print('hi')",
        };

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("Could not reach the server. Check your connection and try again.", viewModel.ErrorMessage);
    }
}

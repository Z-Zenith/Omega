using StudentDesktop.Services;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class CoursesViewModelTests
{
    [Fact]
    public void Sda18_Construction_WithNoReachableServer_DoesNotThrow()
    {
        var exception = Record.Exception(() => new CoursesViewModel(new ApiClient("http://localhost:0")));

        Assert.Null(exception);
    }

    [Fact]
    public void Sda18_Construction_StartsWithNoSelectedCourse()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"));

        Assert.Null(viewModel.SelectedCourse);
        Assert.Empty(viewModel.Courses);
    }

    // SDA-17
    [Fact]
    public async Task Sda17_SubmitFeedback_WithNoSelectedCourse_SetsErrorAndDoesNotThrow()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"));

        await viewModel.SubmitFeedbackCommand.ExecuteAsync(null);

        Assert.Equal("Select a course with an assigned teacher first.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Sda17_SubmitFeedback_WithCourseThatHasNoTeacher_SetsError()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"))
        {
            SelectedCourse = new Models.CourseInfoDto(Guid.NewGuid(), "CS101", "Intro", null, null),
        };

        await viewModel.SubmitFeedbackCommand.ExecuteAsync(null);

        Assert.Equal("Select a course with an assigned teacher first.", viewModel.ErrorMessage);
    }
}

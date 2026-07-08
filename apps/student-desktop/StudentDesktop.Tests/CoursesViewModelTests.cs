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

    // SDA-17: defaulting to 5 would bias any unattended submission toward the highest
    // rating — the student must deliberately pick a value.
    [Fact]
    public void Sda17_Construction_DoesNotDefaultFeedbackRatingToAValidValue()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"));

        Assert.True(viewModel.FeedbackRating is < 1 or > 5);
    }

    // SDA-17: an unset rating should be rejected by the same validation as any other
    // out-of-range rating, rather than silently submitting.
    [Fact]
    public async Task Sda17_SubmitFeedback_WithUnsetRating_SetsErrorAndDoesNotSubmit()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"))
        {
            SelectedCourse = new Models.CourseInfoDto(Guid.NewGuid(), "CS101", "Intro", Guid.NewGuid(), "Teacher"),
        };

        await viewModel.SubmitFeedbackCommand.ExecuteAsync(null);

        Assert.Equal("Rating must be between 1 and 5.", viewModel.ErrorMessage);
    }

    // SDA-17: teacher_feedback has no unique constraint, so the command must refuse to
    // run again while a submission is already in flight, or a double-click creates a
    // duplicate row.
    [Fact]
    public void Sda17_SubmitFeedbackCommand_CanExecute_IsFalseWhileBusy()
    {
        var viewModel = new CoursesViewModel(new ApiClient("http://localhost:0"))
        {
            IsBusy = true,
        };

        Assert.False(viewModel.SubmitFeedbackCommand.CanExecute(null));
    }
}

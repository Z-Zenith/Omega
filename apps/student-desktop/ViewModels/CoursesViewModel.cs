using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-18: course & teacher info per enrolled subject. SDA-17: submit feedback about a
// teacher/course from the same screen — feedback is attributed to whichever course row
// the student picked, resolving TeacherId for them rather than asking for a raw GUID.
public partial class CoursesViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<CourseInfoDto> Courses { get; } = [];

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private CourseInfoDto? _selectedCourse;

    // No default (0 = unset) — defaulting to 5 would bias any unattended/accidental
    // submission toward the highest possible rating. The student must deliberately pick
    // one of 1-5; SubmitFeedbackAsync's existing range check rejects 0.
    [ObservableProperty]
    private int _feedbackRating;

    [ObservableProperty]
    private string _feedbackComments = "";

    [ObservableProperty]
    private string? _feedbackMessage;

    // Gates SubmitFeedbackCommand (see CanSubmitFeedback below) — teacher_feedback has no
    // unique constraint, so without this guard a double-click (or a click that lands
    // before the button visually disables) creates duplicate feedback rows.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitFeedbackCommand))]
    private bool _isBusy;

    public CoursesViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadCoursesAsync();
    }

    [RelayCommand]
    private async Task LoadCoursesAsync()
    {
        try
        {
            var courses = await _apiClient.GetMySubjectsAsync();
            Courses.Clear();
            foreach (var course in courses)
            {
                Courses.Add(course);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
    }

    private bool CanSubmitFeedback() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSubmitFeedback))]
    private async Task SubmitFeedbackAsync()
    {
        FeedbackMessage = null;
        if (SelectedCourse?.TeacherId is not { } teacherId)
        {
            ErrorMessage = "Select a course with an assigned teacher first.";
            return;
        }
        if (FeedbackRating is < 1 or > 5)
        {
            ErrorMessage = "Rating must be between 1 and 5.";
            return;
        }

        IsBusy = true;
        try
        {
            await _apiClient.SubmitTeacherFeedbackAsync(teacherId, FeedbackRating, string.IsNullOrWhiteSpace(FeedbackComments) ? null : FeedbackComments.Trim());
            FeedbackMessage = "Feedback submitted.";
            // Reset so a subsequent click submits a deliberate new choice rather than
            // silently repeating the same rating.
            FeedbackRating = 0;
            FeedbackComments = "";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

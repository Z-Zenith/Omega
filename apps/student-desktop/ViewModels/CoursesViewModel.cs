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

    [ObservableProperty]
    private int _feedbackRating = 5;

    [ObservableProperty]
    private string _feedbackComments = "";

    [ObservableProperty]
    private string? _feedbackMessage;

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

    [RelayCommand]
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

        try
        {
            await _apiClient.SubmitTeacherFeedbackAsync(teacherId, FeedbackRating, string.IsNullOrWhiteSpace(FeedbackComments) ? null : FeedbackComments.Trim());
            FeedbackMessage = "Feedback submitted.";
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
    }
}

using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-10: submit an assignment in the format matching its type (code, quiz, essay, file
// upload) and record a submission timestamp. Late/format-mismatch enforcement lives
// server-side (AssignmentsController.Submit) — this just forwards and surfaces the result.
public partial class AssignmentsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    [ObservableProperty]
    private string _assignmentId = "";

    [ObservableProperty]
    private string _submissionFormat = "Code";

    [ObservableProperty]
    private string _contentUrl = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private SubmissionDto? _lastSubmission;

    public AssignmentsViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        LastSubmission = null;

        if (!Guid.TryParse(AssignmentId.Trim(), out var assignmentId))
        {
            ErrorMessage = "Enter a valid assignment ID.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ContentUrl))
        {
            ErrorMessage = "Enter your submission content.";
            return;
        }

        try
        {
            LastSubmission = await _apiClient.SubmitAssignmentAsync(assignmentId, ContentUrl.Trim(), SubmissionFormat);
            ContentUrl = "";
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

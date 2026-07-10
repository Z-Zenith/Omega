using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-18: course and teacher info for every subject taught to a section the student is
// enrolled in.
public partial class CourseInfoViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<MySubjectDto> Subjects { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public CourseInfoViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var subjects = await _apiClient.GetMySubjectsAsync();
            Subjects.Clear();
            foreach (var subject in subjects)
            {
                Subjects.Add(subject);
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
        finally
        {
            IsBusy = false;
        }
    }
}

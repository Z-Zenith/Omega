using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Models;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-15: shows only published marks — the backend never returns unpublished ones for this student.
public partial class MarksViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<InternalMarkDto> InternalMarks { get; } = [];

    public ObservableCollection<ExternalMarkDto> ExternalMarks { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public MarksViewModel(ApiClient apiClient)
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
            var marks = await _apiClient.GetMyMarksAsync();
            InternalMarks.Clear();
            foreach (var mark in marks.InternalMarks)
            {
                InternalMarks.Add(mark);
            }

            ExternalMarks.Clear();
            foreach (var mark in marks.ExternalMarks)
            {
                ExternalMarks.Add(mark);
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

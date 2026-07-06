using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class EventsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public EventsViewModel(ApiClient apiClient)
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
            var events = await _apiClient.ListEventsAsync();
            Events.Clear();
            foreach (var e in events)
            {
                Events.Add(new EventItemViewModel(_apiClient, e.Id, e.Title, e.StartTime, e.EndTime, e.IsRegistered));
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

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly Action _onSignOut;

    public string FullName { get; }

    public CalendarViewModel CalendarViewModel { get; }
    public EventsViewModel EventsViewModel { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public ShellViewModel(ApiClient apiClient, string fullName, Action onSignOut)
    {
        _apiClient = apiClient;
        _onSignOut = onSignOut;
        FullName = fullName;
        CalendarViewModel = new CalendarViewModel(apiClient);
        EventsViewModel = new EventsViewModel(apiClient);
        _currentPage = CalendarViewModel;
    }

    [RelayCommand]
    private void ShowCalendar() => CurrentPage = CalendarViewModel;

    [RelayCommand]
    private void ShowEvents() => CurrentPage = EventsViewModel;

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _apiClient.LogoutAsync();
        }
        catch (ApiException)
        {
            // Best-effort — the local session is cleared regardless of server-side outcome.
        }
        _onSignOut();
    }
}

using System;
using System.Net.Http;
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
    public ChangePasswordViewModel ChangePasswordViewModel { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public ShellViewModel(ApiClient apiClient, string fullName, Action onSignOut)
    {
        _apiClient = apiClient;
        _onSignOut = onSignOut;
        FullName = fullName;
        CalendarViewModel = new CalendarViewModel(apiClient);
        EventsViewModel = new EventsViewModel(apiClient);
        ChangePasswordViewModel = new ChangePasswordViewModel(apiClient);
        _currentPage = CalendarViewModel;
    }

    [RelayCommand]
    private void ShowCalendar() => CurrentPage = CalendarViewModel;

    [RelayCommand]
    private void ShowEvents() => CurrentPage = EventsViewModel;

    [RelayCommand]
    private void ShowChangePassword() => CurrentPage = ChangePasswordViewModel;

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _apiClient.LogoutAsync();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // Best-effort — the local session is cleared regardless of server-side outcome.
        }
        _onSignOut();
    }
}

using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly ApiClient _apiClient;
    private readonly Action _onSignOut;
    private bool _disposed;

    public string FullName { get; }

    public CalendarViewModel CalendarViewModel { get; }
    public EventsViewModel EventsViewModel { get; }
    public ChangePasswordViewModel ChangePasswordViewModel { get; }
    public MarksViewModel MarksViewModel { get; }
    public BrowserViewModel BrowserViewModel { get; }

    // SDA-01: owned for the lifetime of the signed-in session. Started here so the
    // class-time lock is active as soon as the student is logged in, and stopped/
    // disposed on sign-out so no restriction (and no timer) lingers past the session.
    public ClassLockService ClassLockService { get; }

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
        MarksViewModel = new MarksViewModel(apiClient);
        BrowserViewModel = new BrowserViewModel(apiClient);
        _currentPage = CalendarViewModel;

        ClassLockService = new ClassLockService(apiClient);
        ClassLockService.Start();
    }

    [RelayCommand]
    private void ShowCalendar() => CurrentPage = CalendarViewModel;

    [RelayCommand]
    private void ShowEvents() => CurrentPage = EventsViewModel;

    [RelayCommand]
    private void ShowChangePassword() => CurrentPage = ChangePasswordViewModel;

    [RelayCommand]
    private void ShowMarks() => CurrentPage = MarksViewModel;

    [RelayCommand]
    private void ShowBrowser() => CurrentPage = BrowserViewModel;

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
        Dispose();
        _onSignOut();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ClassLockService.Dispose();
    }
}

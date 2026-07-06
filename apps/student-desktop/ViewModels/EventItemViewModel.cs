using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-20: students browse eligible events and register from this screen.
public partial class EventItemViewModel(ApiClient apiClient, Guid id, string title, DateTime startTime, DateTime endTime, bool isRegistered)
    : ObservableObject
{
    public Guid Id { get; } = id;
    public string Title { get; } = title;
    public DateTime StartTime { get; } = startTime;
    public DateTime EndTime { get; } = endTime;

    [ObservableProperty]
    private bool _isRegistered = isRegistered;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await apiClient.RegisterForEventAsync(Id);
            IsRegistered = true;
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

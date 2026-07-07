using CommunityToolkit.Mvvm.ComponentModel;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient = new();

    // SDA-12: exposed so the window's code-behind can fire an exit-ping on focus-loss/close
    // without the view needing its own copy of session/auth state.
    public ApiClient ApiClient => _apiClient;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        _currentPage = CreateLoginPage();
    }

    private LoginViewModel CreateLoginPage() => new(_apiClient, response =>
    {
        CurrentPage = new ShellViewModel(_apiClient, response.FullName, () => CurrentPage = CreateLoginPage());
    });
}

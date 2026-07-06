using CommunityToolkit.Mvvm.ComponentModel;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient = new();

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

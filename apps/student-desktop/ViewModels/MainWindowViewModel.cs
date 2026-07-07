using CommunityToolkit.Mvvm.ComponentModel;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient = new();

    // SDA-11: shared across the app's lifetime so it survives sign-out/sign-in and can
    // be attached once to the main window in App.axaml.cs. No assignment-editing view
    // calls BeginSession on it yet — see AssignmentAutoSubmitService for details.
    public AssignmentAutoSubmitService AutoSubmitService { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        AutoSubmitService = new AssignmentAutoSubmitService(_apiClient);
        _currentPage = CreateLoginPage();
    }

    private LoginViewModel CreateLoginPage() => new(_apiClient, response =>
    {
        CurrentPage = new ShellViewModel(_apiClient, response.FullName, () => CurrentPage = CreateLoginPage(), AutoSubmitService);
    });
}

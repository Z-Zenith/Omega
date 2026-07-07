using StudentDesktop.ViewModels;

namespace StudentDesktop.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void StartsOnTheLoginPage()
    {
        var viewModel = new MainWindowViewModel();

        Assert.IsType<LoginViewModel>(viewModel.CurrentPage);
    }
}

using Avalonia.Controls;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Deactivated += OnDeactivated;
        Closing += OnClosing;
    }

    // SDA-12: losing effective focus (alt-tabbing away, switching virtual desktops, etc.)
    // or closing the app are both "exit" events for this feature. The server-side
    // ClassSessionLookup is the sole authority on whether a scheduled class session is
    // actually active for this student right now, so the client doesn't need its own
    // timetable check — it just always fires the ping, and it's a no-op server-side when
    // there's nothing to notify about.
    private void OnDeactivated(object? sender, System.EventArgs e) => FireExitPing();

    private void OnClosing(object? sender, WindowClosingEventArgs e) => FireExitPing();

    private void FireExitPing()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            _ = viewModel.ApiClient.ExitPingAsync();
        }
    }
}
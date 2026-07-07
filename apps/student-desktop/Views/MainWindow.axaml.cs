using System;
using Avalonia.Controls;
using Avalonia.Threading;
using StudentDesktop.Services;
using StudentDesktop.Services.Interop;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Views;

public partial class MainWindow : Window
{
    // SDA-01: the lock service lives on ShellViewModel (session-scoped); this window
    // only reacts to its LockStateChanged event to enforce/lift full-screen + the
    // OS-level app-switch block. Outside class hours (or before login / after
    // sign-out) none of this is active and the window behaves normally.
    private ClassLockService? _classLockService;
    private AppSwitchBlocker? _appSwitchBlocker;
    private WindowState _preLockWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Deactivated += OnDeactivated;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.CurrentPage))
                {
                    AttachToCurrentPage(viewModel.CurrentPage);
                }
            };
            AttachToCurrentPage(viewModel.CurrentPage);
        }
    }

    private void AttachToCurrentPage(ViewModelBase page)
    {
        if (_classLockService is not null)
        {
            _classLockService.LockStateChanged -= OnLockStateChanged;
            _classLockService = null;
        }

        if (page is ShellViewModel shell)
        {
            _classLockService = shell.ClassLockService;
            _classLockService.LockStateChanged += OnLockStateChanged;
            ApplyLockState(_classLockService.IsLocked);
        }
        else
        {
            // Logged out (or not logged in yet): no timetable to check against, so
            // there must be zero restriction.
            ApplyLockState(false);
        }
    }

    private void OnLockStateChanged(object? sender, bool isLocked) =>
        Dispatcher.UIThread.Post(() => ApplyLockState(isLocked));

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Best-effort re-focus if something manages to steal activation while a
        // class session is active (e.g. another window briefly grabbing focus).
        if (_classLockService?.IsLocked == true)
        {
            Dispatcher.UIThread.Post(Activate);
        }
    }

    private void ApplyLockState(bool locked)
    {
        if (locked)
        {
            if (WindowState != WindowState.FullScreen)
            {
                _preLockWindowState = WindowState;
            }
            WindowDecorations = WindowDecorations.None;
            Topmost = true;
            WindowState = WindowState.FullScreen;
            Activate();

            if (OperatingSystem.IsWindows())
            {
                _appSwitchBlocker ??= new AppSwitchBlocker();
                _appSwitchBlocker.Start();
            }
        }
        else
        {
            if (OperatingSystem.IsWindows())
            {
                _appSwitchBlocker?.Stop();
            }
            Topmost = false;
            WindowDecorations = WindowDecorations.Full;
            WindowState = _preLockWindowState;
        }
    }
}

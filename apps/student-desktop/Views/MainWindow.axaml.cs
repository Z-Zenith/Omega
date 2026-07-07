using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private readonly IAppClipboardService _clipboard = AppClipboardService.Instance;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Deactivated += OnDeactivated;

        // SDA-21: intercept every copy/cut/paste from any TextBox in the visual tree (these
        // events bubble up from wherever the control lives — Login, Shell, Calendar, Events,
        // and any future view) and reroute them through the app-internal clipboard instead of
        // the OS clipboard. Marking the event Handled stops TextBox's own Copy()/Cut()/Paste()
        // from ever calling TopLevel.Clipboard, whether triggered by Ctrl+C/X/V or by the
        // built-in right-click context menu (both paths raise these same routed events first).
        AddHandler(TextBox.CopyingToClipboardEvent, OnCopyingToClipboard);
        AddHandler(TextBox.CuttingToClipboardEvent, OnCuttingToClipboard);
        AddHandler(TextBox.PastingFromClipboardEvent, OnPastingFromClipboard);
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

    private void OnCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        // e.Source is the TextBox that originated the event; sender is whatever element this
        // handler happens to be attached to (the Window), which stays constant as the event
        // bubbles — so the TextBox itself must be read from Source, not sender.
        if (e.Source is TextBox textBox)
        {
            var selection = textBox.SelectedText;
            if (!string.IsNullOrEmpty(selection))
            {
                _clipboard.SetText(selection);
            }

            // Prevent TextBox.Copy() from also writing the selection to the real OS clipboard.
            e.Handled = true;
        }
    }

    private void OnCuttingToClipboard(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
        {
            var selection = textBox.SelectedText;
            if (!string.IsNullOrEmpty(selection))
            {
                _clipboard.SetText(selection);
                // Mirrors TextBox.Cut(): remove the selection now that we've captured it
                // ourselves, since the default handling (which also removes it) won't run.
                textBox.SelectedText = string.Empty;
            }

            e.Handled = true;
        }
    }

    private void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
        {
            var text = _clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                // Mirrors TextBox.Paste(): insert at the caret / replace the current selection.
                textBox.SelectedText = text;
            }

            // Prevent TextBox.Paste() from also reading from the real OS clipboard.
            e.Handled = true;
        }
    }
}

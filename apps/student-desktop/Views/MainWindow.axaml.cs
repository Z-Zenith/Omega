using System;
using System.Collections.Generic;
using System.Threading;
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

    private UsageTelemetryService? _telemetryService;

    // SDA-22: set from App.axaml.cs (same lifetime/wiring as AttachTo for auto-submit).
    // Null before that wiring happens (e.g. design-time), in which case clipboard actions
    // are never blocked — matches "no assignment open" being the safe default.
    private AssignmentAutoSubmitService? _autoSubmitService;
    private CancellationTokenSource? _blockedNoticeHideCts;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Deactivated += OnDeactivated;
        Deactivated += (_, _) => _telemetryService?.Record("window_blur");
        Activated += (_, _) => _telemetryService?.Record("window_focus");

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
            // SDA-22: same lifetime as the window's DataContext — set once, survives
            // sign-out/sign-in like AutoSubmitService itself does.
            _autoSubmitService = viewModel.AutoSubmitService;

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
            _telemetryService = shell.UsageTelemetryService;
        }
        else
        {
            // Logged out (or not logged in yet): no timetable to check against, so
            // there must be zero restriction.
            ApplyLockState(false);
            _telemetryService = null;
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

    // SDA-22: while an assignment is open for editing, ALL clipboard actions are blocked —
    // including the isolated in-app clipboard from SDA-21, not just the OS one. Each handler
    // below checks this first and, if blocked, does nothing but mark the event handled and
    // show the notice; it never touches _clipboard at all in that case.
    private bool IsClipboardBlocked => _autoSubmitService?.IsAssignmentOpen == true;

    private void OnCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        // e.Source is the TextBox that originated the event; sender is whatever element this
        // handler happens to be attached to (the Window), which stays constant as the event
        // bubbles — so the TextBox itself must be read from Source, not sender.
        if (e.Source is TextBox textBox)
        {
            if (IsClipboardBlocked)
            {
                ShowClipboardBlockedNotice();
                e.Handled = true;
                return;
            }

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
            if (IsClipboardBlocked)
            {
                ShowClipboardBlockedNotice();
                e.Handled = true;
                return;
            }

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
            if (IsClipboardBlocked)
            {
                ShowClipboardBlockedNotice();
                e.Handled = true;
                return;
            }

            var text = _clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                // Mirrors TextBox.Paste(): insert at the caret / replace the current selection.
                textBox.SelectedText = text;
                // SDA-25: paste char_count feeds AIS-07's large-paste-burst heuristic.
                _telemetryService?.Record("paste", new Dictionary<string, object> { ["char_count"] = text.Length });
            }

            // Prevent TextBox.Paste() from also reading from the real OS clipboard.
            e.Handled = true;
        }
    }

    private void ShowClipboardBlockedNotice()
    {
        _blockedNoticeHideCts?.Cancel();
        var cts = new CancellationTokenSource();
        _blockedNoticeHideCts = cts;

        ClipboardBlockedNotice.IsVisible = true;

        _ = HideNoticeAfterDelayAsync(cts.Token);
    }

    private async System.Threading.Tasks.Task HideNoticeAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2), token);
            if (!token.IsCancellationRequested)
            {
                ClipboardBlockedNotice.IsVisible = false;
            }
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // Superseded by a newer blocked attempt — that one owns hiding the notice.
        }
    }
}

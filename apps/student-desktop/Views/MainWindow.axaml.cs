using Avalonia.Controls;
using Avalonia.Interactivity;
using StudentDesktop.Services;

namespace StudentDesktop.Views;

public partial class MainWindow : Window
{
    private readonly IAppClipboardService _clipboard = AppClipboardService.Instance;

    public MainWindow()
    {
        InitializeComponent();

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

namespace StudentDesktop.Services;

// SDA-21: in-memory, app-scoped clipboard. This intentionally never calls the OS clipboard
// (no Avalonia.Input.Platform.IClipboard / TopLevel.Clipboard usage anywhere in this type) —
// the content lives only in this process's memory for the lifetime of the app, exactly like
// a real clipboard, but isolated from every other application on the machine.
//
// There is no DI container in this app (services are constructed and threaded through
// view-model constructors by hand — see MainWindowViewModel), so this is exposed as a single
// process-wide instance, matching how a clipboard is a single shared resource in practice.
public sealed class AppClipboardService : IAppClipboardService
{
    public static AppClipboardService Instance { get; } = new();

    private string? _text;

    private AppClipboardService()
    {
    }

    public bool HasText => !string.IsNullOrEmpty(_text);

    public void SetText(string? text) => _text = text;

    public string? GetText() => _text;

    public void Clear() => _text = null;
}

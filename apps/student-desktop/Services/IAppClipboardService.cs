namespace StudentDesktop.Services;

// SDA-21: contract for the app-internal clipboard. Implementations must never touch the
// OS clipboard (Avalonia.Input.Platform.IClipboard / TopLevel.Clipboard) — content copied
// inside the app must stay inside the app, and the app must never read from the real
// OS clipboard.
public interface IAppClipboardService
{
    bool HasText { get; }

    void SetText(string? text);

    string? GetText();

    void Clear();
}

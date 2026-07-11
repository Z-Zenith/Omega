using System;
using System.Threading.Tasks;
using StudentDesktop.Services;

namespace StudentDesktop.ViewModels;

// SDA-24: embeds DMS-01's MessageInbox + MessageThreadView for all messaging with
// teachers, rather than the Student Desktop App implementing its own inbox/thread UI.
// This ViewModel only owns bridge mounting; the inbox/thread state and rendering live
// entirely in DMS, hosted by MessagesView's NativeWebView and driven through DmsBridge.
public partial class MessagesViewModel : ViewModelBase
{
    private readonly Guid _userId;

    public DmsBridge Bridge { get; }

    public MessagesViewModel(ApiClient apiClient, Guid userId)
    {
        _userId = userId;
        Bridge = new DmsBridge(apiClient);
    }

    // Called by MessagesView once its NativeWebView's InvokeScript delegate is wired —
    // calling this from the constructor would race the View wiring InvokeScript after
    // DataContext is set, and the mount call would silently no-op (same reasoning as
    // SDA-19's NotesViewModel deferring MountNotesEditorAsync to user-driven selection).
    public Task MountAsync() => Bridge.MountInboxAsync(_userId);
}

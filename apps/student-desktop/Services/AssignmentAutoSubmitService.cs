using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace StudentDesktop.Services;

// SDA-11: describes the assignment a student currently has open for editing, so
// AssignmentAutoSubmitService knows what to submit if the app exits or loses focus
// while its submission window is still active. Any future assignment-editing
// ViewModel constructs one of these and hands it to BeginSession/EndSession.
public record ActiveAssignmentSession(
    Guid AssignmentId,
    string SubmissionFormat,
    DateTime SubmissionWindowStart,
    DateTime SubmissionWindowEnd,
    Func<string?> GetCurrentContentUrl);

// SDA-11: "Auto-submit on exit during assignment window". Reusable, UI-agnostic
// service — attach it once to the main window and any assignment-editing view can
// register/unregister the assignment it's currently working on via BeginSession /
// EndSession. No assignment-editing view exists in student-desktop yet, so this
// service has no callers of BeginSession today; it's wired up so the first such view
// only needs to call it, not reimplement exit/focus-loss detection.
public class AssignmentAutoSubmitService(ApiClient apiClient)
{
    private ActiveAssignmentSession? _session;

    // SDA-22/SDA-25: true while an assignment is open for editing — the single source of
    // truth both the clipboard block (SDA-22) and usage telemetry (SDA-25) key off, so
    // there is exactly one notion of "an assignment is currently open" in the app.
    public bool IsAssignmentOpen => _session is not null;

    // Called by an assignment-editing view when the student opens an assignment.
    public void BeginSession(ActiveAssignmentSession session) => _session = session;

    // Called by an assignment-editing view on manual submit / navigating away cleanly,
    // so a later exit/focus-loss doesn't try to auto-submit a finished assignment.
    public void EndSession(Guid assignmentId)
    {
        if (_session?.AssignmentId == assignmentId)
        {
            _session = null;
        }
    }

    // Wires exit and focus-loss detection to the given main window. Safe to call even
    // when no session is active yet — BeginSession has not been called by anything, so
    // the handlers below are no-ops until a future assignment view registers one.
    public void AttachTo(Window window)
    {
        window.Deactivated += (_, _) => TriggerAutoSubmit();
        window.Closing += (_, _) => TriggerAutoSubmit();
    }

    private void TriggerAutoSubmit()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < session.SubmissionWindowStart || now > session.SubmissionWindowEnd)
        {
            // Outside the active window — SDA-11 only fires "during an active
            // assignment window", not before it opens or after it has already closed.
            return;
        }

        // Fire-and-forget: Window.Closing/Deactivated are not awaitable, and the app
        // may be tearing down. Best-effort delivery is the acceptance criterion here —
        // there is no user left to show an error to.
        _ = TryAutoSubmitAsync(session);
    }

    private async Task TryAutoSubmitAsync(ActiveAssignmentSession session)
    {
        try
        {
            var content = session.GetCurrentContentUrl();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            await apiClient.AutoSubmitAssignmentAsync(session.AssignmentId, content, session.SubmissionFormat);

            // Prevent a second auto-submit if both Deactivated and Closing fire
            // (e.g. the window loses focus right before it closes).
            if (_session?.AssignmentId == session.AssignmentId)
            {
                _session = null;
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // Best-effort: the app is exiting or unfocused, so there is no UI to
            // surface a retry to. A already_submitted/window_inactive response from
            // the API (see AssignmentsController.AutoSubmit) also lands here.
        }
    }
}

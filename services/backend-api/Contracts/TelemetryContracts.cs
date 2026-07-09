namespace BackendApi.Contracts;

// SDA-25: the client only ever claims an AssignmentId (a real id it already knows from
// having opened that assignment) — it never claims a ClassSessionId, since the desktop
// app's own view of "is a class in session" is derived client-side from calendar data and
// isn't guaranteed to line up with a persisted class_sessions row. When AssignmentId is
// omitted, the server resolves (or lazily creates) the actual active class session itself
// via ClassSessionLookup, the same authority SDA-12/TWA-08 use, rather than trusting a
// client-supplied id that could violate the usage_telemetry FK or simply be stale.
public record TelemetryEventRequest(
    string EventType,
    Dictionary<string, object>? Metadata,
    Guid? AssignmentId,
    DateTime RecordedAt);

public record SubmitTelemetryRequest(List<TelemetryEventRequest> Events);

public record SubmitTelemetryResponse(int EventsRecorded, int FlagsRaised);

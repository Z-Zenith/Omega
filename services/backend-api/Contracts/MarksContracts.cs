namespace BackendApi.Contracts;

// TWA-16. Publish is an explicit opt-in — omitting it (or leaving it false) records the
// marks without making them visible to the student; the teacher must set it true to
// publish, matching the "invisible until explicitly published" acceptance criterion.
public record CreateInternalMarkRequest(Guid StudentId, Guid SubjectId, Guid? AssignmentId, decimal Marks, bool Publish = false);

public record InternalMarkRecordDto(
    Guid Id,
    Guid StudentId,
    Guid SubjectId,
    Guid? AssignmentId,
    decimal Marks,
    bool Published,
    DateTime? PublishedAt);

// Roster for the marks-entry screen: every student in a section the caller teaches this
// subject to, with their existing (possibly unpublished) mark for the given assignment
// scope, if one has already been entered.
public record InternalMarksRosterEntryDto(
    Guid StudentId,
    string StudentName,
    decimal? Marks,
    bool Published,
    DateTime? PublishedAt);

public record InternalMarkDto(Guid SubjectId, string SubjectName, decimal Marks, DateTime? PublishedAt);

public record ExternalMarkDto(Guid SubjectId, string SubjectName, string Grade, DateTime? ApprovedAt);

// TWA-17
public record CreateExternalMarkRequest(Guid StudentId, Guid SubjectId, string Grade);

public record ExternalMarkSubmissionResponse(
    Guid Id,
    Guid StudentId,
    Guid SubjectId,
    string Grade,
    string Status,
    DateTime SubmittedAt);

// Lets the teacher-web UI decide whether to render the "submit external marks" option —
// the option must disappear the moment the underlying grant expires.
public record ExternalMarksPermissionStatusResponse(bool Granted, DateTime? ExpiresAt);

public record AttendanceRecordDto(DateOnly SessionDate, Guid SubjectId, string SubjectName, string Status);

// SDA-15
public record MyMarksResponse(
    IReadOnlyList<InternalMarkDto> InternalMarks,
    IReadOnlyList<ExternalMarkDto> ExternalMarks);

public record WardRecordResponse(
    Guid StudentId,
    string StudentFullName,
    IReadOnlyList<AttendanceRecordDto> Attendance,
    IReadOnlyList<InternalMarkDto> InternalMarks,
    IReadOnlyList<ExternalMarkDto> ExternalMarks);

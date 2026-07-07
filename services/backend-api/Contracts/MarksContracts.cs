namespace BackendApi.Contracts;

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

public record WardRecordResponse(
    Guid StudentId,
    string StudentFullName,
    IReadOnlyList<AttendanceRecordDto> Attendance,
    IReadOnlyList<InternalMarkDto> InternalMarks,
    IReadOnlyList<ExternalMarkDto> ExternalMarks);

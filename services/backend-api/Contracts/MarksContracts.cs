namespace BackendApi.Contracts;

public record InternalMarkDto(Guid SubjectId, string SubjectName, decimal Marks, DateTime? PublishedAt);

public record ExternalMarkDto(Guid SubjectId, string SubjectName, string Grade, DateTime? ApprovedAt);

// TWA-20
public record PendingExternalMarkDto(
    Guid Id,
    Guid StudentId,
    string StudentFullName,
    Guid SubjectId,
    string SubjectName,
    string Grade,
    Guid SubmittedBy,
    string SubmittedByFullName,
    DateTime SubmittedAt);

public record ApproveExternalMarkResponse(Guid Id, Guid ApprovedBy, DateTime ApprovedAt);

public record AttendanceRecordDto(DateOnly SessionDate, Guid SubjectId, string SubjectName, string Status);

public record WardRecordResponse(
    Guid StudentId,
    string StudentFullName,
    IReadOnlyList<AttendanceRecordDto> Attendance,
    IReadOnlyList<InternalMarkDto> InternalMarks,
    IReadOnlyList<ExternalMarkDto> ExternalMarks);

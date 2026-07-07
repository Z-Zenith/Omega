namespace BackendApi.Contracts;

public record InternalMarkDto(Guid SubjectId, string SubjectName, decimal Marks, DateTime? PublishedAt);

public record ExternalMarkDto(Guid SubjectId, string SubjectName, string Grade, DateTime? ApprovedAt);

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

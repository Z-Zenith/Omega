namespace BackendApi.Contracts;

// TWA-11
public record CreateReportRequest(Guid? SectionId, Guid? StudentId, string Content);

public record TeacherReportDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    Guid? SectionId,
    string? SectionName,
    Guid? StudentId,
    string? StudentName,
    string Content,
    DateTime SubmittedAt);

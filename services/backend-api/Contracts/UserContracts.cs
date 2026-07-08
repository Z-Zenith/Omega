using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateUserRequest(
    Guid CollegeId,
    AccountType AccountType,
    string Identifier,
    string InitialPassword,
    string FullName,
    Guid? DepartmentId);

public record CreateUserResponse(Guid UserId, string TotpProvisioningUri, string TotpSecret);

// AWA-07 — a teacher-submitted remark. TeacherName is resolved via the FK join
// regardless of whether that teacher is still active (see acceptance criterion:
// "record includes remarks... even if the submitting teacher is no longer active").
public record TeacherRemarkDto(Guid Id, Guid TeacherId, string TeacherName, string Content, DateTime SubmittedAt);

// AWA-07 — system-generated report rows (AIS-01 browsing summary, AIS-07 suspicious
// behaviour flag). Both are already-populated tables; this surfaces them, it doesn't
// generate them.
public record BrowsingSummaryReportDto(Guid Id, string SummaryText, DateTime GeneratedAt);

public record SuspiciousFlagReportDto(
    Guid Id,
    decimal ConfidenceScore,
    DateTime FlaggedAt,
    Guid? AssignmentId,
    Guid? ClassSessionId);

public record StudentRecordDto(
    Guid Id,
    string FullName,
    string Identifier,
    string AccountType,
    Guid CollegeId,
    Guid? DepartmentId,
    bool IsActive,
    List<TeacherRemarkDto> Remarks,
    List<BrowsingSummaryReportDto> BrowsingSummaries,
    List<SuspiciousFlagReportDto> SuspiciousFlags);

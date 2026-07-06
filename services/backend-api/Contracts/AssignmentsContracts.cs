using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateAssignmentRequest(
    Guid SubjectId,
    string Title,
    string? Description,
    AssignmentType Type,
    DateTime DueDate,
    DateTime SubmissionWindowStart,
    DateTime SubmissionWindowEnd,
    string? TypeSpecificSettings);

public record AssignmentDto(
    Guid Id,
    Guid SubjectId,
    string Title,
    string? Description,
    string Type,
    DateTime DueDate,
    DateTime SubmissionWindowStart,
    DateTime SubmissionWindowEnd,
    string? TypeSpecificSettings);

public record SubmitAssignmentRequest(string ContentUrl, AssignmentType SubmissionFormat);

public record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid StudentId,
    string ContentUrl,
    DateTime SubmittedAt,
    bool IsLate,
    bool IsAutosubmitted);

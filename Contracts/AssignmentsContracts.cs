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

// AIS-03: cross-class copy-check among one assignment's submissions, via the
// self-hosted embedding-similarity model (services/ai-services).
public record CopyCheckMatchDto(Guid SubmissionAId, Guid SubmissionBId, decimal SimilarityScore);

// AIS-04: advisory autograde suggestion. MaxScoreUsed/Confidence/MatchedCriteria/Feedback
// come straight from the AI Services response and aren't persisted — only SuggestedGrade
// and the confirm bookkeeping live in autograde_suggestions.
public record RubricCriterionInput(string Name, List<string> Keywords, double Weight);

public record RequestAutogradeSuggestion(List<RubricCriterionInput> Rubric, double MaxScore);

public record AutogradeSuggestionDto(
    Guid Id,
    Guid SubmissionId,
    decimal SuggestedGrade,
    double MaxScoreUsed,
    double Confidence,
    IReadOnlyList<string> MatchedCriteria,
    IReadOnlyList<string> Feedback);

public record ConfirmGradeRequest(Guid SuggestionId);

public record ConfirmedGradeDto(Guid Id, Guid SubmissionId, decimal SuggestedGrade, bool ConfirmedByTeacher, DateTime? ConfirmedAt);

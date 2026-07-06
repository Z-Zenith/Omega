namespace BackendApi.Contracts;

public record CreateThreadRequest(Guid StudentId, Guid TeacherId);

public record MessageThreadResponse(Guid Id, Guid StudentId, Guid TeacherId, DateTime CreatedAt);

public record SendMessageRequest(string Content);

public record MessageResponse(Guid Id, Guid ThreadId, Guid SenderId, string Content, DateTime SentAt, DateTime? ReadAt);

public record ThreadSummaryResponse(
    Guid Id,
    Guid StudentId,
    Guid TeacherId,
    DateTime CreatedAt,
    MessageResponse? LastMessage);

namespace BackendApi.Contracts;

public record CreateFeeLinkRequest(Guid StudentId, decimal Amount, DateOnly DueDate);

public record FeeLinkResponse(Guid FeeRecordId, string PaymentLink, decimal Amount, DateOnly DueDate, string Status);

namespace BackendApi.Contracts;

public record WardFeeDto(Guid Id, decimal Amount, DateOnly DueDate, string Status, DateTime? PaidAt);

public record PayFeeResponse(Guid FeeRecordId, string Status, DateTime ProcessedAt, string GatewayTxnId);

public record CreateFeeLinkRequest(Guid StudentId, decimal Amount, DateOnly DueDate);

public record FeeLinkResponse(Guid FeeRecordId, string PaymentLink, decimal Amount, DateOnly DueDate, string Status);

// AWA-05
public record SendFeeRemindersResponse(int FeesDueSoon, List<Guid> NotifiedParentIds);

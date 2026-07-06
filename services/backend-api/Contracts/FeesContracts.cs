namespace BackendApi.Contracts;

public record WardFeeDto(Guid Id, decimal Amount, DateOnly DueDate, string Status, DateTime? PaidAt);

public record PayFeeResponse(Guid FeeRecordId, string Status, DateTime ProcessedAt, string GatewayTxnId);

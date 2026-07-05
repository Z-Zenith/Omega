namespace BackendApi.Contracts;

public record LoginRequest(string Identifier, string Password, string TotpCode, string? DeviceInfo);

public record LoginResponse(string Token, Guid UserId, Guid SessionId, string AccountType, string FullName);

public record SessionInfoResponse(Guid UserId, Guid SessionId, string AccountType, string FullName, Guid CollegeId);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string TotpCode);

namespace BackendApi.Contracts;

public record ParentLoginRequest(string RollNumber, DateOnly DateOfBirth, string? DeviceInfo);

public record ParentLoginResponse(
    string Token,
    Guid ParentUserId,
    Guid SessionId,
    Guid WardStudentId,
    string WardFullName,
    string WardIdentifier);

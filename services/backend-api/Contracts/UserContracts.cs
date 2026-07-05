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

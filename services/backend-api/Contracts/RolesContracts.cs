namespace BackendApi.Contracts;

// AWA-14
public record CreateDepartmentRequest(Guid CollegeId, string Name);

public record DepartmentDto(Guid Id, Guid CollegeId, string Name, Guid? HodRoleBindingId, Guid? HodUserId);

public record AssignHodRequest(Guid UserId);

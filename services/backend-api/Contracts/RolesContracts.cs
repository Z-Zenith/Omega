using BackendApi.Data.Entities;

namespace BackendApi.Contracts;

public record CreateRoleBindingRequest(Guid UserId, string RoleCode, ScopeKind ScopeType, Guid? DepartmentId);

public record RoleBindingDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    string RoleCode,
    ScopeKind ScopeType,
    Guid? DepartmentId,
    DateTime GrantedAt);

public record CreatePermissionGrantRequest(Guid UserId, string PermissionCode, bool Granted, DateTime? ExpiresAt);

public record PermissionGrantDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    string PermissionCode,
    bool Granted,
    DateTime? ExpiresAt,
    Guid GrantedBy,
    DateTime CreatedAt);

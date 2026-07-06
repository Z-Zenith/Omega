namespace BackendApi.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode);

    Task<Guid?> GetDepartmentScopeAsync(Guid userId);
}

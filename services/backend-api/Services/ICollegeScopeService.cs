namespace BackendApi.Services;

public interface ICollegeScopeService
{
    Task<Guid?> GetCollegeIdAsync(Guid userId);

    Task<bool> IsSameCollegeAsync(Guid callerId, Guid targetCollegeId);
}

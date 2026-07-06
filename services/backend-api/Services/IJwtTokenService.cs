using BackendApi.Data.Entities;

namespace BackendApi.Services;

public interface IJwtTokenService
{
    string IssueToken(User user, Guid sessionId, Guid? wardStudentId = null);
}

using BackendApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// #126: Global-scoped role/permission grants are enforced platform-wide today with no
// notion of "college" narrowing them to the granter's own institution. Every controller
// action that loads or mutates a cross-tenant entity by a client-supplied id must compare
// its CollegeId against the caller's own CollegeId using this helper, rather than trusting
// the target id alone — see #126-#129 for the concrete cross-college IDOR/privilege-
// escalation findings this closes.
public class CollegeScopeService(AppDbContext db) : ICollegeScopeService
{
    public async Task<Guid?> GetCollegeIdAsync(Guid userId) =>
        await db.Users.Where(u => u.Id == userId).Select(u => (Guid?)u.CollegeId).FirstOrDefaultAsync();

    public async Task<bool> IsSameCollegeAsync(Guid callerId, Guid targetCollegeId)
    {
        var callerCollegeId = await GetCollegeIdAsync(callerId);
        return callerCollegeId is not null && callerCollegeId == targetCollegeId;
    }
}

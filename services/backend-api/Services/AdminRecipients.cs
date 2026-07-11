using BackendApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// Notification Router (shared) — #80. TWA-11 (report routing) and TWA-13 (timetable change
// routing) both need "every Admin in this college", not a single unambiguous recipient like
// SDA-04's approval flow. Admin is a role (RoleBinding with RoleCode == "admin"), not an
// AccountType, and role_bindings doesn't carry college_id directly — it's derived through the
// bound User instead, so this joins through Users to scope the fan-out to one college rather
// than notifying every admin institution-wide regardless of which college the report/request
// came from.
public static class AdminRecipients
{
    public static async Task<List<Guid>> GetCollegeAdminIdsAsync(AppDbContext db, Guid collegeId, CancellationToken cancellationToken = default) =>
        await db.RoleBindings
            .Where(b => b.RoleCode == "admin")
            .Join(db.Users.Where(u => u.CollegeId == collegeId), b => b.UserId, u => u.Id, (b, u) => u.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
}

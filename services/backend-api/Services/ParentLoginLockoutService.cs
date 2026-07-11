namespace BackendApi.Services;

// #134: RateLimiterPolicies.Auth (#79) partitions purely by caller IP. Parent login (PRT-01)
// authenticates on roll number + date of birth — a 365-value search space — which is
// exhaustible from a single IP well within the 5-requests-per-minute budget, and trivially
// bypassed altogether by rotating source IPs. This adds a second, IP-independent layer keyed
// by the roll number actually being attacked, so a lockout follows the account under attack
// regardless of which (or how many) IPs the requests come from. Deliberately in-memory rather
// than a new DB table — a schema change needs separate sign-off (CLAUDE.md contract-change
// rule) and lockout state doesn't need to survive a process restart to be effective. Registered
// as a singleton (Program.cs) so all requests share the same counters.
public class ParentLoginLockoutService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class Entry
    {
        public int FailedAttempts;
        public DateTime? LockedUntil;
    }

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _gate = new();

    public bool IsLockedOut(string rollNumber)
    {
        lock (_gate)
        {
            var entry = GetLiveEntry(rollNumber);
            return entry?.LockedUntil is { } lockedUntil && DateTime.UtcNow < lockedUntil;
        }
    }

    // Only call this for an actual bad-credential attempt (unknown roll number or wrong DOB) —
    // failures that reflect something other than a guess (e.g. account inactive, no parent
    // registered) shouldn't count against the lockout budget.
    public void RecordFailure(string rollNumber)
    {
        lock (_gate)
        {
            var key = Normalize(rollNumber);
            if (!_entries.TryGetValue(key, out var entry) || IsExpiredLockout(entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            entry.FailedAttempts++;
            if (entry.FailedAttempts >= MaxFailedAttempts)
            {
                entry.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            }
        }
    }

    public void RecordSuccess(string rollNumber)
    {
        lock (_gate)
        {
            _entries.Remove(Normalize(rollNumber));
        }
    }

    private Entry? GetLiveEntry(string rollNumber)
    {
        var key = Normalize(rollNumber);
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }
        if (IsExpiredLockout(entry))
        {
            // The lockout window has passed — drop it rather than let a stale entry keep the
            // roll number locked (or keep counting toward a lockout) forever.
            _entries.Remove(key);
            return null;
        }
        return entry;
    }

    private static bool IsExpiredLockout(Entry entry) =>
        entry.LockedUntil is { } lockedUntil && DateTime.UtcNow >= lockedUntil;

    private static string Normalize(string rollNumber) => rollNumber.Trim().ToUpperInvariant();
}

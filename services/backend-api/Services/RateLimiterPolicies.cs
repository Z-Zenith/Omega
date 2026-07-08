using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace BackendApi.Services;

// #79: no rate limiter existed anywhere in Program.cs. Parent login (PRT-01) authenticates
// on roll number + DOB — far weaker than password+MFA — and the main login endpoint had no
// lockout either. Extracted from Program.cs as a named policy (rather than an inline lambda)
// so the partitioning behavior itself can be unit tested without booting the full app/DB.
public static class RateLimiterPolicies
{
    public const string Auth = "auth";

    public static RateLimitPartition<string> AuthPartitioner(HttpContext httpContext) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            });

    public static void ConfigureAuth(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Auth, AuthPartitioner);
    }
}

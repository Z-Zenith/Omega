using System.Net;
using System.Threading.RateLimiting;
using BackendApi.Services;
using Microsoft.AspNetCore.Http;

namespace BackendApi.Tests.Services;

public class RateLimiterPoliciesTests
{
    private static DefaultHttpContext ContextFromIp(string ip) => new()
    {
        Connection = { RemoteIpAddress = IPAddress.Parse(ip) },
    };

    // #79 — Parent login (PRT-01) authenticates on a guessable roll number + DOB with no
    // lockout; this verifies the sliding-window policy actually rejects a caller after its
    // permit budget is spent, rather than just asserting the wiring compiles.
    [Fact]
    public async Task AuthPartitioner_RejectsRequestBeyondPermitLimit_FromSameIp()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimiterPolicies.AuthPartitioner);
        var context = ContextFromIp("203.0.113.7");

        for (var i = 0; i < 5; i++)
        {
            using var lease = await limiter.AcquireAsync(context);
            Assert.True(lease.IsAcquired, $"request {i + 1} should be within the permit limit");
        }

        using var sixthLease = await limiter.AcquireAsync(context);
        Assert.False(sixthLease.IsAcquired);
    }

    // #79 — a different caller IP must not be throttled by another IP's exhausted budget.
    [Fact]
    public async Task AuthPartitioner_TracksSeparateIpsIndependently()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimiterPolicies.AuthPartitioner);
        var exhausted = ContextFromIp("203.0.113.10");
        var fresh = ContextFromIp("203.0.113.20");

        for (var i = 0; i < 5; i++)
        {
            using var lease = await limiter.AcquireAsync(exhausted);
            Assert.True(lease.IsAcquired);
        }
        using var exhaustedLease = await limiter.AcquireAsync(exhausted);
        Assert.False(exhaustedLease.IsAcquired);

        using var freshLease = await limiter.AcquireAsync(fresh);
        Assert.True(freshLease.IsAcquired);
    }
}

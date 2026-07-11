using BackendApi.Services;

namespace BackendApi.Tests.Services;

public class ParentLoginLockoutServiceTests
{
    // #134: the lockout must trip on repeated bad credentials for a given roll number,
    // independent of any per-IP rate limiting — this is what stops DOB brute-forcing that
    // rotates source IPs.
    [Fact]
    public void IsLockedOut_BecomesTrue_AfterFiveFailures()
    {
        var lockout = new ParentLoginLockoutService();

        for (var i = 0; i < 4; i++)
        {
            lockout.RecordFailure("ROLL-1");
            Assert.False(lockout.IsLockedOut("ROLL-1"));
        }
        lockout.RecordFailure("ROLL-1");

        Assert.True(lockout.IsLockedOut("ROLL-1"));
    }

    [Fact]
    public void IsLockedOut_TracksRollNumbersIndependently()
    {
        var lockout = new ParentLoginLockoutService();

        for (var i = 0; i < 5; i++)
        {
            lockout.RecordFailure("ROLL-LOCKED");
        }

        Assert.True(lockout.IsLockedOut("ROLL-LOCKED"));
        Assert.False(lockout.IsLockedOut("ROLL-FRESH"));
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        var lockout = new ParentLoginLockoutService();

        for (var i = 0; i < 4; i++)
        {
            lockout.RecordFailure("ROLL-2");
        }
        lockout.RecordSuccess("ROLL-2");
        lockout.RecordFailure("ROLL-2");

        // Only 1 failure since the reset — nowhere near the 5-failure threshold.
        Assert.False(lockout.IsLockedOut("ROLL-2"));
    }

    [Fact]
    public void IsLockedOut_IsCaseInsensitiveAndTrimsWhitespace()
    {
        var lockout = new ParentLoginLockoutService();

        for (var i = 0; i < 5; i++)
        {
            lockout.RecordFailure("roll-3");
        }

        Assert.True(lockout.IsLockedOut(" ROLL-3 "));
    }
}

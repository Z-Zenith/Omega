using System;
using StudentDesktop.Services;

namespace StudentDesktop.Tests;

public class ClassLockServiceTests
{
    [Fact]
    public void SDA01_StartsUnlocked()
    {
        using var service = new ClassLockService(new ApiClient());

        Assert.False(service.IsLocked);
    }

    [Fact]
    public void SDA01_StartAndStopDoNotThrow_EvenWithoutAReachableServer()
    {
        using var service = new ClassLockService(new ApiClient("http://localhost:0"));

        service.Start();
        service.Stop();

        Assert.False(service.IsLocked);
    }

    [Fact]
    public void SDA01_StopAlwaysLeavesTheSessionUnlocked()
    {
        using var service = new ClassLockService(new ApiClient());
        var raised = false;
        service.LockStateChanged += (_, locked) => raised = locked;

        service.Start();
        service.Stop();

        Assert.False(service.IsLocked);
        Assert.False(raised);
    }

    [Fact]
    public void SDA01_DisposeIsIdempotent()
    {
        var service = new ClassLockService(new ApiClient());

        service.Dispose();
        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }
}

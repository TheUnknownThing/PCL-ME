using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendSystemMemoryServiceTest
{
    [TestCleanup]
    public void Cleanup()
    {
        FrontendSystemMemoryService.PhysicalMemoryBytesOverride = null;
        FrontendSystemMemoryService.InvalidateMemoryStateCache();
    }

    [TestMethod]
    public void PhysicalMemoryState_PrefersThePlatformSourceAndConvertsToGigabytes()
    {
        FrontendSystemMemoryService.PhysicalMemoryBytesOverride = () => (16L * 1024 * 1024 * 1024, 6L * 1024 * 1024 * 1024);
        FrontendSystemMemoryService.InvalidateMemoryStateCache();

        var state = FrontendSystemMemoryService.GetPhysicalMemoryState();

        Assert.AreEqual(16.0, state.TotalGb, 0.01);
        Assert.AreEqual(6.0, state.AvailableGb, 0.01);
    }

    [TestMethod]
    public void PhysicalMemoryState_ReportsLiveSystemMemoryOnEveryPlatform()
    {
        FrontendSystemMemoryService.InvalidateMemoryStateCache();

        var state = FrontendSystemMemoryService.GetPhysicalMemoryState();

        Assert.IsTrue(state.TotalGb > 0.5, $"Total memory was not read from the platform source: {state.TotalGb} GB.");
        Assert.IsTrue(state.AvailableGb > 0, $"Available memory was not read from the platform source: {state.AvailableGb} GB.");
        Assert.IsTrue(state.AvailableGb <= state.TotalGb, $"Available memory ({state.AvailableGb} GB) exceeds total memory ({state.TotalGb} GB).");
    }

    [TestMethod]
    public void PhysicalMemoryState_CachesUntilInvalidated()
    {
        var liveState = FrontendSystemMemoryService.GetPhysicalMemoryState();

        FrontendSystemMemoryService.PhysicalMemoryBytesOverride = () => (2L * 1024 * 1024 * 1024, 1L * 1024 * 1024 * 1024);
        Assert.AreEqual(liveState.TotalGb, FrontendSystemMemoryService.GetPhysicalMemoryState().TotalGb, 0.01,
            "The cached platform state must be served within the cache duration.");

        FrontendSystemMemoryService.InvalidateMemoryStateCache();
        Assert.AreEqual(2.0, FrontendSystemMemoryService.GetPhysicalMemoryState().TotalGb, 0.01,
            "Invalidating the cache must expose the new platform state.");
    }
}

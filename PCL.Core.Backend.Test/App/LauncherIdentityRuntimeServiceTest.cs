using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherIdentityRuntimeServiceTest
{
    [TestMethod]
    public void ResolveSkipsDeviceLookupWhenPersistedLauncherIdExists()
    {
        var plan = LauncherIdentityRuntimeService.Resolve(new LauncherIdentityRuntimeRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: "persisted-id",
            ReadDeviceLauncherId: () => throw new AssertFailedException("Device identity should not be queried when a persisted id exists."),
            GenerateLauncherId: () => throw new AssertFailedException("Generated fallback should not be requested when a persisted id exists."),
            PersistLauncherId: _ => throw new AssertFailedException("Persistence should not run when a persisted id exists.")));

        Assert.AreEqual(LauncherIdentityResolutionSource.PersistedFile, plan.Source);
        Assert.IsFalse(plan.PersistenceRequested);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("persisted-id"),
            plan.LauncherId);
    }

    [TestMethod]
    public void ResolvePersistsDeviceIdentityWhenNoExistingLauncherIdIsAvailable()
    {
        string? persistedLauncherId = null;
        var plan = LauncherIdentityRuntimeService.Resolve(new LauncherIdentityRuntimeRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: null,
            ReadDeviceLauncherId: () => "device-id",
            GenerateLauncherId: () => "generated-id",
            PersistLauncherId: launcherId => persistedLauncherId = launcherId));

        Assert.AreEqual(LauncherIdentityResolutionSource.DeviceIdentity, plan.Source);
        Assert.IsTrue(plan.PersistenceRequested);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("device-id"),
            plan.LauncherId);
        Assert.AreEqual(plan.LauncherId, persistedLauncherId);
    }
}

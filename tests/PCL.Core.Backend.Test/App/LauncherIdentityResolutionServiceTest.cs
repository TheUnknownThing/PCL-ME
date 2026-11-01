using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherIdentityResolutionServiceTest
{
    [TestMethod]
    public void ResolvePrefersEnvironmentOverrideWithoutPersistence()
    {
        var result = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: "env-id",
            PersistedLauncherId: "persisted-id",
            DeviceLauncherId: "device-id",
            GeneratedLauncherId: "generated-id"));

        Assert.AreEqual(LauncherIdentityResolutionSource.EnvironmentOverride, result.Source);
        Assert.IsFalse(result.ShouldPersist);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("env-id"),
            result.LauncherId);
    }

    [TestMethod]
    public void ResolveUsesPersistedLauncherIdBeforeDeviceIdentity()
    {
        var result = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: "persisted-id",
            DeviceLauncherId: "device-id",
            GeneratedLauncherId: "generated-id"));

        Assert.AreEqual(LauncherIdentityResolutionSource.PersistedFile, result.Source);
        Assert.IsFalse(result.ShouldPersist);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("persisted-id"),
            result.LauncherId);
    }

    [TestMethod]
    public void ResolveUsesDeviceIdentityAndRequestsPersistenceWhenNoExistingIdIsAvailable()
    {
        var result = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: null,
            DeviceLauncherId: "device-id",
            GeneratedLauncherId: "generated-id"));

        Assert.AreEqual(LauncherIdentityResolutionSource.DeviceIdentity, result.Source);
        Assert.IsTrue(result.ShouldPersist);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("device-id"),
            result.LauncherId);
    }

    [TestMethod]
    public void ResolveFallsBackToGeneratedLauncherIdWhenNoOtherSourceExists()
    {
        var result = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: null,
            DeviceLauncherId: null,
            GeneratedLauncherId: "generated-id"));

        Assert.AreEqual(LauncherIdentityResolutionSource.GeneratedFallback, result.Source);
        Assert.IsTrue(result.ShouldPersist);
        Assert.AreEqual(
            LauncherIdentityResolutionService.NormalizeLauncherId("generated-id"),
            result.LauncherId);
    }

    [TestMethod]
    public void NormalizeLauncherIdPreservesCanonicalFormat()
    {
        const string canonical = "ABCD-EFGH-IJKL-MNOP";

        var result = LauncherIdentityResolutionService.NormalizeLauncherId(canonical);

        Assert.AreEqual(canonical, result);
    }
}

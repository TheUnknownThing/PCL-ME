using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherLegacySecretResolutionServiceTest
{
    [TestMethod]
    public void ResolvePrefersExplicitOverride()
    {
        var plan = LauncherLegacySecretResolutionService.Resolve(new LauncherLegacySecretResolutionRequest(
            ExplicitLegacyDecryptKey: "  explicit-key  ",
            LegacyDeviceSeed: "device-seed"));

        Assert.AreEqual(LauncherLegacySecretSource.EnvironmentOverride, plan.Source);
        Assert.AreEqual("explicit-key", plan.DecryptKey);
    }

    [TestMethod]
    public void ResolveDerivesKeyFromLegacyDeviceSeed()
    {
        var plan = LauncherLegacySecretResolutionService.Resolve(new LauncherLegacySecretResolutionRequest(
            ExplicitLegacyDecryptKey: null,
            LegacyDeviceSeed: "cpu-seed-123"));

        Assert.AreEqual(LauncherLegacySecretSource.DeviceSeedDerived, plan.Source);
        Assert.AreEqual(
            LauncherLegacyIdentityService.DeriveEncryptionKey("cpu-seed-123"),
            plan.DecryptKey);
    }

    [TestMethod]
    public void ResolveReturnsUnavailableWhenNoOverrideOrDeviceSeedExists()
    {
        var plan = LauncherLegacySecretResolutionService.Resolve(new LauncherLegacySecretResolutionRequest(
            ExplicitLegacyDecryptKey: null,
            LegacyDeviceSeed: null));

        Assert.AreEqual(LauncherLegacySecretSource.Unavailable, plan.Source);
        Assert.IsNull(plan.DecryptKey);
    }
}

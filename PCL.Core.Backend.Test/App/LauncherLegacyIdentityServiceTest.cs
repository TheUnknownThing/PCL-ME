using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherLegacyIdentityServiceTest
{
    [TestMethod]
    public void DeriveRawCodeFallsBackToDefaultWhenDeviceSeedIsMissing()
    {
        var result = LauncherLegacyIdentityService.DeriveRawCode(null);

        Assert.AreEqual(LauncherLegacyIdentityService.DefaultRawCode, result);
    }

    [TestMethod]
    public void DeriveEncryptionKeyMatchesLegacyDefaultVector()
    {
        var result = LauncherLegacyIdentityService.DeriveEncryptionKey(null);

        Assert.AreEqual("FDD3ED6C9B2E44D83954D0D9FBBE9BBB", result);
    }

    [TestMethod]
    public void DeriveLauncherIdMatchesLegacyFormattingForKnownInputs()
    {
        var result = LauncherLegacyIdentityService.DeriveLauncherId("launch-uuid-1", "cpu-seed-123");

        Assert.AreEqual("CE09-2E95-363D-EA59", result);
    }
}

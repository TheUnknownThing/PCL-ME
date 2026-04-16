using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupVersionIsolationMigrationServiceTest
{
    [TestMethod]
    public void EvaluateReturnsNoOpWhenVersionIsolationV2AlreadyExists()
    {
        var result = LauncherStartupVersionIsolationMigrationService.Evaluate(new LauncherStartupVersionIsolationMigrationRequest(
            HasVersionIsolationV2Setting: true,
            HasLegacyVersionIsolationSetting: true,
            LegacyVersionIsolationValue: 3,
            HasWindowHeightSetting: true,
            LegacyVersionIsolationDefaultValue: 2,
            VersionIsolationV2DefaultValue: 4));

        Assert.IsFalse(result.ShouldStoreVersionIsolationV2);
        Assert.IsNull(result.LogMessage);
    }

    [TestMethod]
    public void EvaluatePrefersLegacyVersionIsolationValue()
    {
        var result = LauncherStartupVersionIsolationMigrationService.Evaluate(new LauncherStartupVersionIsolationMigrationRequest(
            HasVersionIsolationV2Setting: false,
            HasLegacyVersionIsolationSetting: true,
            LegacyVersionIsolationValue: 3,
            HasWindowHeightSetting: true,
            LegacyVersionIsolationDefaultValue: 2,
            VersionIsolationV2DefaultValue: 4));

        Assert.IsTrue(result.ShouldStoreVersionIsolationV2);
        Assert.AreEqual(3, result.VersionIsolationV2Value);
        Assert.AreEqual("[Start] Migrated version isolation from legacy PCL", result.LogMessage);
    }

    [TestMethod]
    public void EvaluateFallsBackToLegacyDefaultWhenOldInstallHasWindowSettings()
    {
        var result = LauncherStartupVersionIsolationMigrationService.Evaluate(new LauncherStartupVersionIsolationMigrationRequest(
            HasVersionIsolationV2Setting: false,
            HasLegacyVersionIsolationSetting: false,
            LegacyVersionIsolationValue: 0,
            HasWindowHeightSetting: true,
            LegacyVersionIsolationDefaultValue: 2,
            VersionIsolationV2DefaultValue: 4));

        Assert.AreEqual(2, result.VersionIsolationV2Value);
        Assert.AreEqual("[Start] Upgraded from legacy PCL without a version-isolation change; using the legacy default", result.LogMessage);
    }

    [TestMethod]
    public void EvaluateUsesNewDefaultForFreshInstall()
    {
        var result = LauncherStartupVersionIsolationMigrationService.Evaluate(new LauncherStartupVersionIsolationMigrationRequest(
            HasVersionIsolationV2Setting: false,
            HasLegacyVersionIsolationSetting: false,
            LegacyVersionIsolationValue: 0,
            HasWindowHeightSetting: false,
            LegacyVersionIsolationDefaultValue: 2,
            VersionIsolationV2DefaultValue: 4));

        Assert.AreEqual(4, result.VersionIsolationV2Value);
        Assert.AreEqual("[Start] Fresh PCL install, using the new version-isolation default", result.LogMessage);
    }
}

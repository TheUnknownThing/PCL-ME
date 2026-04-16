using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherVersionTransitionServiceTest
{
    [TestMethod]
    public void EvaluateReturnsUpgradePlanWithLegacyMigrationsAndNotices()
    {
        var result = LauncherVersionTransitionService.Evaluate(new LauncherVersionTransitionRequest(
            LastVersionCode: 100,
            CurrentVersionCode: 400,
            IsBetaBuild: true,
            HighestRecordedVersionCode: 200,
            LaunchArgumentWindowType: 5,
            ThemeHiddenLegacy: "3|13",
            ThemeHiddenV2: "4|12|13",
            HasLegacyExecutableCustomSkin: true,
            HasLegacyTempCustomSkin: true,
            HasAppDataCustomSkin: false,
            HasLegacyModNameSetting: true,
            HasModNameSettingV2: false,
            LegacyModNameSetting: 2));

        Assert.IsTrue(result.IsUpgrade);
        Assert.IsFalse(result.IsDowngrade);
        Assert.IsTrue(result.ShouldStoreCurrentVersion);
        Assert.AreEqual("SystemHighestBetaVersionReg", result.HighestVersionStorageKey);
        Assert.AreEqual(400, result.HighestVersionToStore);
        Assert.AreEqual(1, result.LaunchArgumentWindowTypeToStore);
        Assert.AreEqual("2|3|4", result.ThemeHiddenV2ToStore);
        Assert.AreEqual(LauncherCustomSkinMigrationSourceKind.ExecutableDirectory, result.CustomSkinMigrationSource);
        Assert.IsTrue(result.ShouldUnhideSetupAbout);
        Assert.IsTrue(result.ShouldMigrateOldProfile);
        Assert.AreEqual(3, result.ModNameSettingV2ToStore);
        Assert.IsTrue(result.ShouldShowCommunityAnnouncement);
        Assert.IsTrue(result.ShouldShowUpdateLog);
        CollectionAssert.AreEqual(
            new[] { "Re-unlock notice", "Re-unlock notice" },
            result.Notices.Select(notice => notice.Title).ToArray());
    }

    [TestMethod]
    public void EvaluatePrefersTempCustomSkinWhenExecutableMigrationIsNoLongerEligible()
    {
        var result = LauncherVersionTransitionService.Evaluate(new LauncherVersionTransitionRequest(
            LastVersionCode: 200,
            CurrentVersionCode: 400,
            IsBetaBuild: false,
            HighestRecordedVersionCode: 250,
            LaunchArgumentWindowType: 1,
            ThemeHiddenLegacy: null,
            ThemeHiddenV2: "4|5",
            HasLegacyExecutableCustomSkin: true,
            HasLegacyTempCustomSkin: true,
            HasAppDataCustomSkin: false,
            HasLegacyModNameSetting: false,
            HasModNameSettingV2: true,
            LegacyModNameSetting: 0));

        Assert.AreEqual("SystemHighestAlphaVersionReg", result.HighestVersionStorageKey);
        Assert.AreEqual(LauncherCustomSkinMigrationSourceKind.TempDirectory, result.CustomSkinMigrationSource);
        Assert.IsNull(result.ModNameSettingV2ToStore);
    }

    [TestMethod]
    public void EvaluateSkipsUpdateLogWhenCurrentVersionWasAlreadySeen()
    {
        var result = LauncherVersionTransitionService.Evaluate(new LauncherVersionTransitionRequest(
            LastVersionCode: 350,
            CurrentVersionCode: 400,
            IsBetaBuild: false,
            HighestRecordedVersionCode: 400,
            LaunchArgumentWindowType: 1,
            ThemeHiddenLegacy: null,
            ThemeHiddenV2: "4|5",
            HasLegacyExecutableCustomSkin: false,
            HasLegacyTempCustomSkin: false,
            HasAppDataCustomSkin: true,
            HasLegacyModNameSetting: false,
            HasModNameSettingV2: true,
            LegacyModNameSetting: 0));

        Assert.IsNull(result.HighestVersionStorageKey);
        Assert.IsNull(result.HighestVersionToStore);
        Assert.IsFalse(result.ShouldShowUpdateLog);
    }

    [TestMethod]
    public void EvaluateReturnsDowngradePlanWithoutUpgradeMigrations()
    {
        var result = LauncherVersionTransitionService.Evaluate(new LauncherVersionTransitionRequest(
            LastVersionCode: 500,
            CurrentVersionCode: 400,
            IsBetaBuild: false,
            HighestRecordedVersionCode: 500,
            LaunchArgumentWindowType: 5,
            ThemeHiddenLegacy: "3",
            ThemeHiddenV2: "4",
            HasLegacyExecutableCustomSkin: true,
            HasLegacyTempCustomSkin: true,
            HasAppDataCustomSkin: false,
            HasLegacyModNameSetting: true,
            HasModNameSettingV2: false,
            LegacyModNameSetting: 2));

        Assert.IsFalse(result.IsUpgrade);
        Assert.IsTrue(result.IsDowngrade);
        Assert.IsTrue(result.ShouldStoreCurrentVersion);
        Assert.IsNull(result.HighestVersionStorageKey);
        Assert.IsNull(result.ThemeHiddenV2ToStore);
        Assert.IsNull(result.CustomSkinMigrationSource);
        Assert.IsFalse(result.ShouldShowCommunityAnnouncement);
        Assert.IsFalse(result.ShouldShowUpdateLog);
        Assert.AreEqual(0, result.Notices.Count);
    }

    [TestMethod]
    public void EvaluateReturnsNoOpWhenVersionDoesNotChange()
    {
        var result = LauncherVersionTransitionService.Evaluate(new LauncherVersionTransitionRequest(
            LastVersionCode: 400,
            CurrentVersionCode: 400,
            IsBetaBuild: false,
            HighestRecordedVersionCode: 400,
            LaunchArgumentWindowType: 1,
            ThemeHiddenLegacy: null,
            ThemeHiddenV2: null,
            HasLegacyExecutableCustomSkin: false,
            HasLegacyTempCustomSkin: false,
            HasAppDataCustomSkin: false,
            HasLegacyModNameSetting: false,
            HasModNameSettingV2: false,
            LegacyModNameSetting: 0));

        Assert.IsFalse(result.IsUpgrade);
        Assert.IsFalse(result.IsDowngrade);
        Assert.IsFalse(result.ShouldStoreCurrentVersion);
        Assert.IsFalse(result.ShouldShowCommunityAnnouncement);
        Assert.AreEqual(0, result.Notices.Count);
    }
}

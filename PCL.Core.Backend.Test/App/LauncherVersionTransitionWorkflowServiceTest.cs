using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherVersionTransitionWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanProducesSettingWritesLogsAndCustomSkinMigration()
    {
        var result = LauncherVersionTransitionWorkflowService.BuildPlan(
            new LauncherVersionTransitionWorkflowRequest(
                new LauncherVersionTransitionRequest(
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
                    LegacyModNameSetting: 2),
                LegacyExecutableCustomSkinPath: @"C:\PCL\CustomSkin.png",
                LegacyTempCustomSkinPath: @"C:\Temp\CustomSkin.png",
                AppDataCustomSkinPath: @"C:\AppData\CustomSkin.png"));

        CollectionAssert.AreEqual(
            new[] { "SystemLastVersionReg", "SystemHighestBetaVersionReg", "LaunchArgumentWindowType", "UiLauncherThemeHide2", "ToolDownloadTranslateV2" },
            result.SettingWrites.Select(write => write.Key).ToArray());
        Assert.AreEqual("[Start] Highest recorded version increased from 200 to 400", result.HighestVersionLogMessage);
        Assert.AreEqual(@"C:\PCL\CustomSkin.png", result.CustomSkinMigration!.SourcePath);
        Assert.AreEqual(@"C:\AppData\CustomSkin.png", result.CustomSkinMigration.TargetPath);
        Assert.AreEqual("[Start] Unhid the help page", result.SetupAboutUnhideLogMessage);
        Assert.AreEqual("[Start] Migrated mod naming settings from the legacy version", result.ModNameMigrationLogMessage);
    }

    [TestMethod]
    public void BuildPlanReturnsMinimalActionsWhenNoUpgradeMigrationsAreNeeded()
    {
        var result = LauncherVersionTransitionWorkflowService.BuildPlan(
            new LauncherVersionTransitionWorkflowRequest(
                new LauncherVersionTransitionRequest(
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
                    LegacyModNameSetting: 0),
                LegacyExecutableCustomSkinPath: @"C:\PCL\CustomSkin.png",
                LegacyTempCustomSkinPath: @"C:\Temp\CustomSkin.png",
                AppDataCustomSkinPath: @"C:\AppData\CustomSkin.png"));

        Assert.AreEqual(0, result.SettingWrites.Count);
        Assert.IsNull(result.HighestVersionLogMessage);
        Assert.IsNull(result.CustomSkinMigration);
        Assert.IsNull(result.SetupAboutUnhideLogMessage);
        Assert.IsNull(result.ModNameMigrationLogMessage);
    }
}

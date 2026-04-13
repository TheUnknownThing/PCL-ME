using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherMainWindowStartupWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanCombinesVersionIsolationConsentAndMilestone()
    {
        var result = LauncherMainWindowStartupWorkflowService.BuildPlan(
            new LauncherMainWindowStartupWorkflowRequest(
                HasVersionIsolationV2Setting: false,
                HasLegacyVersionIsolationSetting: true,
                LegacyVersionIsolationValue: 2,
                HasWindowHeightSetting: true,
                LegacyVersionIsolationDefaultValue: 1,
                VersionIsolationV2DefaultValue: 3,
                SpecialBuildKind: LauncherStartupSpecialBuildKind.Ci,
                IsSpecialBuildHintDisabled: false,
                HasAcceptedEula: false,
                IsTelemetryDefault: true,
                CurrentStartupCount: 98));

        Assert.IsTrue(result.VersionIsolationMigration.ShouldStoreVersionIsolationV2);
        Assert.AreEqual(2, result.VersionIsolationMigration.VersionIsolationV2Value);
        Assert.AreEqual(3, result.Consent.Prompts.Count);
        Assert.AreEqual(99, result.Milestone.UpdatedCount);
        Assert.IsTrue(result.Milestone.ShouldAttemptUnlockHiddenTheme);
    }
}

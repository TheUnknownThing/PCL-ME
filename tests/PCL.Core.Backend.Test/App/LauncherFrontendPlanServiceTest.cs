using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherFrontendPlanServiceTest
{
    [TestMethod]
    public void BuildPlanCombinesStartupAndNavigationContracts()
    {
        var plan = LauncherFrontendPlanService.BuildPlan(new LauncherFrontendPlanRequest(
            new LauncherStartupWorkflowRequest(
                CommandLineArguments: [],
                ExecutableDirectory: @"C:\PCL\",
                TempDirectory: @"C:\PCL\Temp\",
                AppDataDirectory: @"C:\Users\demo\AppData\Roaming\PCL\",
                IsBetaVersion: false,
                DetectedWindowsVersion: new Version(10, 0, 19045),
                Is64BitOperatingSystem: true,
                ShowStartupLogo: true),
            new LauncherStartupConsentRequest(
                LauncherStartupSpecialBuildKind.Ci,
                IsSpecialBuildHintDisabled: false,
                HasAcceptedEula: false),
            new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                HasRunningTasks: true,
                HasGameLogs: true)));

        Assert.AreEqual(4, plan.Catalog.TopLevelPages.Count);
        Assert.AreEqual(2, plan.Consent.Prompts.Count);
        Assert.AreEqual(2, plan.Prompts.Count);
        Assert.AreEqual(LauncherFrontendPageKey.Launch, plan.Navigation.CurrentRoute.Page);
        Assert.AreEqual(LauncherFrontendPageKind.TopLevel, plan.Navigation.CurrentPage.Kind);
        Assert.AreEqual(2, plan.Navigation.UtilityEntries.Count(entry => entry.IsVisible));
    }
}

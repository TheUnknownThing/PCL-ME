using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanCombinesImmediateCommandBootstrapVisualAndPrompt()
    {
        var result = LauncherStartupWorkflowService.BuildPlan(
            new LauncherStartupWorkflowRequest(
                CommandLineArguments: ["--memory"],
                ExecutableDirectory: @"C:\Users\Alice\AppData\Local\Temp\PCL",
                TempDirectory: @"C:\Users\Alice\AppData\Local\Temp\PCL\Temp",
                AppDataDirectory: @"C:\Users\Alice\AppData\Roaming\PCL\",
                IsBetaVersion: true,
                DetectedWindowsVersion: new Version(10, 0, 17762, 0),
                Is64BitOperatingSystem: false,
                ShowStartupLogo: true));

        Assert.AreEqual(LauncherStartupImmediateCommandKind.OptimizeMemory, result.ImmediateCommand.Kind);
        Assert.AreEqual(UpdateChannel.Beta, result.Bootstrap.DefaultUpdateChannel);
        Assert.IsTrue(result.Visual.ShouldShowSplashScreen);
        Assert.IsNotNull(result.EnvironmentWarningPrompt);
        Assert.AreEqual("环境警告", result.EnvironmentWarningPrompt.Title);
    }

    [TestMethod]
    public void BuildPlanOmitsEnvironmentPromptWhenNoWarningsExist()
    {
        var result = LauncherStartupWorkflowService.BuildPlan(
            new LauncherStartupWorkflowRequest(
                CommandLineArguments: [],
                ExecutableDirectory: @"D:\PCL",
                TempDirectory: @"D:\PCL\Temp",
                AppDataDirectory: @"D:\PCL\Data",
                IsBetaVersion: false,
                DetectedWindowsVersion: new Version(10, 0, 19045, 0),
                Is64BitOperatingSystem: true,
                ShowStartupLogo: false));

        Assert.AreEqual(LauncherStartupImmediateCommandKind.None, result.ImmediateCommand.Kind);
        Assert.IsFalse(result.Visual.ShouldShowSplashScreen);
        Assert.IsNull(result.EnvironmentWarningPrompt);
        Assert.AreEqual(UpdateChannel.Release, result.Bootstrap.DefaultUpdateChannel);
    }
}

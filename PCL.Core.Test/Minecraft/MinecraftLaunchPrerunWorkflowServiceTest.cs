using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchPrerunWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanCombinesLauncherProfilesAndOptionsPlans()
    {
        var result = MinecraftLaunchPrerunWorkflowService.BuildPlan(
            new MinecraftLaunchPrerunWorkflowRequest(
                LauncherProfilesPath: @"C:\Minecraft\launcher_profiles.json",
                IsMicrosoftLogin: true,
                ExistingLauncherProfilesJson: "{}",
                UserName: "Player",
                ClientToken: "client-token",
                LauncherProfilesDefaultTimestamp: new DateTime(2026, 4, 2, 10, 0, 0),
                PrimaryOptionsFilePath: @"C:\Minecraft\options.txt",
                PrimaryOptionsFileExists: false,
                PrimaryCurrentLanguage: "en_us",
                YosbrOptionsFilePath: @"C:\Minecraft\config\yosbr\options.txt",
                YosbrOptionsFileExists: true,
                HasExistingSaves: false,
                ReleaseTime: new DateTime(2018, 1, 1),
                LaunchWindowType: 0,
                AutoChangeLanguage: true));

        Assert.IsTrue(result.LauncherProfiles.ShouldEnsureFileExists);
        Assert.AreEqual(@"C:\Minecraft\launcher_profiles.json", result.LauncherProfiles.Path);
        Assert.IsTrue(result.LauncherProfiles.Workflow.ShouldWrite);
        Assert.AreEqual(@"C:\Minecraft\config\yosbr\options.txt", result.Options.TargetFilePath);
        Assert.AreEqual(MinecraftLaunchOptionsFileTargetKind.Yosbr, result.Options.SyncPlan.TargetKind);
    }

    [TestMethod]
    public void BuildPlanSkipsLauncherProfilesWhenNotMicrosoftLogin()
    {
        var result = MinecraftLaunchPrerunWorkflowService.BuildPlan(
            new MinecraftLaunchPrerunWorkflowRequest(
                LauncherProfilesPath: null,
                IsMicrosoftLogin: false,
                ExistingLauncherProfilesJson: null,
                UserName: null,
                ClientToken: null,
                LauncherProfilesDefaultTimestamp: new DateTime(2026, 4, 2, 10, 0, 0),
                PrimaryOptionsFilePath: @"C:\Minecraft\options.txt",
                PrimaryOptionsFileExists: true,
                PrimaryCurrentLanguage: "zh_cn",
                YosbrOptionsFilePath: @"C:\Minecraft\config\yosbr\options.txt",
                YosbrOptionsFileExists: false,
                HasExistingSaves: true,
                ReleaseTime: new DateTime(2023, 1, 1),
                LaunchWindowType: 1,
                AutoChangeLanguage: true));

        Assert.IsFalse(result.LauncherProfiles.ShouldEnsureFileExists);
        Assert.IsNull(result.LauncherProfiles.Path);
        Assert.IsFalse(result.LauncherProfiles.Workflow.ShouldWrite);
        Assert.AreEqual(@"C:\Minecraft\options.txt", result.Options.TargetFilePath);
        Assert.AreEqual(MinecraftLaunchOptionsFileTargetKind.Primary, result.Options.SyncPlan.TargetKind);
    }

    [TestMethod]
    public void BuildGpuPreferenceFailurePlanDelegatesToGpuPreferenceWorkflow()
    {
        var result = MinecraftLaunchPrerunWorkflowService.BuildGpuPreferenceFailurePlan(
            new MinecraftLaunchGpuPreferenceFailureRequest(
                @"C:\Java\bin\javaw.exe",
                WantHighPerformance: true,
                IsRunningAsAdmin: false));

        Assert.AreEqual(MinecraftLaunchGpuPreferenceFailureActionKind.RetryAsAdmin, result.ActionKind);
        Assert.AreEqual("--gpu \"C:\\Java\\bin\\javaw.exe\"", result.AdminRetryArguments);
    }
}

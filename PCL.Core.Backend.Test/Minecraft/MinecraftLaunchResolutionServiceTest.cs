using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchResolutionServiceTest
{
    [TestMethod]
    public void BuildPlanUsesLauncherDimensionsForFollowLauncherMode()
    {
        var result = MinecraftLaunchResolutionService.BuildPlan(
            new MinecraftLaunchResolutionRequest(
                WindowMode: 2,
                LauncherWindowWidth: 1600,
                LauncherWindowHeight: 900,
                LauncherTitleBarHeight: 32,
                CustomWidth: 1200,
                CustomHeight: 800,
                GameVersionDrop: 300,
                JavaMajorVersion: 21,
                JavaRevision: 0,
                HasOptiFine: false,
                HasForge: false,
                DpiScale: 2));

        Assert.AreEqual(1600, result.Width);
        Assert.AreEqual(868, result.Height);
        Assert.IsFalse(result.AppliedLegacyJavaDpiFix);
        Assert.IsNull(result.LogMessage);
    }

    [TestMethod]
    public void BuildPlanAppliesLegacyDpiFixForAffectedJava8WindowSizes()
    {
        var result = MinecraftLaunchResolutionService.BuildPlan(
            new MinecraftLaunchResolutionRequest(
                WindowMode: 3,
                LauncherWindowWidth: null,
                LauncherWindowHeight: null,
                LauncherTitleBarHeight: 0,
                CustomWidth: 1280,
                CustomHeight: 720,
                GameVersionDrop: 120,
                JavaMajorVersion: 8,
                JavaRevision: 271,
                HasOptiFine: false,
                HasForge: false,
                DpiScale: 1.5));

        Assert.AreEqual(853, result.Width);
        Assert.AreEqual(480, result.Height);
        Assert.IsTrue(result.AppliedLegacyJavaDpiFix);
        Assert.AreEqual("Applied legacy oversized-window fix (271)", result.LogMessage);
    }
}

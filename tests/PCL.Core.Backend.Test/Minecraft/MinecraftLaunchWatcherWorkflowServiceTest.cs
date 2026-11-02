using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchWatcherWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanCombinesStartupSummaryAndWatcherRuntimePlan()
    {
        var result = MinecraftLaunchWatcherWorkflowService.BuildPlan(
            new MinecraftLaunchWatcherWorkflowRequest(
                new MinecraftLaunchSessionLogRequest(
                    LauncherVersionName: "2.11.0",
                    LauncherVersionCode: 2110,
                    GameVersionDisplayName: "1.20.5",
                    GameVersionRaw: "1.20.5",
                    GameVersionDrop: 8,
                    IsGameVersionReliable: true,
                    AssetsIndexName: "8",
                    InheritedInstanceName: "Base",
                    AllocatedMemoryInGigabytes: 4,
                    MinecraftFolder: @"C:\Minecraft",
                    InstanceFolder: @"C:\Minecraft\versions\demo",
                    IsVersionIsolated: true,
                    IsHmclFormatJson: false,
                    JavaDescription: "Java 21",
                    NativesFolder: @"C:\Minecraft\natives",
                    PlayerName: "DemoPlayer",
                    AccessToken: "token",
                    ClientToken: "client",
                    Uuid: "uuid",
                    LoginType: "Microsoft"),
                new MinecraftLaunchWatcherRequest(
                    VersionSpecificWindowTitleTemplate: "{version}",
                    VersionTitleExplicitlyEmpty: false,
                    GlobalWindowTitleTemplate: "{global}",
                    JavaFolder: @"C:\Java\bin",
                    JstackExecutableExists: true),
                OutputRealTimeLog: true));

        Assert.IsTrue(result.StartupSummaryLogLines.Any(line => line.Contains("PCL version: 2.11.0 (2110)")));
        Assert.AreEqual("{version}", result.RawWindowTitleTemplate);
        Assert.AreEqual(@"C:\Java\bin/jstack.exe".Replace('/', System.IO.Path.DirectorySeparatorChar), result.JstackExecutablePath.Replace('\\', System.IO.Path.DirectorySeparatorChar));
        Assert.IsTrue(result.ShouldAttachRealtimeLog);
        Assert.AreEqual("Game real-time logs are being shown", result.RealtimeLogAttachedMessage);
    }

    [TestMethod]
    public void BuildPlanOmitsRealtimeLogMessageWhenNotRequested()
    {
        var result = MinecraftLaunchWatcherWorkflowService.BuildPlan(
            new MinecraftLaunchWatcherWorkflowRequest(
                new MinecraftLaunchSessionLogRequest(
                    LauncherVersionName: "2.11.0",
                    LauncherVersionCode: 2110,
                    GameVersionDisplayName: "1.20.5",
                    GameVersionRaw: "1.20.5",
                    GameVersionDrop: 8,
                    IsGameVersionReliable: true,
                    AssetsIndexName: "8",
                    InheritedInstanceName: null,
                    AllocatedMemoryInGigabytes: 4,
                    MinecraftFolder: @"C:\Minecraft",
                    InstanceFolder: @"C:\Minecraft\versions\demo",
                    IsVersionIsolated: false,
                    IsHmclFormatJson: false,
                    JavaDescription: null,
                    NativesFolder: @"C:\Minecraft\natives",
                    PlayerName: "DemoPlayer",
                    AccessToken: "token",
                    ClientToken: "client",
                    Uuid: "uuid",
                    LoginType: "Microsoft"),
                new MinecraftLaunchWatcherRequest(
                    VersionSpecificWindowTitleTemplate: null,
                    VersionTitleExplicitlyEmpty: true,
                    GlobalWindowTitleTemplate: "{global}",
                    JavaFolder: @"C:\Java\bin",
                    JstackExecutableExists: false),
                OutputRealTimeLog: false));

        Assert.AreEqual(string.Empty, result.RawWindowTitleTemplate);
        Assert.AreEqual(string.Empty, result.JstackExecutablePath);
        Assert.IsFalse(result.ShouldAttachRealtimeLog);
        Assert.IsNull(result.RealtimeLogAttachedMessage);
    }
}

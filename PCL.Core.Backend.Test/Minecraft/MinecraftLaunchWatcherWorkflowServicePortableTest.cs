using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Backend.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchWatcherWorkflowServicePortableTest
{
    [TestMethod]
    public void BuildPlanPreservesWindowsStyleJstackPathWhenInputUsesBackslashes()
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

        Assert.IsTrue(result.StartupSummaryLogLines.Any(line => line.Contains("PCL 版本：2.11.0 (2110)")));
        Assert.AreEqual("{version}", result.RawWindowTitleTemplate);
        Assert.AreEqual(@"C:\Java\bin\jstack.exe", result.JstackExecutablePath);
        Assert.IsTrue(result.ShouldAttachRealtimeLog);
    }
}

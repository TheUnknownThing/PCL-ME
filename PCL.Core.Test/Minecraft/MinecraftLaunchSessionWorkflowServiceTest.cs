using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchSessionWorkflowServiceTest
{
    [TestMethod]
    public void BuildStartPlanCombinesCustomCommandProcessAndWatcherPlans()
    {
        var result = MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            new MinecraftLaunchSessionStartWorkflowRequest(
                new MinecraftLaunchCustomCommandWorkflowRequest(
                    new MinecraftLaunchCustomCommandRequest(
                        JavaMajorVersion: 21,
                        InstanceName: "Demo",
                        WorkingDirectory: @"D:\Instances\Demo",
                        JavaExecutablePath: @"C:\Java\bin\java.exe",
                        LaunchArguments: "--demo",
                        GlobalCommand: "echo global",
                        WaitForGlobalCommand: true,
                        InstanceCommand: "echo instance",
                        WaitForInstanceCommand: false),
                    ShellWorkingDirectory: @"D:\Minecraft"),
                new MinecraftLaunchProcessRequest(
                    PreferConsoleJava: false,
                    JavaExecutablePath: @"C:\Java\bin\java.exe",
                    JavawExecutablePath: @"C:\Java\bin\javaw.exe",
                    JavaFolder: @"C:\Java\bin",
                    CurrentPathEnvironmentValue: string.Join(Path.PathSeparator, [@"C:\Windows", @"C:\Tools"]),
                    AppDataPath: @"D:\Minecraft\.minecraft",
                    WorkingDirectory: @"D:\Instances\Demo",
                    LaunchArguments: "--demo",
                    PrioritySetting: 0),
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
                        MinecraftFolder: @"D:\Minecraft\.minecraft",
                        InstanceFolder: @"D:\Instances\Demo",
                        IsVersionIsolated: true,
                        IsHmclFormatJson: false,
                        JavaDescription: "Java 21",
                        NativesFolder: @"D:\Minecraft\natives",
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
                    OutputRealTimeLog: true)));

        StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "echo global");
        Assert.AreEqual(2, result.CustomCommandShellPlans.Count);
        Assert.AreEqual(@"D:\Minecraft", result.CustomCommandShellPlans[0].WorkingDirectory);
        Assert.AreEqual(@"C:\Java\bin\javaw.exe", result.ProcessShellPlan.FileName);
        Assert.AreEqual("{version}", result.WatcherWorkflowPlan.RawWindowTitleTemplate);
        Assert.IsTrue(result.WatcherWorkflowPlan.ShouldAttachRealtimeLog);
    }

    [TestMethod]
    public void BuildStartPlanSkipsShellPlansWhenNoCustomCommandsExist()
    {
        var result = MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            new MinecraftLaunchSessionStartWorkflowRequest(
                new MinecraftLaunchCustomCommandWorkflowRequest(
                    new MinecraftLaunchCustomCommandRequest(
                        JavaMajorVersion: 8,
                        InstanceName: "Demo",
                        WorkingDirectory: @"D:\Instances\Demo",
                        JavaExecutablePath: @"C:\Java\bin\java.exe",
                        LaunchArguments: "--demo",
                        GlobalCommand: null,
                        WaitForGlobalCommand: false,
                        InstanceCommand: null,
                        WaitForInstanceCommand: false),
                    ShellWorkingDirectory: @"D:\Minecraft"),
                new MinecraftLaunchProcessRequest(
                    PreferConsoleJava: true,
                    JavaExecutablePath: @"C:\Java\bin\java.exe",
                    JavawExecutablePath: @"C:\Java\bin\javaw.exe",
                    JavaFolder: @"C:\Java\bin",
                    CurrentPathEnvironmentValue: string.Empty,
                    AppDataPath: @"D:\Minecraft\.minecraft",
                    WorkingDirectory: @"D:\Instances\Demo",
                    LaunchArguments: "--demo",
                    PrioritySetting: 1),
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
                        MinecraftFolder: @"D:\Minecraft\.minecraft",
                        InstanceFolder: @"D:\Instances\Demo",
                        IsVersionIsolated: false,
                        IsHmclFormatJson: false,
                        JavaDescription: null,
                        NativesFolder: @"D:\Minecraft\natives",
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
                    OutputRealTimeLog: false)));

        Assert.AreEqual(0, result.CustomCommandShellPlans.Count);
        Assert.AreEqual(@"C:\Java\bin\java.exe", result.ProcessShellPlan.FileName);
        Assert.AreEqual(string.Empty, result.WatcherWorkflowPlan.JstackExecutablePath);
    }
}

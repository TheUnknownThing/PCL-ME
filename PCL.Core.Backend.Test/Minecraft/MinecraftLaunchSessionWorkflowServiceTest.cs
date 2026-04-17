using System.IO;
using System.Collections.Generic;
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
                        EnvironmentVariables: new Dictionary<string, string>(),
                        GlobalCommand: "echo global",
                        WaitForGlobalCommand: true,
                        InstanceCommand: "echo instance",
                        WaitForInstanceCommand: false,
                        WrapperCommand: null),
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
                    WrapperCommand: null,
                    EnvironmentVariables: new Dictionary<string, string>
                    {
                        ["DEMO_ENV"] = "enabled"
                    },
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
        if (OperatingSystem.IsWindows())
        {
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "cd /D \"D:\\Minecraft\"");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "start \"\" /b \"cmd.exe\" /c \"echo instance\"");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "cd /D \"D:\\Instances\\Demo\"");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "set \"Path=C:\\Windows");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "set \"appdata=D:\\Minecraft\\.minecraft\"");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "set \"DEMO_ENV=enabled\"");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "\"C:\\Java\\bin\\javaw.exe\" --demo");
        }
        else
        {
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "cd 'D:\\Minecraft' || exit 1");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "'/bin/sh' -lc \"echo instance\" &");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "cd 'D:\\Instances\\Demo' || exit 1");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "export Path='C:\\Windows");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "export appdata='D:\\Minecraft\\.minecraft'");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "export DEMO_ENV='enabled'");
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "'C:\\Java\\bin\\javaw.exe' --demo");
        }
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
                        EnvironmentVariables: new Dictionary<string, string>(),
                        GlobalCommand: null,
                        WaitForGlobalCommand: false,
                        InstanceCommand: null,
                        WaitForInstanceCommand: false,
                        WrapperCommand: null),
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
                    WrapperCommand: null,
                    EnvironmentVariables: new Dictionary<string, string>(),
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

    [TestMethod]
    public void BuildStartPlanUsesWrapperForProcessAndScript()
    {
        var result = MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            new MinecraftLaunchSessionStartWorkflowRequest(
                new MinecraftLaunchCustomCommandWorkflowRequest(
                    new MinecraftLaunchCustomCommandRequest(
                        JavaMajorVersion: 21,
                        InstanceName: "Demo",
                        WorkingDirectory: @"/tmp/demo",
                        JavaExecutablePath: @"/usr/bin/java",
                        LaunchArguments: "--demo",
                        EnvironmentVariables: new Dictionary<string, string>(),
                        GlobalCommand: null,
                        WaitForGlobalCommand: false,
                        InstanceCommand: null,
                        WaitForInstanceCommand: false,
                        WrapperCommand: "prime-run"),
                    ShellWorkingDirectory: @"/tmp"),
                new MinecraftLaunchProcessRequest(
                    PreferConsoleJava: false,
                    JavaExecutablePath: @"/usr/bin/java",
                    JavawExecutablePath: null,
                    JavaFolder: @"/usr/bin",
                    CurrentPathEnvironmentValue: @"/usr/local/bin",
                    AppDataPath: @"/tmp/.minecraft",
                    WorkingDirectory: @"/tmp/demo",
                    LaunchArguments: "--demo",
                    WrapperCommand: "prime-run",
                    EnvironmentVariables: new Dictionary<string, string>(),
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
                        MinecraftFolder: @"/tmp/.minecraft",
                        InstanceFolder: @"/tmp/demo",
                        IsVersionIsolated: false,
                        IsHmclFormatJson: false,
                        JavaDescription: null,
                        NativesFolder: @"/tmp/natives",
                        PlayerName: "DemoPlayer",
                        AccessToken: "token",
                        ClientToken: "client",
                        Uuid: "uuid",
                        LoginType: "Microsoft"),
                    new MinecraftLaunchWatcherRequest(
                        VersionSpecificWindowTitleTemplate: null,
                        VersionTitleExplicitlyEmpty: false,
                        GlobalWindowTitleTemplate: "{global}",
                        JavaFolder: @"/usr/bin",
                        JstackExecutableExists: false),
                    OutputRealTimeLog: false)));

        Assert.AreEqual("prime-run", result.ProcessShellPlan.FileName);
        Assert.AreEqual("\"/usr/bin/java\" --demo", result.ProcessShellPlan.Arguments);
        if (OperatingSystem.IsWindows())
        {
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "\"prime-run\" \"/usr/bin/java\" --demo");
        }
        else
        {
            StringAssert.Contains(result.CustomCommandPlan.BatchScriptContent, "'prime-run' \"/usr/bin/java\" --demo");
        }
    }
}

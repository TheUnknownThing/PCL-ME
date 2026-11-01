using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.Linq;
using System.Collections.Generic;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchCustomCommandServiceTest
{
    [TestMethod]
    public void BuildPlanUsesUtf8BatchHeaderAndIncludesBothCustomCommands()
    {
        var result = MinecraftLaunchCustomCommandService.BuildPlan(new MinecraftLaunchCustomCommandRequest(
            JavaMajorVersion: 17,
            InstanceName: "Fabric 1.20.1",
            WorkingDirectory: @"C:\Minecraft",
            JavaExecutablePath: @"C:\Java\bin\java.exe",
            LaunchArguments: "--demo",
                        EnvironmentVariables: new Dictionary<string, string>(),
                        GlobalCommand: "echo global",
                        WaitForGlobalCommand: true,
                        InstanceCommand: "echo version",
                        WaitForInstanceCommand: false,
                        WrapperCommand: null));

        var expectedScript = OperatingSystem.IsWindows()
            ? string.Join("\r\n", new[]
            {
                "chcp 65001>nul",
                "@echo off",
                "title Launch - Fabric 1.20.1",
                        "echo Game is starting, please wait.",
                        "cd /D \"C:\\Minecraft\"",
                        "echo global",
                        "echo version",
                "\"C:\\Java\\bin\\java.exe\" --demo",
                "echo Game has exited.",
                "pause"
            })
            : string.Join("\n", new[]
            {
                "#!/bin/sh",
                "printf '%s\\n' 'Game is starting, please wait.'",
                "cd \"C:\\Minecraft\" || exit 1",
                "echo global",
                "echo version",
                "\"C:\\Java\\bin\\java.exe\" --demo",
                "printf '%s\\n' 'Game has exited.'"
            });

        Assert.AreEqual(expectedScript, result.BatchScriptContent);
        Assert.IsTrue(result.UseUtf8Encoding);
        Assert.AreEqual(2, result.CommandExecutions.Count);
        Assert.AreEqual(MinecraftLaunchCustomCommandScope.Global, result.CommandExecutions[0].Scope);
        Assert.IsTrue(result.CommandExecutions[0].WaitForExit);
        Assert.AreEqual(MinecraftLaunchCustomCommandScope.Instance, result.CommandExecutions[1].Scope);
        Assert.IsFalse(result.CommandExecutions[1].WaitForExit);
    }

    [TestMethod]
    public void BuildPlanSkipsEmptyCommandsAndKeepsLegacyEncodingForJavaEight()
    {
        var result = MinecraftLaunchCustomCommandService.BuildPlan(new MinecraftLaunchCustomCommandRequest(
            JavaMajorVersion: 8,
            InstanceName: "Vanilla",
            WorkingDirectory: @"D:\Games\.minecraft",
            JavaExecutablePath: @"D:\Java\bin\java.exe",
            LaunchArguments: "--fullscreen",
                        EnvironmentVariables: new Dictionary<string, string>(),
                        GlobalCommand: "",
                        WaitForGlobalCommand: false,
                        InstanceCommand: null,
                        WaitForInstanceCommand: false,
                        WrapperCommand: null));

        Assert.IsFalse(result.UseUtf8Encoding);
        Assert.AreEqual(0, result.CommandExecutions.Count);
        Assert.IsFalse(result.BatchScriptContent.Split("\r\n").Contains("chcp 65001>nul"));
        StringAssert.Contains(result.BatchScriptContent, "\"D:\\Java\\bin\\java.exe\" --fullscreen");
    }

    [TestMethod]
    public void BuildPlanPrefixesWrapperCommandBeforeJavaLaunch()
    {
        var result = MinecraftLaunchCustomCommandService.BuildPlan(new MinecraftLaunchCustomCommandRequest(
            JavaMajorVersion: 21,
            InstanceName: "Vanilla",
            WorkingDirectory: @"/tmp/minecraft",
            JavaExecutablePath: @"/usr/bin/java",
            LaunchArguments: "--demo",
            EnvironmentVariables: new Dictionary<string, string>(),
            GlobalCommand: null,
            WaitForGlobalCommand: false,
            InstanceCommand: null,
            WaitForInstanceCommand: false,
            WrapperCommand: "prime-run"));

        StringAssert.Contains(result.BatchScriptContent, "prime-run \"/usr/bin/java\" --demo");
    }
}

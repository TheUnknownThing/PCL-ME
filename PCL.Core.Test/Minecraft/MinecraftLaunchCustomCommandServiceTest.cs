using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.Linq;

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
            GlobalCommand: "echo global",
            WaitForGlobalCommand: true,
            InstanceCommand: "echo version",
            WaitForInstanceCommand: false));

        var expectedScript = string.Join("\r\n", new[]
        {
            "chcp 65001>nul",
            "@echo off",
            "title 启动 - Fabric 1.20.1",
            "echo 游戏正在启动，请稍候。",
            "cd /D \"C:\\Minecraft\"",
            "echo global",
            "echo version",
            "\"C:\\Java\\bin\\java.exe\" --demo",
            "echo 游戏已退出。",
            "pause"
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
            GlobalCommand: "",
            WaitForGlobalCommand: false,
            InstanceCommand: null,
            WaitForInstanceCommand: false));

        Assert.IsFalse(result.UseUtf8Encoding);
        Assert.AreEqual(0, result.CommandExecutions.Count);
        Assert.IsFalse(result.BatchScriptContent.Split("\r\n").Contains("chcp 65001>nul"));
        StringAssert.Contains(result.BatchScriptContent, "\"D:\\Java\\bin\\java.exe\" --fullscreen");
    }
}

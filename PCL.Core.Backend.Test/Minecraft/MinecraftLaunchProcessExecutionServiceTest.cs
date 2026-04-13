using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchProcessExecutionServiceTest
{
    [TestMethod]
    public void BuildCustomCommandStartRequestCopiesShellPlanValues()
    {
        var result = MinecraftLaunchProcessExecutionService.BuildCustomCommandStartRequest(
            new MinecraftLaunchCustomCommandShellPlan(
                FileName: "cmd.exe",
                Arguments: "/c \"echo demo\"",
                WorkingDirectory: @"C:\Minecraft",
                UseShellExecute: false,
                CreateNoWindow: true,
                WaitForExit: true,
                StartLogMessage: "start",
                FailureLogMessage: "fail",
                AbortKillLogMessage: "abort"));

        Assert.AreEqual("cmd.exe", result.FileName);
        Assert.AreEqual("/c \"echo demo\"", result.Arguments);
        Assert.AreEqual(@"C:\Minecraft", result.WorkingDirectory);
        Assert.IsFalse(result.UseShellExecute);
        Assert.IsTrue(result.CreateNoWindow);
        Assert.IsFalse(result.RedirectStandardOutput);
        Assert.IsFalse(result.RedirectStandardError);
    }

    [TestMethod]
    public void BuildGameProcessStartRequestCopiesRedirectsAndEnvironment()
    {
        var result = MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(
            new MinecraftLaunchProcessShellPlan(
                FileName: @"C:\Java\bin\javaw.exe",
                Arguments: "--demo",
                WorkingDirectory: @"C:\Minecraft\.minecraft",
                CreateNoWindow: false,
                UseShellExecute: false,
                RedirectStandardOutput: true,
                RedirectStandardError: true,
                PathEnvironmentValue: @"C:\Windows\System32",
                AppDataEnvironmentValue: @"C:\Minecraft\.minecraft",
                PriorityKind: MinecraftLaunchProcessPriorityKind.AboveNormal,
                StartedLogMessage: "started",
                AbortKillLogMessage: "aborted"));

        Assert.AreEqual(@"C:\Java\bin\javaw.exe", result.FileName);
        Assert.AreEqual("--demo", result.Arguments);
        Assert.AreEqual(@"C:\Minecraft\.minecraft", result.WorkingDirectory);
        Assert.IsTrue(result.RedirectStandardOutput);
        Assert.IsTrue(result.RedirectStandardError);
        Assert.AreEqual(@"C:\Windows\System32", result.EnvironmentVariables!["Path"]);
        Assert.AreEqual(@"C:\Minecraft\.minecraft", result.EnvironmentVariables["appdata"]);
    }

    [TestMethod]
    public void TryApplyPrioritySucceedsForCurrentProcessWithNoPriorityChange()
    {
        var result = MinecraftLaunchProcessExecutionService.TryApplyPriority(
            Process.GetCurrentProcess(),
            MinecraftLaunchProcessPriorityKind.Normal);

        Assert.IsTrue(result);
    }
}

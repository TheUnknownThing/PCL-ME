using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.IO;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchExecutionWorkflowServiceTest
{
    [TestMethod]
    public void BuildCustomCommandShellPlanBuildsCmdInvocation()
    {
        var result = MinecraftLaunchExecutionWorkflowService.BuildCustomCommandShellPlan(
            new MinecraftLaunchCustomCommandShellRequest(
                Command: "echo hello",
                WaitForExit: true,
                WorkingDirectory: @"C:\Minecraft",
                StartLogMessage: "start",
                FailureLogMessage: "fail"));

        Assert.AreEqual("cmd.exe", result.FileName);
        Assert.AreEqual("/c \"echo hello\"", result.Arguments);
        Assert.AreEqual(@"C:\Minecraft", result.WorkingDirectory);
        Assert.IsFalse(result.UseShellExecute);
        Assert.IsTrue(result.CreateNoWindow);
        Assert.IsTrue(result.WaitForExit);
        Assert.AreEqual("由于取消启动，已强制结束自定义命令 CMD 进程", result.AbortKillLogMessage);
    }

    [TestMethod]
    public void BuildProcessShellPlanWrapsRuntimePlanAndAddsLaunchLogs()
    {
        var result = MinecraftLaunchExecutionWorkflowService.BuildProcessShellPlan(
            new MinecraftLaunchProcessRequest(
                PreferConsoleJava: false,
                JavaExecutablePath: @"C:\Java\bin\java.exe",
                JavawExecutablePath: @"C:\Java\bin\javaw.exe",
                JavaFolder: @"C:\Java\bin",
                CurrentPathEnvironmentValue: string.Join(Path.PathSeparator, [@"C:\Windows", @"C:\Tools"]),
                AppDataPath: @"D:\.minecraft",
                WorkingDirectory: @"D:\Instances\Fabric",
                LaunchArguments: "--demo",
                PrioritySetting: 0));

        Assert.AreEqual(@"C:\Java\bin\javaw.exe", result.FileName);
        Assert.AreEqual("--demo", result.Arguments);
        Assert.AreEqual(@"D:\Instances\Fabric", result.WorkingDirectory);
        Assert.IsFalse(result.UseShellExecute);
        Assert.IsTrue(result.RedirectStandardOutput);
        Assert.IsTrue(result.RedirectStandardError);
        Assert.AreEqual(@"D:\.minecraft", result.AppDataEnvironmentValue);
        Assert.AreEqual(MinecraftLaunchProcessPriorityKind.AboveNormal, result.PriorityKind);
        Assert.AreEqual(@"已启动游戏进程：C:\Java\bin\javaw.exe", result.StartedLogMessage);
        Assert.AreEqual("由于取消启动，已强制结束游戏进程", result.AbortKillLogMessage);
    }
}

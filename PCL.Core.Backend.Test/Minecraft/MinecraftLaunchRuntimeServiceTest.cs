using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.IO;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchRuntimeServiceTest
{
    [TestMethod]
    public void BuildProcessPlanAppendsJavaFolderAndUsesConsoleJavaWhenRequested()
    {
        var result = MinecraftLaunchRuntimeService.BuildProcessPlan(new MinecraftLaunchProcessRequest(
            PreferConsoleJava: true,
            JavaExecutablePath: @"C:\Java\bin\java.exe",
            JavawExecutablePath: @"C:\Java\bin\javaw.exe",
            JavaFolder: @"C:\Java\bin",
            CurrentPathEnvironmentValue: string.Join(Path.PathSeparator, [@"C:\Windows", @"C:\Tools"]),
            AppDataPath: @"D:\.minecraft",
            WorkingDirectory: @"D:\Instances\Fabric",
            LaunchArguments: "--demo",
            PrioritySetting: 0));

        Assert.AreEqual(@"C:\Java\bin\java.exe", result.ExecutablePath);
        Assert.AreEqual(@"D:\Instances\Fabric", result.WorkingDirectory);
        Assert.IsTrue(result.CreateNoWindow);
        Assert.AreEqual("--demo", result.LaunchArguments);
        Assert.AreEqual(string.Join(Path.PathSeparator, [@"C:\Windows", @"C:\Tools", @"C:\Java\bin"]), result.PathEnvironmentValue);
        Assert.AreEqual(@"D:\.minecraft", result.AppDataEnvironmentValue);
        Assert.AreEqual(MinecraftLaunchProcessPriorityKind.AboveNormal, result.PriorityKind);
    }

    [TestMethod]
    public void BuildProcessPlanFallsBackToJavaExeWhenJavawIsUnavailable()
    {
        var result = MinecraftLaunchRuntimeService.BuildProcessPlan(new MinecraftLaunchProcessRequest(
            PreferConsoleJava: false,
            JavaExecutablePath: @"/usr/bin/java",
            JavawExecutablePath: null,
            JavaFolder: @"/usr/bin",
            CurrentPathEnvironmentValue: @"/usr/local/bin",
            AppDataPath: @"/tmp/.minecraft",
            WorkingDirectory: @"/tmp/instance",
            LaunchArguments: "--width 1280",
            PrioritySetting: 2));

        Assert.AreEqual(@"/usr/bin/java", result.ExecutablePath);
        Assert.IsTrue(result.CreateNoWindow);
        Assert.AreEqual(MinecraftLaunchProcessPriorityKind.BelowNormal, result.PriorityKind);
    }

    [TestMethod]
    public void BuildWatcherPlanFallsBackToGlobalTitleUnlessVersionExplicitlyClearsIt()
    {
        var fallbackResult = MinecraftLaunchRuntimeService.BuildWatcherPlan(new MinecraftLaunchWatcherRequest(
            VersionSpecificWindowTitleTemplate: "",
            VersionTitleExplicitlyEmpty: false,
            GlobalWindowTitleTemplate: "Global Title",
            JavaFolder: @"C:\Java\bin",
            JstackExecutableExists: true));

        Assert.AreEqual("Global Title", fallbackResult.RawWindowTitleTemplate);
        Assert.AreEqual(Path.Combine(@"C:\Java\bin", "jstack.exe"), fallbackResult.JstackExecutablePath);

        var explicitEmptyResult = MinecraftLaunchRuntimeService.BuildWatcherPlan(new MinecraftLaunchWatcherRequest(
            VersionSpecificWindowTitleTemplate: "",
            VersionTitleExplicitlyEmpty: true,
            GlobalWindowTitleTemplate: "Global Title",
            JavaFolder: @"C:\Java\bin",
            JstackExecutableExists: false));

        Assert.AreEqual(string.Empty, explicitEmptyResult.RawWindowTitleTemplate);
        Assert.AreEqual(string.Empty, explicitEmptyResult.JstackExecutablePath);
    }
}

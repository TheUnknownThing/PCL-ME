using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchWrapperCommandServiceTest
{
    [TestMethod]
    public void BuildPlanLeavesLaunchUntouchedWhenWrapperIsEmpty()
    {
        var result = MinecraftLaunchWrapperCommandService.BuildPlan(
            new MinecraftLaunchWrapperCommandRequest(
                TargetExecutablePath: @"/usr/bin/java",
                TargetArguments: "--demo",
                WrapperCommand: ""));

        Assert.AreEqual(@"/usr/bin/java", result.ExecutablePath);
        Assert.AreEqual("--demo", result.Arguments);
        Assert.AreEqual("\"/usr/bin/java\" --demo", result.ShellCommandLine);
    }

    [TestMethod]
    public void BuildPlanPrefixesWrapperExecutableAndArguments()
    {
        var result = MinecraftLaunchWrapperCommandService.BuildPlan(
            new MinecraftLaunchWrapperCommandRequest(
                TargetExecutablePath: @"/usr/bin/java",
                TargetArguments: "--demo",
                WrapperCommand: "env FOO=bar"));

        Assert.AreEqual("env", result.ExecutablePath);
        Assert.AreEqual("FOO=bar \"/usr/bin/java\" --demo", result.Arguments);
        Assert.AreEqual("env FOO=bar \"/usr/bin/java\" --demo", result.ShellCommandLine);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchJavaWrapperServiceTest
{
    [TestMethod]
    public void ShouldUseReturnsTrueOnlyOnSupportedWindowsJava()
    {
        var result = MinecraftLaunchJavaWrapperService.ShouldUse(
            new MinecraftLaunchJavaWrapperRequest(
                IsRequested: true,
                IsWindowsPlatform: true,
                JavaMajorVersion: 17,
                JavaWrapperTempDirectory: @"C:\Temp",
                JavaWrapperPath: @"C:\Temp\JavaWrapper.jar"));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldUseReturnsFalseOutsideWindows()
    {
        var result = MinecraftLaunchJavaWrapperService.ShouldUse(
            new MinecraftLaunchJavaWrapperRequest(
                IsRequested: true,
                IsWindowsPlatform: false,
                JavaMajorVersion: 17,
                JavaWrapperTempDirectory: "/tmp",
                JavaWrapperPath: "/tmp/java-wrapper.jar"));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseReturnsFalseWhenJavaAlreadyContainsTheUtf8Fix()
    {
        var result = MinecraftLaunchJavaWrapperService.ShouldUse(
            new MinecraftLaunchJavaWrapperRequest(
                IsRequested: true,
                IsWindowsPlatform: true,
                JavaMajorVersion: 21,
                JavaWrapperTempDirectory: @"C:\Temp",
                JavaWrapperPath: @"C:\Temp\JavaWrapper.jar"));

        Assert.IsFalse(result);
    }
}

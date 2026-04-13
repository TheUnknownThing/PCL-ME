using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Backend.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchRuntimeServicePortableTest
{
    [TestMethod]
    public void BuildWatcherPlanPreservesWindowsStyleJavaFolderWhenInputUsesBackslashes()
    {
        var result = MinecraftLaunchRuntimeService.BuildWatcherPlan(new MinecraftLaunchWatcherRequest(
            VersionSpecificWindowTitleTemplate: "",
            VersionTitleExplicitlyEmpty: false,
            GlobalWindowTitleTemplate: "Global Title",
            JavaFolder: @"C:\Java\bin",
            JstackExecutableExists: true));

        Assert.AreEqual("Global Title", result.RawWindowTitleTemplate);
        Assert.AreEqual(@"C:\Java\bin\jstack.exe", result.JstackExecutablePath);
    }
}

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchClasspathServiceTest
{
    [TestMethod]
    public void BuildPlanOrdersSpecialEntriesLikeLauncherClasspathAssembly()
    {
        var result = MinecraftLaunchClasspathService.BuildPlan(
            new MinecraftLaunchClasspathRequest(
                Libraries:
                [
                    new MinecraftLaunchClasspathLibrary("com.cleanroommc:cleanroom:0.2.4-alpha", @"C:\libs\cleanroom.jar", false),
                    new MinecraftLaunchClasspathLibrary("com.example:normal", @"C:\libs\normal.jar", false),
                    new MinecraftLaunchClasspathLibrary("optifine:OptiFine", @"C:\libs\optifine.jar", false),
                    new MinecraftLaunchClasspathLibrary("com.example:natives", @"C:\libs\natives.jar", true),
                    new MinecraftLaunchClasspathLibrary("com.example:last", @"C:\libs\last.jar", false)
                ],
                CustomHeadEntries: ["C:\\head\\first.jar", "C:\\head\\second.jar"],
                RetroWrapperPath: @"C:\libs\RetroWrapper.jar",
                ClasspathSeparator: ";"));

        CollectionAssert.AreEqual(
            new List<string>
            {
                @"C:\head\second.jar",
                @"C:\head\first.jar",
                @"C:\libs\cleanroom.jar",
                @"C:\libs\RetroWrapper.jar",
                @"C:\libs\cleanroom.jar",
                @"C:\libs\optifine.jar",
                @"C:\libs\normal.jar",
                @"C:\libs\last.jar"
            },
            (System.Collections.ICollection)result.Entries);
        Assert.AreEqual(
            @"C:\head\second.jar;C:\head\first.jar;C:\libs\cleanroom.jar;C:\libs\RetroWrapper.jar;C:\libs\cleanroom.jar;C:\libs\optifine.jar;C:\libs\normal.jar;C:\libs\last.jar",
            result.JoinedClasspath);
    }

    [TestMethod]
    public void BuildPlanIgnoresBlankEntriesAndHandlesOptiFineWithoutTrailingLibraries()
    {
        var result = MinecraftLaunchClasspathService.BuildPlan(
            new MinecraftLaunchClasspathRequest(
                Libraries:
                [
                    new MinecraftLaunchClasspathLibrary("optifine:OptiFine", @"/libs/optifine.jar", false)
                ],
                CustomHeadEntries: ["", "  "],
                RetroWrapperPath: null,
                ClasspathSeparator: ":"));

        CollectionAssert.AreEqual(
            new List<string> { @"/libs/optifine.jar" },
            (System.Collections.ICollection)result.Entries);
        Assert.AreEqual(@"/libs/optifine.jar", result.JoinedClasspath);
    }
}

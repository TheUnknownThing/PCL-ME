using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchNativesDirectoryServiceTest
{
    [TestMethod]
    public void ResolvePathPrefersInstanceDirectoryWhenAsciiOrForced()
    {
        var asciiResult = MinecraftLaunchNativesDirectoryService.ResolvePath(
            new MinecraftLaunchNativesDirectoryRequest(
                PreferredInstanceDirectory: @"C:\Minecraft\demo-natives",
                PreferInstanceDirectory: false,
                AppDataNativesDirectory: @"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives",
                FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"));

        Assert.AreEqual(@"C:\Minecraft\demo-natives", asciiResult);

        var forcedResult = MinecraftLaunchNativesDirectoryService.ResolvePath(
            new MinecraftLaunchNativesDirectoryRequest(
                PreferredInstanceDirectory: @"C:\测试\demo-natives",
                PreferInstanceDirectory: true,
                AppDataNativesDirectory: @"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives",
                FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"));

        Assert.AreEqual(@"C:\测试\demo-natives", forcedResult);
    }

    [TestMethod]
    public void ResolvePathFallsBackWhenPreferredDirectoriesAreNonAscii()
    {
        var appDataResult = MinecraftLaunchNativesDirectoryService.ResolvePath(
            new MinecraftLaunchNativesDirectoryRequest(
                PreferredInstanceDirectory: @"C:\测试\demo-natives",
                PreferInstanceDirectory: false,
                AppDataNativesDirectory: @"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives",
                FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"));

        Assert.AreEqual(@"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives", appDataResult);

        var finalFallbackResult = MinecraftLaunchNativesDirectoryService.ResolvePath(
            new MinecraftLaunchNativesDirectoryRequest(
                PreferredInstanceDirectory: @"C:\测试\demo-natives",
                PreferInstanceDirectory: false,
                AppDataNativesDirectory: @"C:\用户\漫游\.minecraft\bin\natives",
                FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"));

        Assert.AreEqual(@"C:\ProgramData\PCL\natives", finalFallbackResult);
    }
}

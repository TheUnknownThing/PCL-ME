using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchReplacementValueServiceTest
{
    [TestMethod]
    public void BuildPlanProducesExpectedMinecraftTokenMap()
    {
        var result = MinecraftLaunchReplacementValueService.BuildPlan(
            new MinecraftLaunchReplacementValueRequest(
                ClasspathSeparator: ";",
                NativesDirectory: @"C:\Minecraft\natives",
                LibraryDirectory: @"C:\Minecraft\libraries",
                LibrariesDirectory: @"C:\Minecraft\libraries",
                LauncherName: "PCLME",
                LauncherVersion: "2110",
                VersionName: "Demo",
                VersionType: "PCL-ME",
                GameDirectory: @"C:\Minecraft\instances\demo",
                AssetsRoot: @"C:\Minecraft\assets",
                UserProperties: "{}",
                AuthPlayerName: "DemoPlayer",
                AuthUuid: "demo-uuid",
                AccessToken: "demo-token",
                UserType: "msa",
                ResolutionWidth: 1280,
                ResolutionHeight: 720,
                GameAssetsDirectory: @"C:\Minecraft\assets\virtual\legacy",
                AssetsIndexName: "8",
                Classpath: @"C:\libs\a.jar;C:\libs\b.jar"));

        Assert.AreEqual("PCLME", result.Values["${launcher_name}"]);
        Assert.AreEqual("2110", result.Values["${launcher_version}"]);
        Assert.AreEqual("DemoPlayer", result.Values["${auth_player_name}"]);
        Assert.AreEqual("demo-uuid", result.Values["${auth_uuid}"]);
        Assert.AreEqual("demo-token", result.Values["${auth_access_token}"]);
        Assert.AreEqual("demo-token", result.Values["${access_token}"]);
        Assert.AreEqual("1280", result.Values["${resolution_width}"]);
        Assert.AreEqual("720", result.Values["${resolution_height}"]);
        Assert.AreEqual(@"C:\libs\a.jar;C:\libs\b.jar", result.Values["${classpath}"]);
    }
}

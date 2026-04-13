using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchOptionsFileServiceTest
{
    [TestMethod]
    public void BuildPlanFallsBackToYosbrAndInitializesLanguageWhenPrimaryOptionsAreMissing()
    {
        var result = MinecraftLaunchOptionsFileService.BuildPlan(new MinecraftLaunchOptionsSyncRequest(
            AutoChangeLanguage: true,
            PrimaryOptionsFileExists: false,
            PrimaryCurrentLanguage: "en_us",
            YosbrOptionsFileExists: true,
            HasExistingSaves: false,
            ReleaseTime: new DateTime(2018, 1, 1),
            LaunchWindowType: 0));

        Assert.AreEqual(MinecraftLaunchOptionsFileTargetKind.Yosbr, result.TargetKind);
        Assert.AreEqual("将修改 Yosbr Mod 中的 options.txt", result.TargetSelectionLogMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                new MinecraftLaunchOptionWrite("lang", "none"),
                new MinecraftLaunchOptionWrite("lang", "-"),
                new MinecraftLaunchOptionWrite("lang", "zh_cn"),
                new MinecraftLaunchOptionWrite("forceUnicodeFont", "true"),
                new MinecraftLaunchOptionWrite("fullscreen", "true")
            },
            result.Writes.ToArray());
    }

    [TestMethod]
    public void BuildPlanUsesLegacyUppercaseLocaleForMinecraftOneToTen()
    {
        var result = MinecraftLaunchOptionsFileService.BuildPlan(new MinecraftLaunchOptionsSyncRequest(
            AutoChangeLanguage: true,
            PrimaryOptionsFileExists: true,
            PrimaryCurrentLanguage: "en_us",
            YosbrOptionsFileExists: false,
            HasExistingSaves: true,
            ReleaseTime: new DateTime(2014, 6, 26),
            LaunchWindowType: 1));

        CollectionAssert.AreEqual(
            new[]
            {
                new MinecraftLaunchOptionWrite("lang", "-"),
                new MinecraftLaunchOptionWrite("lang", "zh_CN")
            },
            result.Writes.ToArray());
        CollectionAssert.AreEqual(
            new[] { "已将语言从 en_us 修改为 zh_CN" },
            result.LogMessages.ToArray());
    }

    [TestMethod]
    public void BuildPlanKeepsExistingLanguageWhenAlreadyCompatibleAndOnlyUpdatesFullscreen()
    {
        var result = MinecraftLaunchOptionsFileService.BuildPlan(new MinecraftLaunchOptionsSyncRequest(
            AutoChangeLanguage: true,
            PrimaryOptionsFileExists: true,
            PrimaryCurrentLanguage: "zh_cn",
            YosbrOptionsFileExists: false,
            HasExistingSaves: true,
            ReleaseTime: new DateTime(2023, 1, 1),
            LaunchWindowType: 3));

        CollectionAssert.AreEqual(
            new[]
            {
                new MinecraftLaunchOptionWrite("fullscreen", "false")
            },
            result.Writes.ToArray());
        CollectionAssert.AreEqual(
            new[] { "需要的语言为 zh_cn，当前语言为 zh_cn，无需修改" },
            result.LogMessages.ToArray());
    }
}

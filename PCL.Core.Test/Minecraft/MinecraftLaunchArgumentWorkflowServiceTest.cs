using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchArgumentWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanAddsEncodingFlagsReplacesTokensAndUsesQuickPlayForModernVersions()
    {
        var result = MinecraftLaunchArgumentWorkflowService.BuildPlan(
            new MinecraftLaunchArgumentPlanRequest(
                BaseArguments: "-Xmx4G -Dos.name=Windows 10 --username ${auth_player_name} --versionType ${version_type} --gameDir ${game_directory}",
                JavaMajorVersion: 21,
                UseFullscreen: true,
                ExtraArguments: ["--demo"],
                CustomGameArguments: "--server example.invalid",
                ReplacementValues: new Dictionary<string, string>
                {
                    ["${auth_player_name}"] = "Demo Player",
                    ["${version_type}"] = "PCL CE",
                    ["${game_directory}"] = @"C:\Minecraft Instances\Demo"
                },
                WorldName: null,
                ServerAddress: "play.example.invalid",
                ReleaseTime: new DateTime(2024, 4, 23),
                HasOptiFine: false));

        Assert.AreEqual(
            "-Dfile.encoding=COMPAT -Dstderr.encoding=UTF-8 -Dstdout.encoding=UTF-8 -Xmx4G -Dos.name=\"Windows 10\" --username \"Demo Player\" --versionType \"PCL CE\" --gameDir \"C:\\Minecraft Instances\\Demo\" --fullscreen --demo --server example.invalid --quickPlayMultiplayer \"play.example.invalid\"",
            result.FinalArguments);
        Assert.IsFalse(result.ShouldWarnAboutLegacyServerWithOptiFine);
    }

    [TestMethod]
    public void BuildPlanOmitsVersionTypePlaceholderAndFallsBackToLegacyServerArguments()
    {
        var result = MinecraftLaunchArgumentWorkflowService.BuildPlan(
            new MinecraftLaunchArgumentPlanRequest(
                BaseArguments: "--versionType ${version_type} --accessToken ${access_token}",
                JavaMajorVersion: 8,
                UseFullscreen: false,
                ExtraArguments: [],
                CustomGameArguments: null,
                ReplacementValues: new Dictionary<string, string>
                {
                    ["${version_type}"] = "",
                    ["${access_token}"] = "token"
                },
                WorldName: null,
                ServerAddress: "mc.example.invalid:25570",
                ReleaseTime: new DateTime(2020, 1, 1),
                HasOptiFine: true));

        Assert.AreEqual("--accessToken token --server mc.example.invalid --port 25570", result.FinalArguments);
        Assert.IsTrue(result.ShouldWarnAboutLegacyServerWithOptiFine);
    }

    [TestMethod]
    public void BuildPlanPrefersSingleplayerQuickPlayWhenWorldNameIsProvided()
    {
        var result = MinecraftLaunchArgumentWorkflowService.BuildPlan(
            new MinecraftLaunchArgumentPlanRequest(
                BaseArguments: "--username ${auth_player_name}",
                JavaMajorVersion: 17,
                UseFullscreen: false,
                ExtraArguments: null,
                CustomGameArguments: null,
                ReplacementValues: new Dictionary<string, string>
                {
                    ["${auth_player_name}"] = "DemoPlayer"
                },
                WorldName: "My World",
                ServerAddress: "play.example.invalid",
                ReleaseTime: new DateTime(2024, 4, 23),
                HasOptiFine: false));

        Assert.AreEqual(
            "-Dstderr.encoding=UTF-8 -Dstdout.encoding=UTF-8 --username DemoPlayer --quickPlaySingleplayer \"My World\"",
            result.FinalArguments);
    }
}

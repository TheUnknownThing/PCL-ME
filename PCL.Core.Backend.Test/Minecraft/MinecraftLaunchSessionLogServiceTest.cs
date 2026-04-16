using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.Linq;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchSessionLogServiceTest
{
    [TestMethod]
    public void BuildStartupSummaryFormatsLaunchAndProfileSections()
    {
        var result = MinecraftLaunchSessionLogService.BuildStartupSummary(new MinecraftLaunchSessionLogRequest(
            LauncherVersionName: "2.9.0",
            LauncherVersionCode: 123,
            GameVersionDisplayName: "1.20.1",
            GameVersionRaw: "1.20.1",
            GameVersionDrop: 5,
            IsGameVersionReliable: false,
            AssetsIndexName: "1.20",
            InheritedInstanceName: "",
            AllocatedMemoryInGigabytes: 3.5,
            MinecraftFolder: @"C:\.minecraft",
            InstanceFolder: @"C:\.minecraft\versions\fabric",
            IsVersionIsolated: true,
            IsHmclFormatJson: false,
            JavaDescription: "Java 17",
            NativesFolder: @"C:\natives",
            PlayerName: "Player",
            AccessToken: "access",
            ClientToken: "client",
            Uuid: "uuid",
            LoginType: "Microsoft"));

        CollectionAssert.AreEqual(
            new[]
            {
                "",
                "~ Base Parameters ~",
                "PCL version: 2.9.0 (123)",
                "Game version: 1.20.1 (1.20.1, Drop 5, not fully confirmed)",
                "Asset version: 1.20",
                "Inherited instance: None",
                "Allocated memory: 3.5 GB (3584 MB)",
                @"Minecraft folder: C:\.minecraft",
                @"Instance folder: C:\.minecraft\versions\fabric",
                "Version isolation: True",
                "HMCL format: False",
                "Java info: Java 17",
                @"Natives folder: C:\natives",
                "Native archives: 0",
                "",
                "~ Profile Parameters ~",
                "Player name: Player",
                "Access token: access",
                "Client token: client",
                "UUID: uuid",
                "Login type: Microsoft",
                ""
            },
            result.LogLines.ToArray());
    }

    [TestMethod]
    public void BuildStartupSummaryFallsBackWhenJavaOrInheritedInstanceIsMissing()
    {
        var result = MinecraftLaunchSessionLogService.BuildStartupSummary(new MinecraftLaunchSessionLogRequest(
            LauncherVersionName: "2.9.0",
            LauncherVersionCode: 123,
            GameVersionDisplayName: "1.7.10",
            GameVersionRaw: "1.7.10",
            GameVersionDrop: 1,
            IsGameVersionReliable: true,
            AssetsIndexName: "legacy",
            InheritedInstanceName: null,
            AllocatedMemoryInGigabytes: 2,
            MinecraftFolder: "/tmp/.minecraft",
            InstanceFolder: "/tmp/.minecraft/versions/legacy",
            IsVersionIsolated: false,
            IsHmclFormatJson: true,
            JavaDescription: null,
            NativesFolder: "/tmp/natives",
            PlayerName: "Offline",
            AccessToken: "token",
            ClientToken: "client",
            Uuid: "uuid",
            LoginType: "Legacy"));

        Assert.AreEqual("Inherited instance: None", result.LogLines[5]);
        Assert.AreEqual("Java info: No Java available", result.LogLines[11]);
        Assert.AreEqual("Game version: 1.7.10 (1.7.10, Drop 1)", result.LogLines[3]);
    }
}

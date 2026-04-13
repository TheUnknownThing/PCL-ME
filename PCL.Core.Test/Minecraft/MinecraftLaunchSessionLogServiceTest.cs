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
                "~ 基础参数 ~",
                "PCL 版本：2.9.0 (123)",
                "游戏版本：1.20.1（1.20.1，Drop 5，无法完全确定）",
                "资源版本：1.20",
                "实例继承：无",
                "分配的内存：3.5 GB（3584 MB）",
                @"MC 文件夹：C:\.minecraft",
                @"实例文件夹：C:\.minecraft\versions\fabric",
                "版本隔离：True",
                "HMCL 格式：False",
                "Java 信息：Java 17",
                @"Natives 文件夹：C:\natives",
                "",
                "~ 档案参数 ~",
                "玩家用户名：Player",
                "AccessToken：access",
                "ClientToken：client",
                "UUID：uuid",
                "验证方式：Microsoft",
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

        Assert.AreEqual("实例继承：无", result.LogLines[5]);
        Assert.AreEqual("Java 信息：无可用 Java", result.LogLines[11]);
        Assert.AreEqual("游戏版本：1.7.10（1.7.10，Drop 1）", result.LogLines[3]);
    }
}

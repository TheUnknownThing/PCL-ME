using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashReportBuilderTest
{
    [TestMethod]
    public void BuildEnvironmentReportFormatsLauncherAndEnvironmentDetails()
    {
        var request = new MinecraftCrashEnvironmentReportRequest(
            "2.14.5",
            "device-123",
            """
            header
            [Launch] ~ 基础参数 ~
            玩家用户名：Steve [test]
            验证方式：Microsoft [test]
            Java 信息：Zulu 21 [test]
            MC 文件夹：C:\Games\.minecraft [test]
            分配的内存：4096 MB [test]
            开始 Minecraft 日志监控
            footer
            """,
            """
            java -jar game.jar
            """,
            new SystemEnvironmentSnapshot(
                "Microsoft Windows 11 Pro",
                new Version(10, 0, 22635, 0),
                Architecture.Arm64,
                true,
                16UL * 1024 * 1024 * 1024,
                "AMD Ryzen",
                [
                    new SystemGpuInfo("GPU A", 4096, "31.0"),
                    new SystemGpuInfo("GPU B", 2048, "30.0"),
                ]));

        var report = MinecraftCrashReportBuilder.BuildEnvironmentReport(request);

        var expected = string.Join(
            "\r\n",
            [
                "PCL CE 版本：2.14.5 ",
                "识别码：device-123",
                "",
                "- 档案信息 -",
                "档案名称：Steve (验证方式：Microsoft)",
                "",
                "- 实例信息 -",
                "选定的 Java 虚拟机：Zulu 21",
                "Log4j2 NoLookups：True",
                @"MC 文件夹：C:\Games\.minecraft",
                "",
                "- 环境信息 -",
                "操作系统：Microsoft Windows 11 Pro 10.0.22635.0（64 位：True, ARM64: True）",
                "CPU：AMD Ryzen",
                "内存分配 (分配的内存 / 已安装物理内存)：4096 MB / 16 GB (16384 MB)",
                "显卡 0：GPU A (>= 4096 MB, 31.0)",
                "显卡 1：GPU B (2048 MB, 30.0)",
                "",
            ]);

        Assert.AreEqual(expected, report);
    }

    [TestMethod]
    public void BuildEnvironmentReportFallsBackWhenMarkersAreMissing()
    {
        var request = new MinecraftCrashEnvironmentReportRequest(
            "2.14.5",
            "device-123",
            "没有启动参数片段",
            "cmd -Dlog4j2.formatMsgNoLookups=false",
            new SystemEnvironmentSnapshot(
                "Linux",
                new Version(6, 8),
                Architecture.X64,
                true,
                0,
                string.Empty,
                []));

        var report = MinecraftCrashReportBuilder.BuildEnvironmentReport(request);

        StringAssert.Contains(report, "档案名称：没有启动参数片段 (验证方式：没有启动参数片段)");
        StringAssert.Contains(report, "Log4j2 NoLookups：False");
        StringAssert.Contains(report, "内存分配 (分配的内存 / 已安装物理内存)：没有启动参数片段 / 0 GB (0 MB)");
    }
}

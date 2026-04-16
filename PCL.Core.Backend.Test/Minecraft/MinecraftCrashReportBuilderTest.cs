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
            [Launch] ~ Base Parameters ~
            Player name: Steve [test]
            Login type: Microsoft [test]
            Java info: Zulu 21 [test]
            Minecraft folder: C:\Games\.minecraft [test]
            Allocated memory: 4096 MB [test]
            Start Minecraft log monitoring
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
                "PCL-ME version: 2.14.5 ",
                "Identifier: device-123",
                "",
                "- Profile -",
                "Profile name: Steve (auth method: Microsoft)",
                "",
                "- Instance -",
                "Selected Java runtime: Zulu 21",
                "Log4j2 NoLookups: True",
                @"MC folder: C:\Games\.minecraft",
                "",
                "- Environment -",
                "Operating system: Microsoft Windows 11 Pro 10.0.22635.0 (64-bit: True, ARM64: True)",
                "CPU: AMD Ryzen",
                "Memory allocation (allocated / installed physical memory): 4096 MB / 16 GB (16384 MB)",
                "GPU 0: GPU A (>= 4096 MB, 31.0)",
                "GPU 1: GPU B (2048 MB, 30.0)",
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
            "No launch parameter fragment",
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

        StringAssert.Contains(report, "Profile name: No launch parameter fragment (auth method: No launch parameter fragment)");
        StringAssert.Contains(report, "Log4j2 NoLookups: False");
        StringAssert.Contains(report, "Memory allocation (allocated / installed physical memory): No launch parameter fragment / 0 GB (0 MB)");
    }
}

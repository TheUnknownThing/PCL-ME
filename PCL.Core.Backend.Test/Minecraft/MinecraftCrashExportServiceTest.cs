using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashExportServiceTest
{
    [TestMethod]
    public void PrepareReportDirectoryRenamesSanitizesAndBuildsEnvironmentReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-export-" + Guid.NewGuid().ToString("N"));
        var reportDirectory = Path.Combine(root, "Report");
        Directory.CreateDirectory(root);

        try
        {
            var launcherLogPath = Path.Combine(root, "Log-CE1.log");
            var launchScriptPath = Path.Combine(root, "LatestLaunch.bat");
            var rawOutputPath = Path.Combine(root, "RawOutput.log");
            var userProfilePath = @"C:\Users\Alice";
            var accessToken = "abcde1234567890vwxyz";
            File.WriteAllText(launcherLogPath,
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
                Encoding.UTF8);
            File.WriteAllText(launchScriptPath, $"java -jar game.jar --token {accessToken} accessToken {accessToken}", Encoding.UTF8);
            File.WriteAllText(rawOutputPath, $"Path={userProfilePath}", Encoding.UTF8);

            var result = MinecraftCrashExportService.PrepareReportDirectory(new MinecraftCrashExportRequest(
                reportDirectory,
                "2.14.5",
                "device-123",
                [
                    new MinecraftCrashExportFile(launcherLogPath),
                    new MinecraftCrashExportFile(launchScriptPath),
                    new MinecraftCrashExportFile(rawOutputPath)
                ],
                launcherLogPath,
                new SystemEnvironmentSnapshot(
                    "Windows",
                    new Version(10, 0, 22635, 0),
                    Architecture.X64,
                    true,
                    16UL * 1024 * 1024 * 1024,
                    "AMD Ryzen",
                    []),
                accessToken,
                "uuid-123",
                userProfilePath));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    Path.Combine(reportDirectory, "PCL Launcher Log.txt"),
                    Path.Combine(reportDirectory, "Launch Script.bat"),
                    Path.Combine(reportDirectory, "Pre-Crash Output.txt"),
                    Path.Combine(reportDirectory, "Environment and Launch Info.txt")
                },
                result.WrittenFiles.ToArray());

            var launchScript = File.ReadAllText(Path.Combine(reportDirectory, "Launch Script.bat"), Encoding.UTF8);
            var rawOutput = File.ReadAllText(Path.Combine(reportDirectory, "Pre-Crash Output.txt"), Encoding.UTF8);
            var environmentReport = File.ReadAllText(Path.Combine(reportDirectory, "Environment and Launch Info.txt"), Encoding.UTF8);

            Assert.IsFalse(launchScript.Contains(accessToken, StringComparison.Ordinal));
            Assert.IsFalse(rawOutput.Contains(userProfilePath, StringComparison.Ordinal));
            StringAssert.Contains(environmentReport, "PCL-ME 版本：2.14.5");
            StringAssert.Contains(environmentReport, "识别码：device-123");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void PrepareReportDirectoryStillWritesEnvironmentReportWhenSourceFilesAreMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-export-" + Guid.NewGuid().ToString("N"));
        var reportDirectory = Path.Combine(root, "Report");
        Directory.CreateDirectory(root);

        try
        {
            var result = MinecraftCrashExportService.PrepareReportDirectory(new MinecraftCrashExportRequest(
                reportDirectory,
                "2.14.5",
                "device-456",
                [new MinecraftCrashExportFile(Path.Combine(root, "missing.log"))],
                CurrentLauncherLogFilePath: null,
                new SystemEnvironmentSnapshot(
                    "Windows",
                    new Version(10, 0, 19045, 0),
                    Architecture.X64,
                    true,
                    8UL * 1024 * 1024 * 1024,
                    "CPU",
                    []),
                CurrentAccessToken: null,
                CurrentUserUuid: null,
                UserProfilePath: null));

            Assert.AreEqual(1, result.WrittenFiles.Count);
            Assert.AreEqual(Path.Combine(reportDirectory, "Environment and Launch Info.txt"), result.WrittenFiles.Single());
            StringAssert.Contains(File.ReadAllText(result.WrittenFiles.Single(), Encoding.UTF8), "识别码：device-456");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}

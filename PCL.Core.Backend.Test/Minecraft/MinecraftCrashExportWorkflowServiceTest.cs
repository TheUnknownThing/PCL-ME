using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashExportWorkflowServiceTest
{
    [TestMethod]
    public void CreatePlanBuildsSuggestedArchiveNameAndDistinctSourceFiles()
    {
        var timestamp = new DateTime(2026, 4, 2, 13, 5, 6);
        var result = MinecraftCrashExportWorkflowService.CreatePlan(new MinecraftCrashExportPlanRequest(
            timestamp,
            ReportDirectory: "/tmp/report",
            LauncherVersionName: "2.14.5",
            UniqueAddress: "device-123",
            SourceFilePaths:
            [
                "/tmp/a.log",
                "/tmp/b.log",
                "/tmp/a.log",
                ""
            ],
            AdditionalSourceFilePaths:
            [
                "/tmp/c.log",
                "/tmp/B.log"
            ],
            CurrentLauncherLogFilePath: "/tmp/a.log",
            Environment: new SystemEnvironmentSnapshot(
                "Windows",
                new Version(10, 0, 22635, 0),
                Architecture.X64,
                true,
                16UL * 1024 * 1024 * 1024,
                "AMD Ryzen",
                []),
            CurrentAccessToken: "token",
            CurrentUserUuid: "uuid",
            UserProfilePath: @"C:\Users\Alice",
            Culture: new CultureInfo("zh-CN")));

        Assert.AreEqual("错误报告-2026-4-2_13.05.06.zip", result.SuggestedArchiveName);
        Assert.AreEqual("/tmp/report", result.ExportRequest.ReportDirectory);
        Assert.AreEqual("/tmp/a.log", result.ExportRequest.CurrentLauncherLogFilePath);
        CollectionAssert.AreEqual(
            new[]
            {
                "/tmp/a.log",
                "/tmp/b.log",
                "/tmp/c.log"
            },
            result.ExportRequest.SourceFiles.Select(file => file.SourcePath).ToArray());
    }

    [TestMethod]
    public void CreatePlanAllowsMissingAdditionalFiles()
    {
        var result = MinecraftCrashExportWorkflowService.CreatePlan(new MinecraftCrashExportPlanRequest(
            Timestamp: new DateTime(2026, 4, 2, 13, 5, 6),
            ReportDirectory: "/tmp/report",
            LauncherVersionName: "2.14.5",
            UniqueAddress: "device-123",
            SourceFilePaths:
            [
                "/tmp/a.log"
            ],
            AdditionalSourceFilePaths: null,
            CurrentLauncherLogFilePath: null,
            Environment: new SystemEnvironmentSnapshot(
                "Windows",
                new Version(10, 0, 22635, 0),
                Architecture.X64,
                true,
                16UL * 1024 * 1024 * 1024,
                "AMD Ryzen",
                []),
            CurrentAccessToken: null,
            CurrentUserUuid: null,
            UserProfilePath: null));

        Assert.AreEqual(1, result.ExportRequest.SourceFiles.Count);
        Assert.AreEqual("/tmp/a.log", result.ExportRequest.SourceFiles.Single().SourcePath);
    }

    [TestMethod]
    public void CreatePlanAddsLauncherLogWhenItIsOnlyProvidedAsCurrentLogPath()
    {
        var result = MinecraftCrashExportWorkflowService.CreatePlan(new MinecraftCrashExportPlanRequest(
            Timestamp: new DateTime(2026, 4, 2, 13, 5, 6),
            ReportDirectory: "/tmp/report",
            LauncherVersionName: "2.14.5",
            UniqueAddress: "device-123",
            SourceFilePaths:
            [
                "/tmp/a.log"
            ],
            AdditionalSourceFilePaths: null,
            CurrentLauncherLogFilePath: "/tmp/launcher.log",
            Environment: new SystemEnvironmentSnapshot(
                "Windows",
                new Version(10, 0, 22635, 0),
                Architecture.X64,
                true,
                16UL * 1024 * 1024 * 1024,
                "AMD Ryzen",
                []),
            CurrentAccessToken: null,
            CurrentUserUuid: null,
            UserProfilePath: null));

        CollectionAssert.AreEqual(
            new[]
            {
                "/tmp/a.log",
                "/tmp/launcher.log"
            },
            result.ExportRequest.SourceFiles.Select(file => file.SourcePath).ToArray());
    }
}

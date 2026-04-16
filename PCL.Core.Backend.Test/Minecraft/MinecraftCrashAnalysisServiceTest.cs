using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashAnalysisServiceTest
{
    [TestMethod]
    public void AnalyzeReturnsHelpfulMessageWhenNoLogsExist()
    {
        var result = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
            [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.log")],
            CurrentLauncherLogFilePath: null));

        Assert.IsFalse(result.HasKnownReason);
        Assert.IsFalse(result.HasDirectFile);
        StringAssert.Contains(result.ResultText, "could not find any relevant log files");
    }

    [TestMethod]
    public void AnalyzeDetectsConfirmedModCrashFromLatestLog()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-analysis-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var latestLogPath = Path.Combine(root, "latest.log");
            File.WriteAllText(
                latestLogPath,
                """
                [12:00:00] [main/FATAL]: Caught exception from jei
                java.lang.RuntimeException: boom
                """,
                new UTF8Encoding(false));

            var result = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
                [latestLogPath],
                CurrentLauncherLogFilePath: null));

            Assert.IsTrue(result.HasKnownReason);
            Assert.IsTrue(result.HasDirectFile);
        StringAssert.Contains(result.ResultText, "The mod named jei caused the game to fail.");
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
    public void AnalyzeDetectsModLoaderIncompatibilityFromLoaderMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-analysis-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var latestLogPath = Path.Combine(root, "latest.log");
            File.WriteAllText(
                latestLogPath,
                """
                Incompatible mods found!
                    Mod ID: 'forge', Requested by 'demomod'
                """,
                new UTF8Encoding(false));

            var result = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
                [latestLogPath],
                CurrentLauncherLogFilePath: null));

            Assert.IsTrue(result.HasKnownReason);
            Assert.IsTrue(result.HasModLoaderVersionMismatch);
            StringAssert.StartsWith(result.ResultText, "The mod loader version is incompatible with the mod.");
            StringAssert.Contains(result.ResultText, "Mod ID: 'forge', Requested by 'demomod'");
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

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Testing;

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
    public void AnalyzeLocalizesGenericFailureMessageWhenNoReasonIsFound()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-analysis-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var latestLogPath = Path.Combine(root, "latest.log");
            File.WriteAllText(
                latestLogPath,
                """
                [12:00:00] [main/INFO]: launcher booted
                [12:00:01] [main/INFO]: no recognized crash markers here
                """,
                new UTF8Encoding(false));

            var i18n = new DictionaryI18nService(new Dictionary<string, string>
            {
                ["crash.analysis.generic_failure.message"] = "抱歉，你的游戏遇到了一些问题……",
                ["crash.analysis.help_suffix"] = "如果你需要帮助，请把崩溃报告文件发给对方，而不是发送此窗口的照片或截图。"
            }, "zh-Hans");

            var result = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
                [latestLogPath],
                CurrentLauncherLogFilePath: null), i18n.T);

            Assert.IsFalse(result.HasKnownReason);
            Assert.IsTrue(result.HasDirectFile);
            Assert.AreEqual(
                "抱歉，你的游戏遇到了一些问题……\r\n如果你需要帮助，请把崩溃报告文件发给对方，而不是发送此窗口的照片或截图。",
                result.ResultText);
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

    [TestMethod]
    public void AnalyzeLocalizesManualDebugCrashMessageWhenI18nIsProvided()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-analysis-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var crashReportPath = Path.Combine(root, "crash-2026-04-17_12.00.00-client.txt");
            File.WriteAllText(
                crashReportPath,
                """
                ---- Minecraft Crash Report ----
                java.lang.Throwable: Manually triggered debug crash
                """,
                new UTF8Encoding(false));

            var i18n = new DictionaryI18nService(new Dictionary<string, string>
            {
                ["crash.analysis.manual_debug.message"] = "* 其实，你的游戏并没有问题。这次崩溃是故意触发的。\n* 你难道没有更重要的事要做吗？"
            }, "zh-Hans");

            var result = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
                [crashReportPath],
                CurrentLauncherLogFilePath: null), i18n.T);

            Assert.IsTrue(result.HasKnownReason);
            StringAssert.StartsWith(result.ResultText, "* 其实，你的游戏并没有问题。这次崩溃是故意触发的。\r\n* 你难道没有更重要的事要做吗？");
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

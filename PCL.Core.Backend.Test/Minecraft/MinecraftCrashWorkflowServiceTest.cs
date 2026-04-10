using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashWorkflowServiceTest
{
    [TestMethod]
    public void BuildOutputPromptReturnsConfirmOnlyForManualAnalysis()
    {
        var result = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            "manual result",
            IsManualAnalysis: true,
            HasDirectFile: true,
            CanOpenModLoaderSettings: true));

        Assert.AreEqual("错误报告分析结果", result.Title);
        Assert.AreEqual(1, result.Buttons.Count);
        Assert.AreEqual("确定", result.Buttons[0].Label);
        Assert.AreEqual(MinecraftCrashOutputPromptActionKind.Close, result.Buttons[0].Action);
    }

    [TestMethod]
    public void BuildOutputPromptReturnsViewLogButtonWhenDirectFileIsAvailable()
    {
        var result = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            "normal crash result",
            IsManualAnalysis: false,
            HasDirectFile: true,
            CanOpenModLoaderSettings: false));

        Assert.AreEqual("Minecraft 出现错误", result.Title);
        Assert.AreEqual(3, result.Buttons.Count);
        Assert.AreEqual("查看日志", result.Buttons[1].Label);
        Assert.AreEqual(MinecraftCrashOutputPromptActionKind.ViewLog, result.Buttons[1].Action);
        Assert.IsFalse(result.Buttons[1].ClosesPrompt);
        Assert.AreEqual("导出错误报告", result.Buttons[2].Label);
        Assert.AreEqual(MinecraftCrashOutputPromptActionKind.ExportReport, result.Buttons[2].Action);
    }

    [TestMethod]
    public void BuildOutputPromptPrefersModLoaderSettingsActionWhenAvailable()
    {
        var result = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            "Mod 加载器版本与 Mod 不兼容，请前往修改",
            IsManualAnalysis: false,
            HasDirectFile: true,
            CanOpenModLoaderSettings: true));

        Assert.AreEqual(3, result.Buttons.Count);
        Assert.AreEqual("前往修改", result.Buttons[1].Label);
        Assert.AreEqual(MinecraftCrashOutputPromptActionKind.OpenInstanceSettings, result.Buttons[1].Action);
        Assert.IsTrue(result.Buttons[1].ClosesPrompt);
    }

    [TestMethod]
    public void GetSuggestedExportArchiveNameMatchesLegacyFormatting()
    {
        var result = MinecraftCrashWorkflowService.GetSuggestedExportArchiveName(
            new DateTime(2026, 4, 2, 13, 5, 6),
            new CultureInfo("zh-CN"));

        Assert.AreEqual("错误报告-2026-4-2_13.05.06.zip", result);
    }
}

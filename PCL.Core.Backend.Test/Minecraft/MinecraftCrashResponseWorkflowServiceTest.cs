using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashResponseWorkflowServiceTest
{
    [TestMethod]
    public void ResolvePromptResponseMapsExportAction()
    {
        var result = MinecraftCrashResponseWorkflowService.ResolvePromptResponse(
            MinecraftCrashOutputPromptActionKind.ExportReport);

        Assert.AreEqual(MinecraftCrashPromptResponseKind.ExportReport, result.Kind);
    }

    [TestMethod]
    public void ResolvePromptResponseMapsInstanceSettingsAction()
    {
        var result = MinecraftCrashResponseWorkflowService.ResolvePromptResponse(
            MinecraftCrashOutputPromptActionKind.OpenInstanceSettings);

        Assert.AreEqual(MinecraftCrashPromptResponseKind.OpenInstanceSettings, result.Kind);
    }

    [TestMethod]
    public void BuildExportCompletionPlanReturnsHintAndRevealPath()
    {
        var result = MinecraftCrashResponseWorkflowService.BuildExportCompletionPlan(
            "/tmp/crash-report.zip");

        Assert.AreEqual("错误报告已导出！", result.HintMessage);
        Assert.AreEqual("/tmp/crash-report.zip", result.RevealInShellPath);
    }

    [TestMethod]
    public void BuildExportSaveDialogPlanReturnsLauncherDialogContract()
    {
        var result = MinecraftCrashResponseWorkflowService.BuildExportSaveDialogPlan(
            "错误报告-2026-4-3_10.00.00.zip");

        Assert.AreEqual("选择保存位置", result.Title);
        Assert.AreEqual("错误报告-2026-4-3_10.00.00.zip", result.DefaultFileName);
        Assert.AreEqual("Minecraft 错误报告(*.zip)|*.zip", result.Filter);
    }
}

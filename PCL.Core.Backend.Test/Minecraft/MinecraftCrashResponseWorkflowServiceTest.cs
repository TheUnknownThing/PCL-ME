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

        Assert.AreEqual("Crash report exported.", result.HintMessage);
        Assert.AreEqual("/tmp/crash-report.zip", result.RevealInShellPath);
    }

    [TestMethod]
    public void BuildExportSaveDialogPlanReturnsLauncherDialogContract()
    {
        var result = MinecraftCrashResponseWorkflowService.BuildExportSaveDialogPlan(
            "crash-report-2026-4-3_10.00.00.zip");

        Assert.AreEqual("Choose save location", result.Title);
        Assert.AreEqual("crash-report-2026-4-3_10.00.00.zip", result.DefaultFileName);
        Assert.AreEqual("Minecraft Crash Report (*.zip)|*.zip", result.Filter);
    }
}

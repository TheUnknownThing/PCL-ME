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
}

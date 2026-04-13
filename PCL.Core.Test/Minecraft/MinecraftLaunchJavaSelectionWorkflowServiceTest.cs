using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchJavaSelectionWorkflowServiceTest
{
    [TestMethod]
    public void ResolveInitialSelectionUsesSelectedJavaLogWhenDisplayNameIsPresent()
    {
        var plan = CreateWorkflowPlan();

        var result = MinecraftLaunchJavaSelectionWorkflowService.ResolveInitialSelection(
            plan,
            "Java 21 (C:\\Java\\bin\\javaw.exe)");

        Assert.AreEqual(MinecraftLaunchJavaSelectionActionKind.UseSelectedJava, result.ActionKind);
        Assert.AreEqual("选择的 Java：Java 21 (C:\\Java\\bin\\javaw.exe)", result.LogMessage);
        Assert.IsNull(result.Prompt);
    }

    [TestMethod]
    public void ResolveInitialSelectionReturnsPromptWhenJavaIsMissing()
    {
        var plan = CreateWorkflowPlan();

        var result = MinecraftLaunchJavaSelectionWorkflowService.ResolveInitialSelection(plan, selectedJavaDisplayName: null);

        Assert.AreEqual(MinecraftLaunchJavaSelectionActionKind.PromptForDownload, result.ActionKind);
        Assert.AreEqual(plan.MissingJavaLogMessage, result.LogMessage);
        Assert.AreEqual(plan.MissingJavaPrompt, result.Prompt);
    }

    [TestMethod]
    public void ResolvePostDownloadSelectionUsesSelectedJavaLogWhenDisplayNameIsPresent()
    {
        var plan = CreateWorkflowPlan();

        var result = MinecraftLaunchJavaSelectionWorkflowService.ResolvePostDownloadSelection(
            plan,
            "Java 8u412 (C:\\Minecraft\\.minecraft\\runtime\\jre-8u412)");

        Assert.AreEqual(MinecraftLaunchJavaPostDownloadActionKind.UseSelectedJava, result.ActionKind);
        Assert.AreEqual("选择的 Java：Java 8u412 (C:\\Minecraft\\.minecraft\\runtime\\jre-8u412)", result.LogMessage);
        Assert.IsNull(result.HintMessage);
    }

    [TestMethod]
    public void ResolvePostDownloadSelectionReturnsAbortHintWhenJavaIsStillMissing()
    {
        var plan = CreateWorkflowPlan();

        var result = MinecraftLaunchJavaSelectionWorkflowService.ResolvePostDownloadSelection(plan, selectedJavaDisplayName: null);

        Assert.AreEqual(MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch, result.ActionKind);
        Assert.IsNull(result.LogMessage);
        Assert.AreEqual(plan.NoJavaAvailableHintMessage, result.HintMessage);
    }

    private static MinecraftLaunchJavaWorkflowPlan CreateWorkflowPlan()
    {
        return MinecraftLaunchJavaWorkflowService.BuildPlan(new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: true,
            ReleaseTime: new DateTime(2024, 4, 23),
            VanillaVersion: new Version(20, 0, 5),
            HasOptiFine: false,
            HasForge: false,
            ForgeVersion: null,
            HasCleanroom: false,
            HasFabric: true,
            HasLiteLoader: false,
            HasLabyMod: false,
            JsonRequiredMajorVersion: 21,
            MojangRecommendedMajorVersion: 21,
            MojangRecommendedComponent: "jre-legacy"));
    }
}

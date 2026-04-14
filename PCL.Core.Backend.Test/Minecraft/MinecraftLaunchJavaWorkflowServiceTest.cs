using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchJavaWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanIncludesRequirementLogAndPrompt()
    {
        var result = MinecraftLaunchJavaWorkflowService.BuildPlan(CreateWorkflowRequest() with
        {
            IsVersionInfoValid = true,
            VanillaVersion = new Version(20, 0, 5),
            MojangRecommendedMajorVersion = 22,
            MojangRecommendedComponent = "jre-legacy"
        });

        Assert.AreEqual(new Version(22, 0, 0, 0), result.MinimumVersion);
        Assert.AreEqual("Mojang 要求至少使用 Java 22", result.RecommendedVersionLogMessage);
        Assert.AreEqual("Java 版本需求：最低 22.0.0.0，最高 999.999.999.999", result.RequirementLogMessage);
        Assert.AreEqual("无合适的 Java，需要确认是否自动下载", result.MissingJavaLogMessage);
        Assert.AreEqual("jre-legacy", result.MissingJavaPrompt.DownloadTarget);
    }

    [TestMethod]
    public void ResolveInitialSelectionPromptsWhenJavaIsMissing()
    {
        var plan = MinecraftLaunchJavaWorkflowService.BuildPlan(CreateWorkflowRequest());

        var result = MinecraftLaunchJavaWorkflowService.ResolveInitialSelection(plan, hasSelectedJava: false);

        Assert.AreEqual(MinecraftLaunchJavaSelectionActionKind.PromptForDownload, result.ActionKind);
        Assert.AreEqual(plan.MissingJavaLogMessage, result.LogMessage);
        Assert.AreEqual(plan.MissingJavaPrompt, result.Prompt);
    }

    [TestMethod]
    public void ResolvePromptDecisionRequestsDownloadWhenUserAccepts()
    {
        var prompt = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(17, 0, 0, 0),
                new Version(999, 999, 999, 999),
                HasForge: false,
                RecommendedComponent: "17"));

        var result = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(prompt, MinecraftLaunchJavaPromptDecision.Download);

        Assert.AreEqual(MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection, result.ActionKind);
        Assert.AreEqual("17", result.DownloadTarget);
    }

    [TestMethod]
    public void ResolvePostDownloadSelectionReturnsAbortHintWhenJavaIsStillMissing()
    {
        var plan = MinecraftLaunchJavaWorkflowService.BuildPlan(CreateWorkflowRequest());

        var result = MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(plan, hasSelectedJava: false);

        Assert.AreEqual(MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch, result.ActionKind);
        Assert.AreEqual("没有可用的 Java，已取消启动！", result.HintMessage);
    }

    [TestMethod]
    public void EvaluateRequiresJava21ForModernMinecraft()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            IsVersionInfoValid = true,
            VanillaVersion = new Version(20, 0, 5)
        });

        Assert.AreEqual(new Version(21, 0, 0, 0), result.MinimumVersion);
    }

    [TestMethod]
    public void EvaluateCapsForge116WindowToJava8u321()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            IsVersionInfoValid = true,
            VanillaVersion = new Version(16, 0, 5),
            HasForge = true,
            ForgeVersion = "36.2.25"
        });

        Assert.AreEqual(new Version(1, 8, 0, 321), result.MaximumVersion);
    }

    [TestMethod]
    public void EvaluateCapsLegacyVanillaToJava8()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            IsVersionInfoValid = true,
            ReleaseTime = new DateTime(2013, 9, 19),
            VanillaVersion = new Version(6, 0, 4)
        });

        Assert.AreEqual(new Version(1, 8, 999, 999), result.MaximumVersion);
    }

    [TestMethod]
    public void EvaluateRequiresJava17ForModernFabric()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            IsVersionInfoValid = true,
            VanillaVersion = new Version(18, 2, 0),
            HasFabric = true
        });

        Assert.AreEqual(new Version(17, 0, 0, 0), result.MinimumVersion);
    }

    [TestMethod]
    public void EvaluateCapsOptiFineOnMinecraft18ToJava18()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            IsVersionInfoValid = true,
            VanillaVersion = new Version(18, 1, 0),
            HasForge = true,
            ForgeVersion = "40.0.0",
            HasOptiFine = true
        });

        Assert.AreEqual(new Version(1, 18, 999, 999), result.MaximumVersion);
    }

    [TestMethod]
    public void EvaluateRequiresJava21ForCleanroom()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            HasCleanroom = true
        });

        Assert.AreEqual(new Version(21, 0, 0, 0), result.MinimumVersion);
    }

    [TestMethod]
    public void EvaluateRequiresJava21AndRemovesUpperBoundForLabyMod()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            HasLabyMod = true,
            HasOptiFine = true,
            IsVersionInfoValid = true,
            VanillaVersion = new Version(12, 0, 0)
        });

        Assert.AreEqual(new Version(21, 0, 0, 0), result.MinimumVersion);
        Assert.AreEqual(new Version(999, 999, 999, 999), result.MaximumVersion);
    }

    [TestMethod]
    public void EvaluateAppliesMojangComponentRecommendation()
    {
        var result = MinecraftLaunchJavaRequirementService.Evaluate(CreateRequirementRequest() with
        {
            MojangRecommendedMajorVersion = 22,
            MojangRecommendedComponent = "jre-legacy"
        });

        Assert.AreEqual(new Version(22, 0, 0, 0), result.MinimumVersion);
        Assert.AreEqual("jre-legacy", result.RecommendedComponent);
    }

    [TestMethod]
    public void BuildMissingJavaPromptReturnsManualLegacyJava7MessageForForge()
    {
        var result = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(1, 7, 0, 0),
                new Version(1, 7, 999, 999),
                HasForge: true,
                RecommendedComponent: null));

        Assert.AreEqual("launch.prompts.java_missing.title", result.Title.Key);
        Assert.AreEqual(MinecraftLaunchJavaPromptDecision.Abort, result.Options.Single().Decision);
        Assert.AreEqual("launch.prompts.java_missing.manual_java7_with_legacy_fixer.message", result.Message.Key);
    }

    [TestMethod]
    public void BuildMissingJavaPromptReturnsManualBoundedJava8Message()
    {
        var result = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(1, 8, 0, 141),
                new Version(1, 8, 0, 320),
                HasForge: false,
                RecommendedComponent: null));

        Assert.AreEqual("launch.prompts.java_missing.title", result.Title.Key);
        Assert.AreEqual("launch.prompts.java_missing.manual_java8u141_to_320.message", result.Message.Key);
        Assert.AreEqual(MinecraftLaunchJavaPromptDecision.Abort, result.Options.Single().Decision);
    }

    [TestMethod]
    public void BuildMissingJavaPromptReturnsAutomaticDownloadChoicesForJava8()
    {
        var result = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(1, 8, 0, 0),
                new Version(999, 999, 999, 999),
                HasForge: false,
                RecommendedComponent: null));

        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchJavaPromptDecision.Download,
                MinecraftLaunchJavaPromptDecision.Abort
            },
            result.Options.Select(option => option.Decision).ToArray());
        Assert.AreEqual("8", result.DownloadTarget);
    }

    [TestMethod]
    public void BuildMissingJavaPromptUsesRecommendedComponentForModernDownload()
    {
        var result = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(22, 0, 0, 0),
                new Version(999, 999, 999, 999),
                HasForge: false,
                RecommendedComponent: "jre-legacy"));

        Assert.AreEqual("jre-legacy", result.DownloadTarget);
        Assert.AreEqual(MinecraftLaunchJavaPromptDecision.Download, result.Options[0].Decision);
        Assert.AreEqual(MinecraftLaunchJavaPromptDecision.Abort, result.Options[1].Decision);
    }

    [TestMethod]
    public void BuildMissingJavaPromptDisplaysModernJavaMajorVersion()
    {
        var result = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                new Version(22, 0, 0, 0),
                new Version(999, 999, 999, 999),
                HasForge: false,
                RecommendedComponent: null));

        Assert.AreEqual("launch.prompts.java_missing.auto_download.message", result.Message.Key);
        Assert.AreEqual("Java 22", result.Message.Arguments?.Single().StringValue);
        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchJavaPromptDecision.Download,
                MinecraftLaunchJavaPromptDecision.Abort
            },
            result.Options.Select(option => option.Decision).ToArray());
    }

    private static MinecraftLaunchJavaRequirementRequest CreateRequirementRequest()
    {
        return new MinecraftLaunchJavaRequirementRequest(
            IsVersionInfoValid: false,
            ReleaseTime: new DateTime(2018, 1, 1),
            VanillaVersion: new Version(1, 12, 2),
            HasOptiFine: false,
            HasForge: false,
            ForgeVersion: null,
            HasCleanroom: false,
            HasFabric: false,
            HasLiteLoader: false,
            HasLabyMod: false,
            JsonRequiredMajorVersion: null,
            MojangRecommendedMajorVersion: 0,
            MojangRecommendedComponent: null);
    }

    private static MinecraftLaunchJavaWorkflowRequest CreateWorkflowRequest()
    {
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: false,
            ReleaseTime: new DateTime(2018, 1, 1),
            VanillaVersion: new Version(1, 12, 2),
            HasOptiFine: false,
            HasForge: false,
            ForgeVersion: null,
            HasCleanroom: false,
            HasFabric: false,
            HasLiteLoader: false,
            HasLabyMod: false,
            JsonRequiredMajorVersion: null,
            MojangRecommendedMajorVersion: 0,
            MojangRecommendedComponent: null);
    }
}

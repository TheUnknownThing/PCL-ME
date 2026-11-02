using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaWorkflowService
{
    private const string MissingJavaLogMessage = "No suitable Java runtime was found; confirm whether it should be downloaded automatically.";
    private const string MissingJavaHintMessage = "No Java runtime is available. Launch has been canceled.";

    public static MinecraftLaunchJavaWorkflowPlan BuildPlan(MinecraftLaunchJavaWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requirement = MinecraftLaunchJavaRequirementService.Evaluate(
            new MinecraftLaunchJavaRequirementRequest(
                request.IsVersionInfoValid,
                request.ReleaseTime,
                request.VanillaVersion,
                request.HasOptiFine,
                request.HasForge,
                request.ForgeVersion,
                request.HasCleanroom,
                request.HasFabric,
                request.HasLiteLoader,
                request.HasLabyMod,
                request.JsonRequiredMajorVersion,
                request.MojangRecommendedMajorVersion,
                request.MojangRecommendedComponent));

        var prompt = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
            new MinecraftLaunchJavaPromptRequest(
                requirement.MinimumVersion,
                requirement.MaximumVersion,
                request.HasForge,
                requirement.RecommendedComponent));

        return new MinecraftLaunchJavaWorkflowPlan(
            requirement.MinimumVersion,
            requirement.MaximumVersion,
            requirement.RecommendedMajorVersion,
            requirement.RecommendedComponent,
            requirement.RecommendedMajorVersion >= 22
                ? $"Mojang requires at least Java {requirement.RecommendedMajorVersion}"
                : null,
            $"Java version requirement: minimum {requirement.MinimumVersion}, maximum {requirement.MaximumVersion}",
            MissingJavaLogMessage,
            MissingJavaHintMessage,
            prompt);
    }

    public static MinecraftLaunchJavaSelectionOutcome ResolveInitialSelection(
        MinecraftLaunchJavaWorkflowPlan plan,
        bool hasSelectedJava)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return hasSelectedJava
            ? new MinecraftLaunchJavaSelectionOutcome(
                MinecraftLaunchJavaSelectionActionKind.UseSelectedJava,
                LogMessage: null,
                Prompt: null)
            : new MinecraftLaunchJavaSelectionOutcome(
                MinecraftLaunchJavaSelectionActionKind.PromptForDownload,
                plan.MissingJavaLogMessage,
                plan.MissingJavaPrompt);
    }

    public static MinecraftLaunchJavaPromptOutcome ResolvePromptDecision(
        MinecraftLaunchJavaPrompt prompt,
        MinecraftLaunchJavaPromptDecision decision)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        return decision == MinecraftLaunchJavaPromptDecision.Download
            ? new MinecraftLaunchJavaPromptOutcome(
                MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection,
                prompt.DownloadTarget)
            : new MinecraftLaunchJavaPromptOutcome(
                MinecraftLaunchJavaPromptActionKind.AbortLaunch,
                DownloadTarget: null);
    }

    public static MinecraftLaunchJavaPostDownloadOutcome ResolvePostDownloadSelection(
        MinecraftLaunchJavaWorkflowPlan plan,
        bool hasSelectedJava)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return hasSelectedJava
            ? new MinecraftLaunchJavaPostDownloadOutcome(
                MinecraftLaunchJavaPostDownloadActionKind.UseSelectedJava,
                HintMessage: null)
            : new MinecraftLaunchJavaPostDownloadOutcome(
                MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch,
                plan.NoJavaAvailableHintMessage);
    }
}

public sealed record MinecraftLaunchJavaWorkflowRequest(
    bool IsVersionInfoValid,
    DateTime ReleaseTime,
    Version? VanillaVersion,
    bool HasOptiFine,
    bool HasForge,
    string? ForgeVersion,
    bool HasCleanroom,
    bool HasFabric,
    bool HasLiteLoader,
    bool HasLabyMod,
    int? JsonRequiredMajorVersion,
    int MojangRecommendedMajorVersion,
    string? MojangRecommendedComponent);

public sealed record MinecraftLaunchJavaWorkflowPlan(
    Version MinimumVersion,
    Version MaximumVersion,
    int RecommendedMajorVersion,
    string? RecommendedComponent,
    string? RecommendedVersionLogMessage,
    string RequirementLogMessage,
    string MissingJavaLogMessage,
    string NoJavaAvailableHintMessage,
    MinecraftLaunchJavaPrompt MissingJavaPrompt);

public sealed record MinecraftLaunchJavaSelectionOutcome(
    MinecraftLaunchJavaSelectionActionKind ActionKind,
    string? LogMessage,
    MinecraftLaunchJavaPrompt? Prompt);

public enum MinecraftLaunchJavaSelectionActionKind
{
    UseSelectedJava = 0,
    PromptForDownload = 1
}

public sealed record MinecraftLaunchJavaPromptOutcome(
    MinecraftLaunchJavaPromptActionKind ActionKind,
    string? DownloadTarget);

public enum MinecraftLaunchJavaPromptActionKind
{
    AbortLaunch = 0,
    DownloadAndRetrySelection = 1
}

public sealed record MinecraftLaunchJavaPostDownloadOutcome(
    MinecraftLaunchJavaPostDownloadActionKind ActionKind,
    string? HintMessage);

public enum MinecraftLaunchJavaPostDownloadActionKind
{
    UseSelectedJava = 0,
    AbortLaunch = 1
}

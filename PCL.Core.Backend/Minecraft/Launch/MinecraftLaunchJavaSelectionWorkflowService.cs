namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaSelectionWorkflowService
{
    private const string SelectedJavaLogPrefix = "选择的 Java：";

    public static MinecraftLaunchResolvedJavaSelection ResolveInitialSelection(
        MinecraftLaunchJavaWorkflowPlan plan,
        string? selectedJavaDisplayName)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!string.IsNullOrWhiteSpace(selectedJavaDisplayName))
        {
            return new MinecraftLaunchResolvedJavaSelection(
                MinecraftLaunchJavaSelectionActionKind.UseSelectedJava,
                $"{SelectedJavaLogPrefix}{selectedJavaDisplayName}",
                Prompt: null);
        }

        return new MinecraftLaunchResolvedJavaSelection(
            MinecraftLaunchJavaSelectionActionKind.PromptForDownload,
            plan.MissingJavaLogMessage,
            plan.MissingJavaPrompt);
    }

    public static MinecraftLaunchResolvedJavaPostDownloadSelection ResolvePostDownloadSelection(
        MinecraftLaunchJavaWorkflowPlan plan,
        string? selectedJavaDisplayName)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!string.IsNullOrWhiteSpace(selectedJavaDisplayName))
        {
            return new MinecraftLaunchResolvedJavaPostDownloadSelection(
                MinecraftLaunchJavaPostDownloadActionKind.UseSelectedJava,
                $"{SelectedJavaLogPrefix}{selectedJavaDisplayName}",
                HintMessage: null);
        }

        return new MinecraftLaunchResolvedJavaPostDownloadSelection(
            MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch,
            LogMessage: null,
            plan.NoJavaAvailableHintMessage);
    }
}

public sealed record MinecraftLaunchResolvedJavaSelection(
    MinecraftLaunchJavaSelectionActionKind ActionKind,
    string? LogMessage,
    MinecraftLaunchJavaPrompt? Prompt);

public sealed record MinecraftLaunchResolvedJavaPostDownloadSelection(
    MinecraftLaunchJavaPostDownloadActionKind ActionKind,
    string? LogMessage,
    string? HintMessage);

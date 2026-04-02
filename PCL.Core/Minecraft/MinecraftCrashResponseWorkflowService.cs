namespace PCL.Core.Minecraft;

public static class MinecraftCrashResponseWorkflowService
{
    public static MinecraftCrashPromptResponse ResolvePromptResponse(MinecraftCrashOutputPromptActionKind action)
    {
        return action switch
        {
            MinecraftCrashOutputPromptActionKind.OpenInstanceSettings => new MinecraftCrashPromptResponse(
                MinecraftCrashPromptResponseKind.OpenInstanceSettings),
            MinecraftCrashOutputPromptActionKind.ExportReport => new MinecraftCrashPromptResponse(
                MinecraftCrashPromptResponseKind.ExportReport),
            _ => MinecraftCrashPromptResponse.None
        };
    }

    public static MinecraftCrashExportCompletionPlan BuildExportCompletionPlan(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        return new MinecraftCrashExportCompletionPlan(
            HintMessage: "错误报告已导出！",
            RevealInShellPath: archivePath);
    }
}

public sealed record MinecraftCrashPromptResponse(
    MinecraftCrashPromptResponseKind Kind)
{
    public static MinecraftCrashPromptResponse None { get; } = new(MinecraftCrashPromptResponseKind.None);
}

public enum MinecraftCrashPromptResponseKind
{
    None = 0,
    OpenInstanceSettings = 1,
    ExportReport = 2
}

public sealed record MinecraftCrashExportCompletionPlan(
    string HintMessage,
    string RevealInShellPath);

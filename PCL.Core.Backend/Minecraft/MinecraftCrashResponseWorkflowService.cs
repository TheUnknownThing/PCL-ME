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
            HintMessage: "Crash report exported.",
            RevealInShellPath: archivePath);
    }

    public static MinecraftCrashExportSaveDialogPlan BuildExportSaveDialogPlan(string suggestedArchiveName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedArchiveName);

        return new MinecraftCrashExportSaveDialogPlan(
            Title: "Choose save location",
            DefaultFileName: suggestedArchiveName,
            Filter: "Minecraft Crash Report (*.zip)|*.zip");
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

public sealed record MinecraftCrashExportSaveDialogPlan(
    string Title,
    string DefaultFileName,
    string Filter);

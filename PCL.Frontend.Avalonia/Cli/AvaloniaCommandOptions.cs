using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Cli;

internal sealed record AvaloniaCommandOptions(
    AvaloniaCommandKind Command,
    string Scenario,
    AvaloniaOutputMode Mode,
    AvaloniaOutputFormat Format,
    bool UseHostEnvironment,
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    AvaloniaJavaDownloadSessionState JavaDownloadState,
    MinecraftCrashOutputPromptActionKind CrashAction,
    bool ForceCjkFontWarning,
    string? SaveBatchPath,
    string? WorkspaceRoot,
    string? InputRoot,
    string? ExportArchivePath);

internal sealed record AvaloniaParseResult(
    AvaloniaCommandOptions? Options,
    bool ShowHelp,
    string? ErrorMessage);

internal enum AvaloniaCommandKind
{
    Startup = 0,
    Launch = 1,
    Crash = 2,
    All = 3,
    Shell = 4,
    App = 5
}

internal enum AvaloniaOutputMode
{
    Plan = 0,
    Run = 1,
    Execute = 2
}

internal enum AvaloniaOutputFormat
{
    Json = 0,
    Text = 1
}

internal enum AvaloniaJavaDownloadSessionState
{
    Finished = 0,
    Failed = 1,
    Aborted = 2
}

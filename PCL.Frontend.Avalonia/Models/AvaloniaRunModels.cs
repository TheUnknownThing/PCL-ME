using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record StartupAvaloniaRun(
    AvaloniaTranscript Transcript);

internal sealed record ShellAvaloniaRun(
    AvaloniaTranscript Transcript);

internal sealed record LaunchAvaloniaRun(
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    AvaloniaTranscript Transcript);

internal sealed record CrashAvaloniaRun(
    MinecraftCrashOutputPromptActionKind SelectedAction,
    AvaloniaTranscript Transcript);

internal sealed record AvaloniaRunBundle(
    StartupAvaloniaRun Startup,
    LaunchAvaloniaRun Launch,
    CrashAvaloniaRun Crash);

internal sealed record AvaloniaTranscript(
    string Title,
    IReadOnlyList<AvaloniaTranscriptSection> Sections);

internal sealed record AvaloniaTranscriptSection(
    string Heading,
    IReadOnlyList<string> Lines);

internal sealed record AvaloniaExecutionArtifact(
    string Label,
    string Path);

internal sealed record AvaloniaExecutionSummary(
    string WorkspaceRoot,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<string> DeletedFiles,
    IReadOnlyList<AvaloniaExecutionArtifact> Artifacts);

internal sealed record StartupAvaloniaExecution(
    AvaloniaExecutionSummary Execution,
    AvaloniaTranscript Transcript);

internal sealed record ShellAvaloniaExecution(
    AvaloniaExecutionSummary Execution,
    AvaloniaTranscript Transcript);

internal sealed record LaunchAvaloniaExecution(
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    AvaloniaExecutionSummary Execution,
    AvaloniaTranscript Transcript);

internal sealed record CrashAvaloniaExecution(
    MinecraftCrashOutputPromptActionKind SelectedAction,
    AvaloniaExecutionSummary Execution,
    IReadOnlyList<string> ArchivedFileNames,
    AvaloniaTranscript Transcript);

internal sealed record AvaloniaExecutionBundle(
    StartupAvaloniaExecution Startup,
    LaunchAvaloniaExecution Launch,
    CrashAvaloniaExecution Crash);

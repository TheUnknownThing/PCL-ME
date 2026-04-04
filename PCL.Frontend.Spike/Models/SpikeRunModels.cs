using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record StartupSpikeRun(
    SpikeTranscript Transcript);

internal sealed record ShellSpikeRun(
    SpikeTranscript Transcript);

internal sealed record LaunchSpikeRun(
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    SpikeTranscript Transcript);

internal sealed record CrashSpikeRun(
    MinecraftCrashOutputPromptActionKind SelectedAction,
    SpikeTranscript Transcript);

internal sealed record SpikeRunBundle(
    StartupSpikeRun Startup,
    LaunchSpikeRun Launch,
    CrashSpikeRun Crash);

internal sealed record SpikeTranscript(
    string Title,
    IReadOnlyList<SpikeTranscriptSection> Sections);

internal sealed record SpikeTranscriptSection(
    string Heading,
    IReadOnlyList<string> Lines);

internal sealed record SpikeExecutionArtifact(
    string Label,
    string Path);

internal sealed record SpikeExecutionSummary(
    string WorkspaceRoot,
    IReadOnlyList<string> CreatedDirectories,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<string> DeletedFiles,
    IReadOnlyList<SpikeExecutionArtifact> Artifacts);

internal sealed record StartupSpikeExecution(
    SpikeExecutionSummary Execution,
    SpikeTranscript Transcript);

internal sealed record ShellSpikeExecution(
    SpikeExecutionSummary Execution,
    SpikeTranscript Transcript);

internal sealed record LaunchSpikeExecution(
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    SpikeExecutionSummary Execution,
    SpikeTranscript Transcript);

internal sealed record CrashSpikeExecution(
    MinecraftCrashOutputPromptActionKind SelectedAction,
    SpikeExecutionSummary Execution,
    IReadOnlyList<string> ArchivedFileNames,
    SpikeTranscript Transcript);

internal sealed record SpikeExecutionBundle(
    StartupSpikeExecution Startup,
    LaunchSpikeExecution Launch,
    CrashSpikeExecution Crash);

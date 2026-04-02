using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record StartupSpikeRun(
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

using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Cli;

internal sealed record SpikeCommandOptions(
    SpikeCommandKind Command,
    string Scenario,
    SpikeOutputMode Mode,
    SpikeOutputFormat Format,
    MinecraftLaunchJavaPromptDecision JavaPromptDecision,
    MinecraftCrashOutputPromptActionKind CrashAction,
    string? WorkspaceRoot,
    string? InputRoot);

internal sealed record SpikeParseResult(
    SpikeCommandOptions? Options,
    bool ShowHelp,
    string? ErrorMessage);

internal enum SpikeCommandKind
{
    Startup = 0,
    Launch = 1,
    Crash = 2,
    All = 3
}

internal enum SpikeOutputMode
{
    Plan = 0,
    Run = 1,
    Execute = 2
}

internal enum SpikeOutputFormat
{
    Json = 0,
    Text = 1
}

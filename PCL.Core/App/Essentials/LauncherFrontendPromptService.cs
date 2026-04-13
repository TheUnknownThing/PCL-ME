using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.App.Essentials;

public static class LauncherFrontendPromptService
{
    public static IReadOnlyList<LauncherFrontendPrompt> BuildStartupPromptQueue(
        LauncherStartupWorkflowPlan startupPlan,
        LauncherStartupConsentResult consent)
    {
        ArgumentNullException.ThrowIfNull(startupPlan);
        ArgumentNullException.ThrowIfNull(consent);

        var prompts = new List<LauncherFrontendPrompt>();
        if (startupPlan.EnvironmentWarningPrompt is not null)
        {
            prompts.Add(FromStartupPrompt(
                "startup-environment-warning",
                LauncherFrontendPromptSource.StartupEnvironmentWarning,
                startupPlan.EnvironmentWarningPrompt));
        }

        prompts.AddRange(consent.Prompts.Select((prompt, index) => FromStartupPrompt(
            $"startup-consent-{index}",
            LauncherFrontendPromptSource.StartupConsent,
            prompt)));
        return prompts;
    }

    public static IReadOnlyList<LauncherFrontendPrompt> BuildLaunchPromptQueue(
        MinecraftLaunchPrecheckResult precheckResult,
        MinecraftLaunchPrompt? supportPrompt,
        MinecraftLaunchJavaPrompt? missingJavaPrompt)
    {
        ArgumentNullException.ThrowIfNull(precheckResult);

        var prompts = precheckResult.Prompts
            .Select((prompt, index) => FromLaunchPrompt(
                $"launch-precheck-{index}",
                LauncherFrontendPromptSource.LaunchPrecheck,
                prompt))
            .ToList();

        if (supportPrompt is not null)
        {
            prompts.Add(FromLaunchPrompt(
                "launch-support",
                LauncherFrontendPromptSource.LaunchSupport,
                supportPrompt));
        }

        if (missingJavaPrompt is not null)
        {
            prompts.Add(FromJavaPrompt("launch-java-download", missingJavaPrompt));
        }

        return prompts;
    }

    public static IReadOnlyList<LauncherFrontendPrompt> BuildCrashPromptQueue(MinecraftCrashOutputPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return [FromCrashPrompt("crash-output", prompt)];
    }

    public static LauncherFrontendPrompt FromStartupPrompt(
        string id,
        LauncherFrontendPromptSource source,
        LauncherStartupPrompt prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(prompt);

        return new LauncherFrontendPrompt(
            id,
            source,
            prompt.Title,
            prompt.Message,
            prompt.IsWarning ? LauncherFrontendPromptSeverity.Warning : LauncherFrontendPromptSeverity.Info,
            prompt.Buttons.Select(MapStartupButton).ToArray());
    }

    public static LauncherFrontendPrompt FromLaunchPrompt(
        string id,
        LauncherFrontendPromptSource source,
        MinecraftLaunchPrompt prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(prompt);

        return new LauncherFrontendPrompt(
            id,
            source,
            prompt.Title,
            prompt.Message,
            prompt.IsWarning ? LauncherFrontendPromptSeverity.Warning : LauncherFrontendPromptSeverity.Info,
            prompt.Buttons.Select(MapLaunchButton).ToArray());
    }

    public static LauncherFrontendPrompt FromJavaPrompt(string id, MinecraftLaunchJavaPrompt prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(prompt);

        return new LauncherFrontendPrompt(
            id,
            LauncherFrontendPromptSource.LaunchJavaDownload,
            prompt.Title,
            prompt.Message,
            LauncherFrontendPromptSeverity.Warning,
            prompt.Options.Select(option => MapJavaOption(option, prompt.DownloadTarget)).ToArray());
    }

    public static LauncherFrontendPrompt FromCrashPrompt(string id, MinecraftCrashOutputPrompt prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(prompt);

        return new LauncherFrontendPrompt(
            id,
            LauncherFrontendPromptSource.CrashOutput,
            prompt.Title,
            prompt.Message,
            LauncherFrontendPromptSeverity.Warning,
            prompt.Buttons.Select(MapCrashButton).ToArray());
    }

    private static LauncherFrontendPromptOption MapStartupButton(LauncherStartupPromptButton button)
    {
        return new LauncherFrontendPromptOption(
            button.Label,
            button.ClosesPrompt,
            button.Actions.Select(action => action.Kind switch
            {
                LauncherStartupPromptActionKind.Continue => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.ContinueFlow),
                LauncherStartupPromptActionKind.Accept => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.AcceptConsent),
                LauncherStartupPromptActionKind.Reject => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.RejectConsent),
                LauncherStartupPromptActionKind.OpenUrl => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.OpenUrl,
                    action.Value),
                LauncherStartupPromptActionKind.ExitLauncher => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.ExitLauncher),
                LauncherStartupPromptActionKind.SetTelemetryEnabled => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.SetTelemetryEnabled,
                    action.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unknown startup prompt action.")
            }).ToArray());
    }

    private static LauncherFrontendPromptOption MapLaunchButton(MinecraftLaunchPromptButton button)
    {
        return new LauncherFrontendPromptOption(
            button.Label,
            button.ClosesPrompt,
            button.Actions.Select(action => action.Kind switch
            {
                MinecraftLaunchPromptActionKind.Continue => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.ContinueFlow),
                MinecraftLaunchPromptActionKind.Abort => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.AbortLaunch),
                MinecraftLaunchPromptActionKind.OpenUrl => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.OpenUrl,
                    action.Value),
                MinecraftLaunchPromptActionKind.AppendLaunchArgument => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.AppendLaunchArgument,
                    action.Value),
                MinecraftLaunchPromptActionKind.PersistNonAsciiPathWarningDisabled => new LauncherFrontendPromptCommand(
                    LauncherFrontendPromptCommandKind.PersistSetting,
                    action.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unknown launch prompt action.")
            }).ToArray());
    }

    private static LauncherFrontendPromptOption MapJavaOption(
        MinecraftLaunchJavaPromptOption option,
        string? downloadTarget)
    {
        return new LauncherFrontendPromptOption(
            option.Label,
            ClosesPrompt: true,
            [
                option.Decision switch
                {
                    MinecraftLaunchJavaPromptDecision.Download => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.DownloadJavaRuntime,
                        downloadTarget),
                    MinecraftLaunchJavaPromptDecision.Abort => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.AbortLaunch),
                    _ => throw new ArgumentOutOfRangeException(nameof(option), option.Decision, "Unknown Java prompt decision.")
                }
            ]);
    }

    private static LauncherFrontendPromptOption MapCrashButton(MinecraftCrashOutputPromptButton button)
    {
        return new LauncherFrontendPromptOption(
            button.Label,
            button.ClosesPrompt,
            [
                button.Action switch
                {
                    MinecraftCrashOutputPromptActionKind.Close => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.ClosePrompt),
                    MinecraftCrashOutputPromptActionKind.ViewLog => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.ViewGameLog),
                    MinecraftCrashOutputPromptActionKind.OpenInstanceSettings => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.OpenInstanceSettings),
                    MinecraftCrashOutputPromptActionKind.ExportReport => new LauncherFrontendPromptCommand(
                        LauncherFrontendPromptCommandKind.ExportCrashReport),
                    _ => throw new ArgumentOutOfRangeException(nameof(button), button.Action, "Unknown crash prompt action.")
                }
            ]);
    }
}

public sealed record LauncherFrontendPrompt(
    string Id,
    LauncherFrontendPromptSource Source,
    string Title,
    string Message,
    LauncherFrontendPromptSeverity Severity,
    IReadOnlyList<LauncherFrontendPromptOption> Options);

public sealed record LauncherFrontendPromptOption(
    string Label,
    bool ClosesPrompt,
    IReadOnlyList<LauncherFrontendPromptCommand> Commands);

public sealed record LauncherFrontendPromptCommand(
    LauncherFrontendPromptCommandKind Kind,
    string? Value = null);

public enum LauncherFrontendPromptSource
{
    StartupEnvironmentWarning = 0,
    StartupConsent = 1,
    LaunchPrecheck = 2,
    LaunchSupport = 3,
    LaunchJavaDownload = 4,
    CrashOutput = 5
}

public enum LauncherFrontendPromptSeverity
{
    Info = 0,
    Warning = 1
}

public enum LauncherFrontendPromptCommandKind
{
    ContinueFlow = 0,
    AcceptConsent = 1,
    RejectConsent = 2,
    OpenUrl = 3,
    ExitLauncher = 4,
    SetTelemetryEnabled = 5,
    AbortLaunch = 6,
    AppendLaunchArgument = 7,
    PersistSetting = 8,
    DownloadJavaRuntime = 9,
    ClosePrompt = 10,
    ViewGameLog = 11,
    OpenInstanceSettings = 12,
    ExportCrashReport = 13
}

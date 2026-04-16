using System;
using PCL.Core.App.I18n;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupShellService
{
    public static LauncherStartupImmediateCommandPlan ResolveImmediateCommand(string[]? arguments)
    {
        var command = LauncherStartupCommandService.Parse(arguments);
        return command.Kind switch
        {
            LauncherStartupCommandKind.SetGpuPreference when !command.IsValid => new LauncherStartupImmediateCommandPlan(
                LauncherStartupImmediateCommandKind.Invalid,
                InvalidMessage: "The executable path required for GPU preference configuration is missing."),
            LauncherStartupCommandKind.SetGpuPreference => new LauncherStartupImmediateCommandPlan(
                LauncherStartupImmediateCommandKind.SetGpuPreference,
                command.Argument),
            LauncherStartupCommandKind.OptimizeMemory => new LauncherStartupImmediateCommandPlan(
                LauncherStartupImmediateCommandKind.OptimizeMemory),
            _ => LauncherStartupImmediateCommandPlan.None
        };
    }

    public static LauncherStartupPrompt? GetEnvironmentWarningPrompt(string? warningMessage)
    {
        if (string.IsNullOrWhiteSpace(warningMessage))
        {
            return null;
        }

        return new LauncherStartupPrompt(
            I18nText.WithArgs(
                "startup.prompts.environment_warning.message",
                I18nTextArgument.String("warnings", warningMessage)),
            I18nText.Plain("startup.prompts.environment_warning.title"),
            [
                new LauncherStartupPromptButton(
                    I18nText.Plain("startup.prompts.environment_warning.actions.acknowledge"),
                    [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Continue)])
            ],
            IsWarning: true);
    }
}

public sealed record LauncherStartupImmediateCommandPlan(
    LauncherStartupImmediateCommandKind Kind,
    string? Argument = null,
    string? InvalidMessage = null)
{
    public static LauncherStartupImmediateCommandPlan None { get; } = new(LauncherStartupImmediateCommandKind.None);
}

public enum LauncherStartupImmediateCommandKind
{
    None = 0,
    SetGpuPreference = 1,
    OptimizeMemory = 2,
    Invalid = 3
}

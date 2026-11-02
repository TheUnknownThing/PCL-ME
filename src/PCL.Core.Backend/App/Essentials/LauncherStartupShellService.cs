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
                InvalidMessage: I18nText.Plain("shell.status.commands.invalid_message.gpu_preference_path_missing")),
            LauncherStartupCommandKind.SetGpuPreference => new LauncherStartupImmediateCommandPlan(
                LauncherStartupImmediateCommandKind.SetGpuPreference,
                command.Argument),
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
    I18nText? InvalidMessage = null)
{
    public static LauncherStartupImmediateCommandPlan None { get; } = new(LauncherStartupImmediateCommandKind.None);
}

public enum LauncherStartupImmediateCommandKind
{
    None = 0,
    SetGpuPreference = 1,
    Invalid = 2
}

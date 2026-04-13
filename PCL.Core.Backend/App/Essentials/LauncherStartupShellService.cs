using System;

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
                InvalidMessage: "缺少需要设置显卡偏好的可执行文件路径。"),
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
            warningMessage,
            "环境警告",
            [new LauncherStartupPromptButton("我知道了", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Continue)])],
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

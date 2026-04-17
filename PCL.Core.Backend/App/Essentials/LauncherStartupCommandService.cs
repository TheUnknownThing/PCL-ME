using System;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupCommandService
{
    public static LauncherStartupCommand Parse(string[]? arguments)
    {
        if (arguments is not { Length: > 0 })
        {
            return LauncherStartupCommand.None;
        }

        var commandText = arguments[0] ?? string.Empty;
        if (string.Equals(commandText, "--gpu", StringComparison.Ordinal))
        {
            if (arguments.Length < 2 || string.IsNullOrWhiteSpace(arguments[1]))
            {
                return new LauncherStartupCommand(LauncherStartupCommandKind.SetGpuPreference, null, IsValid: false);
            }

            return new LauncherStartupCommand(
                LauncherStartupCommandKind.SetGpuPreference,
                arguments[1].Trim('"'),
                IsValid: true);
        }

        return LauncherStartupCommand.None;
    }
}

public sealed record LauncherStartupCommand(
    LauncherStartupCommandKind Kind,
    string? Argument,
    bool IsValid)
{
    public static LauncherStartupCommand None { get; } = new(LauncherStartupCommandKind.None, null, IsValid: true);
}

public enum LauncherStartupCommandKind
{
    None = 0,
    SetGpuPreference = 1
}

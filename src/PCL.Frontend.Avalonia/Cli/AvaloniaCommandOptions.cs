namespace PCL.Frontend.Avalonia.Cli;

internal enum AvaloniaCommandKind
{
    App = 0,
    LaunchInstance = 1,
    Register = 2,
    Unregister = 3
}

internal sealed record AvaloniaCommandOptions(
    AvaloniaCommandKind Command,
    string Scenario,
    bool ForceCjkFontWarning,
    string? InstanceNameOverride,
    bool DisableExtensions = false);

internal sealed record AvaloniaParseResult(
    AvaloniaCommandOptions? Options,
    bool ShowHelp,
    string? ErrorMessage);

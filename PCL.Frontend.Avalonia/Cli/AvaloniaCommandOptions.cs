namespace PCL.Frontend.Avalonia.Cli;

internal sealed record AvaloniaCommandOptions(
    string Scenario,
    bool ForceCjkFontWarning);

internal sealed record AvaloniaParseResult(
    AvaloniaCommandOptions? Options,
    bool ShowHelp,
    string? ErrorMessage);

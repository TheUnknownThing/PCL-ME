namespace PCL.Frontend.Avalonia.Cli;

internal static class AvaloniaCommandParser
{
    public static AvaloniaParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return Success(CreateDefaultOptions());
        }

        if (IsHelpToken(args[0]))
        {
            return new AvaloniaParseResult(null, ShowHelp: true, ErrorMessage: null);
        }

        if (IsLegacyInspectionCommand(args[0]))
        {
            return Error($"Legacy inspection command '{args[0]}' has been removed. Run the desktop app with `app` or no arguments.");
        }

        if (string.Equals(args[0], "app", StringComparison.OrdinalIgnoreCase))
        {
            return ParseAppCommand(args);
        }

        if (string.Equals(args[0], "launch-instance", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLaunchInstanceCommand(args);
        }

        if (string.Equals(args[0], "register", StringComparison.OrdinalIgnoreCase))
        {
            return ParseNoArgumentCommand(args, AvaloniaCommandKind.Register);
        }

        if (string.Equals(args[0], "unregister", StringComparison.OrdinalIgnoreCase))
        {
            return ParseNoArgumentCommand(args, AvaloniaCommandKind.Unregister);
        }

        return Error($"Unknown command '{args[0]}'.");
    }

    private static AvaloniaParseResult ParseAppCommand(string[] args)
    {
        var scenario = "modern-fabric";
        var forceCjkFontWarning = false;
        var index = 1;

        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Unexpected positional argument '{token}'.");
            }

            var (name, value, nextIndex, errorMessage) = ReadOption(args, index);
            if (errorMessage is not null)
            {
                return Error(errorMessage);
            }

            switch (name)
            {
                case "scenario":
                    scenario = value!.Trim().ToLowerInvariant();
                    break;
                case "force-cjk-font-warning":
                    if (!TryParseBooleanFlag(value!, out forceCjkFontWarning))
                    {
                        return Error($"Unknown CJK font warning flag '{value}'.");
                    }
                    break;
                default:
                    return Error($"Unknown option '--{name}'.");
            }

            index = nextIndex;
        }

        return Success(new AvaloniaCommandOptions(
            AvaloniaCommandKind.App,
            scenario,
            forceCjkFontWarning,
            InstanceNameOverride: null));
    }

    private static AvaloniaParseResult ParseLaunchInstanceCommand(string[] args)
    {
        string? instanceName = null;
        var index = 1;

        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Unexpected positional argument '{token}'.");
            }

            var (name, value, nextIndex, errorMessage) = ReadOption(args, index);
            if (errorMessage is not null)
            {
                return Error(errorMessage);
            }

            switch (name)
            {
                case "instance":
                    instanceName = value?.Trim();
                    break;
                default:
                    return Error($"Unknown option '--{name}'.");
            }

            index = nextIndex;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return Error("Option '--instance' is required.");
        }

        return Success(new AvaloniaCommandOptions(
            AvaloniaCommandKind.LaunchInstance,
            Scenario: "modern-fabric",
            ForceCjkFontWarning: false,
            InstanceNameOverride: instanceName));
    }

    private static AvaloniaParseResult ParseNoArgumentCommand(string[] args, AvaloniaCommandKind command)
    {
        if (args.Length > 1)
        {
            return Error($"Unexpected argument '{args[1]}'.");
        }

        return Success(new AvaloniaCommandOptions(
            command,
            Scenario: "modern-fabric",
            ForceCjkFontWarning: false,
            InstanceNameOverride: null));
    }

    public static string GetUsageText()
    {
        return """
PCL.Frontend.Avalonia

Usage:
  app [--scenario modern-fabric|legacy-forge] [--force-cjk-font-warning true|false]
  launch-instance --instance <instance-name>
  register
  unregister
  help

Defaults:
  command: app
  scenario: modern-fabric
  force cjk font warning: false
""";
    }

    public static AvaloniaCommandOptions CreateDefaultOptions() =>
        new(AvaloniaCommandKind.App, "modern-fabric", false, InstanceNameOverride: null);

    private static AvaloniaParseResult Success(AvaloniaCommandOptions options) =>
        new(options, ShowHelp: false, ErrorMessage: null);

    private static AvaloniaParseResult Error(string message) =>
        new(null, ShowHelp: false, ErrorMessage: message);

    private static bool IsHelpToken(string token) =>
        token is "help" or "--help" or "-h";

    private static bool IsLegacyInspectionCommand(string token)
    {
        return token.Trim().ToLowerInvariant() is "startup" or "shell" or "launch" or "crash" or "all";
    }

    private static bool TryParseBooleanFlag(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                result = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static (string Name, string? Value, int NextIndex, string? ErrorMessage) ReadOption(string[] args, int index)
    {
        var token = args[index];
        var separatorIndex = token.IndexOf('=');
        if (separatorIndex >= 0)
        {
            var name = token[2..separatorIndex];
            var value = token[(separatorIndex + 1)..];
            return (name, value, index + 1, string.IsNullOrWhiteSpace(name) ? "Option name cannot be empty." : null);
        }

        if (index + 1 >= args.Length)
        {
            return (token[2..], null, index + 1, $"Option '{token}' requires a value.");
        }

        return (token[2..], args[index + 1], index + 2, null);
    }
}

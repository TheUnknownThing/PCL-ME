namespace PCL.Tools.I18n;

public static class I18nToolCommandParser
{
    public static I18nToolParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new I18nToolParseResult(null, ShowHelp: true, ErrorMessage: null);
        }

        if (IsHelpToken(args[0]))
        {
            return new I18nToolParseResult(null, ShowHelp: true, ErrorMessage: null);
        }

        var subcommand = args[0].Trim().ToLowerInvariant();
        if (subcommand == "schema")
        {
            return ParseSchemaCommand(args);
        }

        var kind = subcommand switch
        {
            "get" => I18nToolCommandKind.Get,
            "set" => I18nToolCommandKind.Set,
            "tree" => I18nToolCommandKind.Tree,
            "validate" => I18nToolCommandKind.Validate,
            _ => (I18nToolCommandKind?)null
        };

        if (kind is null)
        {
            return Error($"Unknown command '{args[0]}'.");
        }

        string? locale = null;
        string? key = null;
        string? value = null;
        string? localesDirectory = null;
        string? prefix = null;
        IReadOnlyList<string>? placeholders = null;
        var outputFormat = I18nToolOutputFormat.Text;
        var index = 1;

        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Unexpected positional argument '{token}'.");
            }

            var (name, optionValue, nextIndex, errorMessage) = ReadOption(args, index);
            if (errorMessage is not null)
            {
                return Error(errorMessage);
            }

            switch (name)
            {
                case "locale":
                    locale = optionValue;
                    break;
                case "key":
                    key = optionValue;
                    break;
                case "value":
                    value = optionValue;
                    break;
                case "locales-dir":
                    localesDirectory = optionValue;
                    break;
                case "prefix":
                    prefix = optionValue;
                    break;
                case "format":
                    if (!TryParseOutputFormat(optionValue, out outputFormat))
                    {
                        return Error($"Unknown output format '{optionValue}'.");
                    }
                    break;
                default:
                    return Error($"Unknown option '--{name}'.");
            }

            index = nextIndex;
        }

        if (kind == I18nToolCommandKind.Get && (locale is null || key is null))
        {
            return Error("The get command requires --locale and --key.");
        }

        if (kind == I18nToolCommandKind.Set && (locale is null || key is null || value is null))
        {
            return Error("The set command requires --locale, --key, and --value.");
        }

        return new I18nToolParseResult(
            new I18nToolCommandOptions(kind.Value, locale, key, value, localesDirectory, prefix, placeholders, outputFormat),
            ShowHelp: false,
            ErrorMessage: null);
    }

    public static string GetUsageText()
    {
        return """
PCL.Tools.I18n

Usage:
  get --locale <locale> --key <dot.path> [--locales-dir <path>]
  set --locale <locale> --key <dot.path> --value <text> [--locales-dir <path>]
  tree --locale <locale> [--prefix <dot.path>] [--locales-dir <path>]
  validate [--locale <locale>] [--locales-dir <path>] [--format text|msbuild]
  schema get --key <dot.path> [--locales-dir <path>]
  schema set --key <dot.path> [--placeholders <csv>] [--locales-dir <path>]
  schema remove --key <dot.path> [--locales-dir <path>]
  schema tree [--prefix <dot.path>] [--locales-dir <path>]
  help
""";
    }

    private static I18nToolParseResult Error(string message) =>
        new(null, ShowHelp: false, ErrorMessage: message);

    private static bool IsHelpToken(string token) =>
        token is "help" or "--help" or "-h";

    private static I18nToolParseResult ParseSchemaCommand(string[] args)
    {
        if (args.Length < 2)
        {
            return Error("The schema command requires a subcommand.");
        }

        var subcommand = args[1].Trim().ToLowerInvariant();
        var kind = subcommand switch
        {
            "get" => I18nToolCommandKind.SchemaGet,
            "set" => I18nToolCommandKind.SchemaSet,
            "remove" => I18nToolCommandKind.SchemaRemove,
            "tree" => I18nToolCommandKind.SchemaTree,
            _ => (I18nToolCommandKind?)null
        };

        if (kind is null)
        {
            return Error($"Unknown schema subcommand '{args[1]}'.");
        }

        string? key = null;
        string? localesDirectory = null;
        string? prefix = null;
        IReadOnlyList<string>? placeholders = null;
        var outputFormat = I18nToolOutputFormat.Text;
        var index = 2;

        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Unexpected positional argument '{token}'.");
            }

            var (name, optionValue, nextIndex, errorMessage) = ReadOption(args, index);
            if (errorMessage is not null)
            {
                return Error(errorMessage);
            }

            switch (name)
            {
                case "key":
                    key = optionValue;
                    break;
                case "locales-dir":
                    localesDirectory = optionValue;
                    break;
                case "prefix":
                    prefix = optionValue;
                    break;
                case "placeholders":
                    placeholders = ParsePlaceholders(optionValue);
                    break;
                case "format":
                    if (!TryParseOutputFormat(optionValue, out outputFormat))
                    {
                        return Error($"Unknown output format '{optionValue}'.");
                    }
                    break;
                default:
                    return Error($"Unknown option '--{name}'.");
            }

            index = nextIndex;
        }

        if ((kind == I18nToolCommandKind.SchemaGet ||
             kind == I18nToolCommandKind.SchemaSet ||
             kind == I18nToolCommandKind.SchemaRemove) &&
            key is null)
        {
            return Error($"The schema {subcommand} command requires --key.");
        }

        return new I18nToolParseResult(
            new I18nToolCommandOptions(kind.Value, Locale: null, key, Value: null, localesDirectory, prefix, placeholders, outputFormat),
            ShowHelp: false,
            ErrorMessage: null);
    }

    private static IReadOnlyList<string> ParsePlaceholders(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryParseOutputFormat(string? rawValue, out I18nToolOutputFormat format)
    {
        switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "text":
                format = I18nToolOutputFormat.Text;
                return true;
            case "msbuild":
                format = I18nToolOutputFormat.MsBuild;
                return true;
            default:
                format = I18nToolOutputFormat.Text;
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

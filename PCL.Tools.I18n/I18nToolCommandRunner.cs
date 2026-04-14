namespace PCL.Tools.I18n;

public static class I18nToolCommandRunner
{
    public static int Run(I18nToolCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var localeDirectory = ResolveLocaleDirectory(options.LocalesDirectory);
            var editor = new I18nYamlEditor(localeDirectory);
            return options.Kind switch
            {
                I18nToolCommandKind.Get => RunGet(editor, options),
                I18nToolCommandKind.Set => RunSet(editor, options),
                I18nToolCommandKind.Tree => RunTree(editor, options),
                I18nToolCommandKind.Validate => RunValidate(editor, options),
                I18nToolCommandKind.SchemaGet => RunSchemaGet(editor, options),
                I18nToolCommandKind.SchemaSet => RunSchemaSet(editor, options),
                I18nToolCommandKind.SchemaRemove => RunSchemaRemove(editor, options),
                I18nToolCommandKind.SchemaTree => RunSchemaTree(editor, options),
                _ => throw new InvalidOperationException($"Unsupported command '{options.Kind}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunGet(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Locale is null || options.Key is null)
        {
            throw new InvalidOperationException("The get command requires --locale and --key.");
        }

        if (!editor.TryReadLocaleValue(options.Locale, options.Key, out var value))
        {
            Console.Error.WriteLine($"Locale '{options.Locale}' does not define key '{options.Key}'.");
            return 1;
        }

        Console.WriteLine(value);
        return 0;
    }

    private static int RunSet(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Locale is null || options.Key is null || options.Value is null)
        {
            throw new InvalidOperationException("The set command requires --locale, --key, and --value.");
        }

        editor.SetLocaleValue(options.Locale, options.Key, options.Value);
        Console.WriteLine($"Updated '{options.Key}' in locale '{options.Locale}'.");
        return 0;
    }

    private static int RunValidate(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        var report = editor.Validate(options.Locale);
        foreach (var issue in report.Issues)
        {
            Console.WriteLine(FormatIssue(issue, options.OutputFormat));
        }

        if (report.HasErrors)
        {
            return 1;
        }

        if (options.OutputFormat == I18nToolOutputFormat.Text)
        {
            Console.WriteLine($"Validation passed for {report.Locales.Count} locale(s) with {report.Warnings.Count} warning(s).");
        }

        return 0;
    }

    private static int RunTree(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Locale is null)
        {
            throw new InvalidOperationException("The tree command requires --locale.");
        }

        var lines = editor.RenderLocaleTree(options.Locale, options.Prefix);
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static int RunSchemaGet(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Key is null)
        {
            throw new InvalidOperationException("The schema get command requires --key.");
        }

        if (!editor.TryReadSchemaValue(options.Key, out var placeholders))
        {
            Console.Error.WriteLine($"Schema does not define key '{options.Key}'.");
            return 1;
        }

        Console.WriteLine(placeholders.Count == 0 ? "[]" : "[" + string.Join(", ", placeholders) + "]");
        return 0;
    }

    private static int RunSchemaSet(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Key is null)
        {
            throw new InvalidOperationException("The schema set command requires --key.");
        }

        editor.SetSchemaValue(options.Key, options.Placeholders ?? []);
        Console.WriteLine(
            $"Updated schema key '{options.Key}' with placeholders [{string.Join(", ", options.Placeholders ?? [])}].");
        return 0;
    }

    private static int RunSchemaRemove(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        if (options.Key is null)
        {
            throw new InvalidOperationException("The schema remove command requires --key.");
        }

        if (!editor.RemoveSchemaValue(options.Key))
        {
            Console.Error.WriteLine($"Schema does not define key '{options.Key}'.");
            return 1;
        }

        Console.WriteLine($"Removed schema key '{options.Key}'.");
        return 0;
    }

    private static int RunSchemaTree(I18nYamlEditor editor, I18nToolCommandOptions options)
    {
        var lines = editor.RenderSchemaTree(options.Prefix);
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static string ResolveLocaleDirectory(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "PCL.Frontend.Avalonia", "Locales"),
            Path.Combine(Environment.CurrentDirectory, "Locales"),
            Path.Combine(AppContext.BaseDirectory, "Locales")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static string FormatIssue(I18nValidationIssue issue, I18nToolOutputFormat format)
    {
        if (format == I18nToolOutputFormat.MsBuild)
        {
            var prefix = issue.Severity == I18nValidationSeverity.Warning ? "WARNING" : "ERROR";
            return $"{prefix}|{issue.Message}";
        }

        var severity = issue.Severity == I18nValidationSeverity.Warning ? "warning" : "error";
        return $"{severity}: {issue.Message}";
    }
}

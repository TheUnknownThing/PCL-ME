namespace PCL.Tools.I18n;

public sealed record I18nToolCommandOptions(
    I18nToolCommandKind Kind,
    string? Locale,
    string? Key,
    string? Value,
    string? LocalesDirectory,
    string? Prefix,
    IReadOnlyList<string>? Placeholders,
    I18nToolOutputFormat OutputFormat);

public sealed record I18nToolParseResult(
    I18nToolCommandOptions? Options,
    bool ShowHelp,
    string? ErrorMessage);

public enum I18nToolCommandKind
{
    Get,
    Set,
    Tree,
    Validate,
    SchemaGet,
    SchemaSet,
    SchemaRemove,
    SchemaTree
}

public enum I18nToolOutputFormat
{
    Text,
    MsBuild
}

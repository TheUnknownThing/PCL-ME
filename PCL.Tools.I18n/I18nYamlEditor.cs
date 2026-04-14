using System.Diagnostics.CodeAnalysis;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PCL.Tools.I18n;

public sealed class I18nYamlEditor
{
    private const string PlaceholderSuffix = ":placeholder";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .DisableAliases()
        .Build();

    private readonly string _localeDirectory;
    private readonly string _metaDirectory;
    private readonly string _schemaPath;
    private readonly string _manifestPath;

    public I18nYamlEditor(string localeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localeDirectory);

        _localeDirectory = Path.GetFullPath(localeDirectory);
        _metaDirectory = Path.Combine(_localeDirectory, "Meta");
        _schemaPath = Path.Combine(_metaDirectory, "schema.yaml");
        _manifestPath = Path.Combine(_metaDirectory, "manifest.yaml");
    }

    public IReadOnlyDictionary<string, string> ReadManifestLocales()
    {
        var root = LoadRequiredMapping(_manifestPath, "manifest");
        if (!TryGetChild(root, "locales", out var localesNode) || localesNode is not YamlMappingNode localesMapping)
        {
            throw new InvalidDataException($"Manifest file '{_manifestPath}' must contain a 'locales' mapping.");
        }

        var locales = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in localesMapping.Children)
        {
            var localeKey = ReadRequiredScalar(entry.Key, _manifestPath, "Manifest locale entries must use scalar keys.");
            var normalizedLocale = I18nLocaleUtility.NormalizeLocale(localeKey)
                                   ?? throw new InvalidDataException(
                                       $"Manifest file '{_manifestPath}' contains an invalid locale '{localeKey}'.");
            if (locales.ContainsKey(normalizedLocale))
            {
                throw new InvalidDataException(
                    $"Manifest file '{_manifestPath}' contains a duplicate locale '{normalizedLocale}'.");
            }

            if (entry.Value is not YamlScalarNode displayNameNode)
            {
                throw new InvalidDataException(
                    $"Manifest file '{_manifestPath}' contains a non-scalar display name for locale '{normalizedLocale}'.");
            }

            locales[normalizedLocale] = displayNameNode.Value ?? string.Empty;
        }

        return locales;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ReadSchema()
    {
        var schemaRoot = LoadRequiredMapping(_schemaPath, "schema");
        var schema = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        FlattenSchema(schemaRoot, prefix: null, schema);
        return schema;
    }

    public bool TryReadSchemaValue(string key, [NotNullWhen(true)] out IReadOnlyList<string>? placeholders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            return ReadSchema().TryGetValue(NormalizeKeyOrThrow(key), out placeholders);
        }
        catch (FileNotFoundException)
        {
            placeholders = null;
            return false;
        }
    }

    public IReadOnlyDictionary<string, string> ReadLocaleValues(string locale)
    {
        var normalizedLocale = NormalizeLocaleOrThrow(locale);
        var localePath = GetLocalePath(normalizedLocale);
        var root = LoadRequiredMapping(localePath, "locale");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        FlattenLocale(root, prefix: null, values, localePath);
        return values;
    }

    public bool TryReadLocaleValue(string locale, string key, [NotNullWhen(true)] out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var values = ReadLocaleValues(locale);
            return values.TryGetValue(key.Trim(), out value);
        }
        catch (FileNotFoundException)
        {
            value = null;
            return false;
        }
    }

    public void SetLocaleValue(string locale, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var normalizedLocale = NormalizeLocaleOrThrow(locale);
        var normalizedKey = NormalizeKeyOrThrow(key);
        var schema = ReadSchema();
        if (!schema.TryGetValue(normalizedKey, out var expectedPlaceholders))
        {
            throw new KeyNotFoundException($"Schema file '{_schemaPath}' does not define key '{normalizedKey}'.");
        }

        var manifestLocales = ReadManifestLocales();
        if (!manifestLocales.ContainsKey(normalizedLocale))
        {
            throw new InvalidOperationException(
                $"Manifest file '{_manifestPath}' does not declare locale '{normalizedLocale}'.");
        }

        var localePath = GetLocalePath(normalizedLocale);
        var root = File.Exists(localePath)
            ? LoadRequiredMapping(localePath, "locale")
            : new YamlMappingNode();

        ValidatePlaceholderSet(normalizedKey, value, expectedPlaceholders, localePath);
        SetLocaleLeaf(root, normalizedKey, value, localePath);
        WriteYaml(localePath, root);
    }

    public void SetSchemaValue(string key, IReadOnlyList<string> placeholders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(placeholders);

        var normalizedKey = NormalizeKeyOrThrow(key);
        var normalizedPlaceholders = NormalizePlaceholders(placeholders);
        var root = File.Exists(_schemaPath)
            ? LoadRequiredMapping(_schemaPath, "schema")
            : new YamlMappingNode();

        SetSchemaLeaf(root, normalizedKey, normalizedPlaceholders);
        WriteYaml(_schemaPath, root);

        EnsureLocaleSentinelsForSchemaKey(normalizedKey);
    }

    public bool RemoveSchemaValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!File.Exists(_schemaPath))
        {
            return false;
        }

        var normalizedKey = NormalizeKeyOrThrow(key);
        var root = LoadRequiredMapping(_schemaPath, "schema");
        if (!RemovePath(root, normalizedKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), index: 0))
        {
            return false;
        }

        WriteYaml(_schemaPath, root);
        RemoveLocaleKeyFromDeclaredLocales(normalizedKey);
        return true;
    }

    public IReadOnlyList<string> RenderSchemaTree(string? prefix = null)
    {
        var schema = ReadSchema();
        var entries = prefix is null
            ? schema
            : schema.Where(entry => IsInTreePrefix(entry.Key, NormalizeKeyOrThrow(prefix)))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return RenderTree(
            entries,
            placeholders => placeholders.Count == 0 ? " []" : " [" + string.Join(", ", placeholders) + "]");
    }

    public IReadOnlyList<string> RenderLocaleTree(string locale, string? prefix = null)
    {
        var localeValues = ReadLocaleValues(locale);
        var entries = prefix is null
            ? localeValues
            : localeValues.Where(entry => IsInTreePrefix(entry.Key, NormalizeKeyOrThrow(prefix)))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return RenderTree(
            entries,
            value => " = " + QuoteForDisplay(value));
    }

    public I18nValidationReport Validate(string? locale = null)
    {
        var issues = new List<I18nValidationIssue>();
        IReadOnlyDictionary<string, string> manifestLocales;
        IReadOnlyDictionary<string, IReadOnlyList<string>> schema;

        try
        {
            manifestLocales = ReadManifestLocales();
        }
        catch (Exception ex)
        {
            return new I18nValidationReport([], [new I18nValidationIssue(I18nValidationSeverity.Error, "manifest.invalid", ex.Message, FilePath: _manifestPath)]);
        }

        try
        {
            schema = ReadSchema();
        }
        catch (Exception ex)
        {
            return new I18nValidationReport([], [new I18nValidationIssue(I18nValidationSeverity.Error, "schema.invalid", ex.Message, FilePath: _schemaPath)]);
        }

        var localesToValidate = new List<string>();
        if (string.IsNullOrWhiteSpace(locale))
        {
            localesToValidate.AddRange(manifestLocales.Keys);
            foreach (var localeFile in EnumerateLocaleFiles())
            {
                var fileLocale = Path.GetFileNameWithoutExtension(localeFile);
                var normalizedFileLocale = I18nLocaleUtility.NormalizeLocale(fileLocale);
                if (normalizedFileLocale is null)
                {
                    issues.Add(new I18nValidationIssue(
                        I18nValidationSeverity.Error,
                        "locale.filename_invalid",
                        $"Locale file '{localeFile}' uses an invalid locale name '{fileLocale}'.",
                        FilePath: localeFile));
                    continue;
                }

                if (!manifestLocales.ContainsKey(normalizedFileLocale))
                {
                    issues.Add(new I18nValidationIssue(
                        I18nValidationSeverity.Error,
                        "locale.undeclared",
                        $"Locale file '{localeFile}' is not declared in manifest.",
                        Locale: normalizedFileLocale,
                        FilePath: localeFile));
                }
            }
        }
        else
        {
            var normalizedLocale = NormalizeLocaleOrThrow(locale);
            localesToValidate.Add(normalizedLocale);
            if (!manifestLocales.ContainsKey(normalizedLocale))
            {
                issues.Add(new I18nValidationIssue(
                    I18nValidationSeverity.Error,
                    "locale.undeclared",
                    $"Manifest file '{_manifestPath}' does not declare locale '{normalizedLocale}'.",
                    Locale: normalizedLocale,
                    FilePath: _manifestPath));
            }
        }

        foreach (var localeName in localesToValidate.Distinct(StringComparer.Ordinal))
        {
            ValidateLocale(localeName, schema, issues);
        }

        return new I18nValidationReport(localesToValidate.Distinct(StringComparer.Ordinal).ToArray(), issues);
    }

    private IEnumerable<string> EnumerateLocaleFiles()
    {
        if (!Directory.Exists(_localeDirectory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(_localeDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }
    }

    private void ValidateLocale(
        string locale,
        IReadOnlyDictionary<string, IReadOnlyList<string>> schema,
        ICollection<I18nValidationIssue> issues)
    {
        var localePath = GetLocalePath(locale);
        if (!File.Exists(localePath))
        {
            issues.Add(new I18nValidationIssue(
                I18nValidationSeverity.Error,
                "locale.file_missing",
                $"Locale '{locale}' is declared in manifest but file '{localePath}' does not exist.",
                Locale: locale,
                FilePath: localePath));
            return;
        }

        IReadOnlyDictionary<string, string> localeValues;
        try
        {
            localeValues = ReadLocaleValues(locale);
        }
        catch (Exception ex)
        {
            issues.Add(new I18nValidationIssue(
                I18nValidationSeverity.Error,
                "locale.invalid",
                ex.Message,
                Locale: locale,
                FilePath: localePath));
            return;
        }

        foreach (var schemaEntry in schema)
        {
            if (!localeValues.TryGetValue(schemaEntry.Key, out var value))
            {
                issues.Add(new I18nValidationIssue(
                    I18nValidationSeverity.Error,
                    "locale.key_missing",
                    $"Locale '{locale}' is missing key '{schemaEntry.Key}'.",
                    Locale: locale,
                    Key: schemaEntry.Key,
                    FilePath: localePath));
                continue;
            }

            if (string.Equals(value, GetPlaceholderSentinelValue(schemaEntry.Key), StringComparison.Ordinal))
            {
                issues.Add(new I18nValidationIssue(
                    I18nValidationSeverity.Warning,
                    "locale.placeholder_value",
                    $"Locale '{locale}' still uses placeholder sentinel for key '{schemaEntry.Key}'.",
                    Locale: locale,
                    Key: schemaEntry.Key,
                    FilePath: localePath));
                continue;
            }

            try
            {
                ValidatePlaceholderSet(schemaEntry.Key, value, schemaEntry.Value, localePath);
            }
            catch (Exception ex)
            {
                issues.Add(new I18nValidationIssue(
                    I18nValidationSeverity.Error,
                    "locale.placeholder_mismatch",
                    ex.Message,
                    Locale: locale,
                    Key: schemaEntry.Key,
                    FilePath: localePath));
            }
        }

        foreach (var localeEntry in localeValues.Keys)
        {
            if (schema.ContainsKey(localeEntry))
            {
                continue;
            }

            issues.Add(new I18nValidationIssue(
                I18nValidationSeverity.Error,
                "locale.key_extra",
                $"Locale '{locale}' defines extra key '{localeEntry}' that is not present in schema.",
                Locale: locale,
                Key: localeEntry,
                FilePath: localePath));
        }
    }

    private void FlattenSchema(
        YamlMappingNode mappingNode,
        string? prefix,
        IDictionary<string, IReadOnlyList<string>> schema)
    {
        foreach (var entry in mappingNode.Children)
        {
            var keySegment = ReadRequiredScalar(entry.Key, _schemaPath, "Schema keys must be scalar values.");
            var key = prefix is null ? keySegment : prefix + "." + keySegment;

            switch (entry.Value)
            {
                case YamlMappingNode childMapping:
                    FlattenSchema(childMapping, key, schema);
                    break;
                case YamlSequenceNode placeholderList:
                    schema[key] = ReadPlaceholderList(placeholderList);
                    break;
                default:
                    throw new InvalidDataException(
                        $"Schema file '{_schemaPath}' contains a non-mapping, non-sequence node for key '{key}'.");
            }
        }
    }

    private static IReadOnlyList<string> ReadPlaceholderList(YamlSequenceNode placeholderList)
    {
        return NormalizePlaceholders(placeholderList.Children.Select(child =>
        {
            if (child is not YamlScalarNode scalarNode || string.IsNullOrWhiteSpace(scalarNode.Value))
            {
                throw new InvalidDataException("Schema placeholder entries must be non-empty scalar values.");
            }

            return scalarNode.Value;
        }).ToArray());
    }

    private static void FlattenLocale(
        YamlMappingNode mappingNode,
        string? prefix,
        IDictionary<string, string> values,
        string localePath)
    {
        foreach (var entry in mappingNode.Children)
        {
            var keySegment = ReadRequiredScalar(entry.Key, localePath, "Locale keys must be scalar values.");
            var key = prefix is null ? keySegment : prefix + "." + keySegment;

            switch (entry.Value)
            {
                case YamlMappingNode childMapping:
                    FlattenLocale(childMapping, key, values, localePath);
                    break;
                case YamlScalarNode scalarNode:
                    values[key] = scalarNode.Value ?? string.Empty;
                    break;
                default:
                    throw new InvalidDataException(
                        $"Locale file '{localePath}' contains a non-scalar leaf for key '{key}'.");
            }
        }
    }

    private static void ValidatePlaceholderSet(
        string key,
        string value,
        IReadOnlyList<string> expectedPlaceholders,
        string localePath)
    {
        var actualPlaceholderSet = ParsePlaceholderNames(key, value, localePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedPlaceholderSet = expectedPlaceholders.ToHashSet(StringComparer.Ordinal);
        if (actualPlaceholderSet.SetEquals(expectedPlaceholderSet))
        {
            return;
        }

        var expected = string.Join(", ", expectedPlaceholderSet.OrderBy(name => name, StringComparer.Ordinal));
        var actual = string.Join(", ", actualPlaceholderSet.OrderBy(name => name, StringComparer.Ordinal));
        throw new InvalidDataException(
            $"Locale file '{localePath}' contains placeholder mismatch for key '{key}'. Expected [{expected}] but found [{actual}].");
    }

    private static IReadOnlyList<string> ParsePlaceholderNames(string key, string value, string localePath)
    {
        var placeholders = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '{':
                    if (index + 1 < value.Length && value[index + 1] == '{')
                    {
                        index++;
                        continue;
                    }

                    var closingIndex = value.IndexOf('}', index + 1);
                    if (closingIndex < 0)
                    {
                        throw CreateTemplateFormatException(key, localePath, "Unmatched '{' brace.");
                    }

                    var placeholderName = value[(index + 1)..closingIndex].Trim();
                    if (string.IsNullOrWhiteSpace(placeholderName) ||
                        placeholderName.Contains('{', StringComparison.Ordinal) ||
                        placeholderName.Contains('}', StringComparison.Ordinal))
                    {
                        throw CreateTemplateFormatException(
                            key,
                            localePath,
                            "Placeholders must use a non-empty named brace token.");
                    }

                    if (seen.Add(placeholderName))
                    {
                        placeholders.Add(placeholderName);
                    }

                    index = closingIndex;
                    break;
                case '}':
                    if (index + 1 < value.Length && value[index + 1] == '}')
                    {
                        index++;
                        continue;
                    }

                    throw CreateTemplateFormatException(key, localePath, "Unmatched '}' brace.");
            }
        }

        return placeholders;
    }

    private static InvalidDataException CreateTemplateFormatException(string key, string localePath, string message)
    {
        return new InvalidDataException($"Locale file '{localePath}' contains an invalid template for key '{key}': {message}");
    }

    public static string GetPlaceholderSentinelValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return NormalizeKeyOrThrow(key) + PlaceholderSuffix;
    }

    private static void SetLocaleLeaf(YamlMappingNode root, string key, string value, string localePath)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }

        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (!TryGetChild(current, segment, out var childNode))
            {
                var newMapping = new YamlMappingNode();
                current.Children.Add(new YamlScalarNode(segment), newMapping);
                current = newMapping;
                continue;
            }

            if (childNode is not YamlMappingNode childMapping)
            {
                throw new InvalidDataException(
                    $"Locale file '{localePath}' contains a scalar at '{string.Join('.', segments[..(index + 1)])}', so key '{key}' cannot be assigned.");
            }

            current = childMapping;
        }

        SetChild(
            current,
            segments[^1],
            new YamlScalarNode(value) { Style = ScalarStyle.DoubleQuoted });
    }

    private static void SetSchemaLeaf(YamlMappingNode root, string key, IReadOnlyList<string> placeholders)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (!TryGetChild(current, segment, out var childNode))
            {
                var newMapping = new YamlMappingNode();
                current.Children.Add(new YamlScalarNode(segment), newMapping);
                current = newMapping;
                continue;
            }

            if (childNode is not YamlMappingNode childMapping)
            {
                throw new InvalidDataException(
                    $"Schema file '{key}' cannot assign child key under scalar/sequence node '{string.Join('.', segments[..(index + 1)])}'.");
            }

            current = childMapping;
        }

        var placeholderSequence = new YamlSequenceNode(
            placeholders.Select(placeholder => new YamlScalarNode(placeholder)));
        SetChild(current, segments[^1], placeholderSequence);
    }

    private void EnsureLocaleSentinelsForSchemaKey(string key)
    {
        var manifestLocales = ReadManifestLocales();
        var sentinelValue = GetPlaceholderSentinelValue(key);
        foreach (var locale in manifestLocales.Keys)
        {
            var localePath = GetLocalePath(locale);
            var root = File.Exists(localePath)
                ? LoadRequiredMapping(localePath, "locale")
                : new YamlMappingNode();

            var localeValues = new Dictionary<string, string>(StringComparer.Ordinal);
            FlattenLocale(root, prefix: null, localeValues, localePath);
            if (localeValues.ContainsKey(key))
            {
                continue;
            }

            SetLocaleLeaf(root, key, sentinelValue, localePath);
            WriteYaml(localePath, root);
        }
    }

    private void RemoveLocaleKeyFromDeclaredLocales(string key)
    {
        IReadOnlyDictionary<string, string> manifestLocales;
        try
        {
            manifestLocales = ReadManifestLocales();
        }
        catch
        {
            return;
        }

        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var locale in manifestLocales.Keys)
        {
            var localePath = GetLocalePath(locale);
            if (!File.Exists(localePath))
            {
                continue;
            }

            var root = LoadRequiredMapping(localePath, "locale");
            if (!RemovePath(root, segments, index: 0))
            {
                continue;
            }

            WriteYaml(localePath, root);
        }
    }

    private static bool RemovePath(YamlMappingNode mapping, IReadOnlyList<string> segments, int index)
    {
        if (index >= segments.Count)
        {
            return false;
        }

        if (!TryFindChild(mapping, segments[index], out var keyNode, out var value))
        {
            return false;
        }

        if (index == segments.Count - 1)
        {
            mapping.Children.Remove(keyNode);
            return true;
        }

        if (value is not YamlMappingNode childMapping)
        {
            return false;
        }

        var removed = RemovePath(childMapping, segments, index + 1);
        if (removed && childMapping.Children.Count == 0)
        {
            mapping.Children.Remove(keyNode);
        }

        return removed;
    }

    private static void SetChild(YamlMappingNode mapping, string key, YamlNode value)
    {
        if (TryFindChild(mapping, key, out var keyNode, out _))
        {
            mapping.Children[keyNode] = value;
            return;
        }

        mapping.Children.Add(new YamlScalarNode(key), value);
    }

    private static bool TryGetChild(YamlMappingNode mapping, string key, [NotNullWhen(true)] out YamlNode? value)
    {
        if (TryFindChild(mapping, key, out _, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryFindChild(
        YamlMappingNode mapping,
        string key,
        [NotNullWhen(true)] out YamlScalarNode? keyNode,
        [NotNullWhen(true)] out YamlNode? value)
    {
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode scalarKey ||
                !string.Equals(scalarKey.Value, key, StringComparison.Ordinal))
            {
                continue;
            }

            keyNode = scalarKey;
            value = entry.Value;
            return true;
        }

        keyNode = null;
        value = null;
        return false;
    }

    private static string ReadRequiredScalar(YamlNode node, string path, string message)
    {
        if (node is not YamlScalarNode scalarNode || string.IsNullOrWhiteSpace(scalarNode.Value))
        {
            throw new InvalidDataException($"{message} Path: '{path}'.");
        }

        return scalarNode.Value.Trim();
    }

    private static YamlMappingNode LoadRequiredMapping(string path, string fileKind)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The {fileKind} file '{path}' was not found.", path);
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            return new YamlMappingNode();
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException($"YAML file '{path}' must use a mapping root.");
        }

        return root;
    }

    private static void WriteYaml(string path, YamlMappingNode root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Serializer.Serialize(writer, ConvertNode(root));
    }

    private static object? ConvertNode(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode mappingNode => ConvertMapping(mappingNode),
            YamlSequenceNode sequenceNode => sequenceNode.Children.Select(ConvertNode).ToList(),
            YamlScalarNode scalarNode => scalarNode.Value ?? string.Empty,
            _ => throw new InvalidDataException($"Unsupported YAML node type '{node.NodeType}'.")
        };
    }

    private static Dictionary<string, object?> ConvertMapping(YamlMappingNode mappingNode)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in mappingNode.Children)
        {
            result[ReadRequiredScalar(entry.Key, "<memory>", "Mapping keys must be scalar values.")] = ConvertNode(entry.Value);
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizePlaceholders(IReadOnlyList<string> placeholders)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placeholder in placeholders)
        {
            if (string.IsNullOrWhiteSpace(placeholder))
            {
                throw new InvalidDataException("Schema placeholder entries must be non-empty scalar values.");
            }

            var trimmed = placeholder.Trim();
            if (!seen.Add(trimmed))
            {
                throw new InvalidDataException($"Schema placeholder '{trimmed}' is duplicated.");
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static bool IsInTreePrefix(string key, string prefix)
    {
        return string.Equals(key, prefix, StringComparison.Ordinal) ||
               key.StartsWith(prefix + ".", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> RenderTree<TValue>(
        IReadOnlyDictionary<string, TValue> entries,
        Func<TValue, string> leafFormatter)
    {
        var root = new DisplayTreeNode(string.Empty);
        foreach (var entry in entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var current = root;
            foreach (var segment in entry.Key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new DisplayTreeNode(segment);
                    current.Children[segment] = child;
                }

                current = child;
            }

            current.LeafSuffix = leafFormatter(entry.Value);
        }

        var lines = new List<string>();
        foreach (var child in root.Children.Values.OrderBy(node => node.Name, StringComparer.Ordinal))
        {
            AppendTreeLines(child, indent: 0, lines);
        }

        return lines;
    }

    private static void AppendTreeLines(DisplayTreeNode node, int indent, ICollection<string> lines)
    {
        lines.Add(new string(' ', indent * 2) + node.Name + node.LeafSuffix);
        foreach (var child in node.Children.Values.OrderBy(child => child.Name, StringComparer.Ordinal))
        {
            AppendTreeLines(child, indent + 1, lines);
        }
    }

    private static string QuoteForDisplay(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
    }

    private string NormalizeLocaleOrThrow(string locale)
    {
        return I18nLocaleUtility.NormalizeLocale(locale)
               ?? throw new ArgumentException($"Locale '{locale}' is invalid.", nameof(locale));
    }

    private static string NormalizeKeyOrThrow(string key)
    {
        var normalizedKey = key.Trim();
        if (normalizedKey.Length == 0 ||
            normalizedKey.StartsWith(".", StringComparison.Ordinal) ||
            normalizedKey.EndsWith(".", StringComparison.Ordinal) ||
            normalizedKey.Split('.', StringSplitOptions.None).Any(segment => string.IsNullOrWhiteSpace(segment)))
        {
            throw new ArgumentException($"Key '{key}' is invalid.", nameof(key));
        }

        return normalizedKey;
    }

    private string GetLocalePath(string locale)
    {
        return Path.Combine(_localeDirectory, locale + ".yaml");
    }

    private sealed class DisplayTreeNode(string name)
    {
        public string Name { get; } = name;

        public SortedDictionary<string, DisplayTreeNode> Children { get; } = new(StringComparer.Ordinal);

        public string LeafSuffix { get; set; } = string.Empty;
    }
}

public sealed record I18nValidationIssue(
    I18nValidationSeverity Severity,
    string Code,
    string Message,
    string? Locale = null,
    string? Key = null,
    string? FilePath = null);

public sealed class I18nValidationReport
{
    public I18nValidationReport(IReadOnlyList<string> locales, IReadOnlyList<I18nValidationIssue> issues)
    {
        Locales = locales;
        Issues = issues;
    }

    public IReadOnlyList<string> Locales { get; }

    public IReadOnlyList<I18nValidationIssue> Issues { get; }

    public IReadOnlyList<I18nValidationIssue> Errors => Issues.Where(issue => issue.Severity == I18nValidationSeverity.Error).ToArray();

    public IReadOnlyList<I18nValidationIssue> Warnings => Issues.Where(issue => issue.Severity == I18nValidationSeverity.Warning).ToArray();

    public bool HasErrors => Errors.Count > 0;

    public bool HasWarnings => Warnings.Count > 0;

    public bool IsValid => !HasErrors;
}

public enum I18nValidationSeverity
{
    Warning,
    Error
}

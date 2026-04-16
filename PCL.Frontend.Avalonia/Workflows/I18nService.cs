using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Text;
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PCL.Frontend.Avalonia.Workflows;

internal interface II18nService
{
    string Locale { get; }

    IReadOnlyList<string> AvailableLocales { get; }

    string T(string key);

    string T(string key, IReadOnlyDictionary<string, object?> args);

    string T(I18nText text);

    bool SetLocale(string locale);

    bool ReloadLocaleFromSettings();

    bool ReloadCurrentLocale();

    event Action? Changed;
}

internal sealed class I18nService : II18nService, IDisposable
{
    private const string DefaultLocale = "en-US";
    private const int SchemaPreviewLineLimit = 24;

    private static readonly IReadOnlyDictionary<string, object?> EmptyArgs =
        new Dictionary<string, object?>(0, StringComparer.Ordinal);

    private readonly string _localeDirectory;
    private readonly string _schemaPath;
    private readonly II18nSettingsManager _settingsManager;
    private readonly string _fallbackLocale;
    private readonly IReadOnlyList<string> _availableLocales;
    private readonly ConcurrentDictionary<string, I18nLocaleSnapshot> _snapshotCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _missingKeyWarnings = new(StringComparer.Ordinal);
    private readonly I18nSchemaSnapshot _schemaSnapshot;
    private I18nLocaleSnapshot _currentSnapshot;
    private bool _disposed;

    public I18nService(II18nSettingsManager settingsManager)
        : this(Path.Combine(AppContext.BaseDirectory, "Locales"), settingsManager)
    {
    }

    internal I18nService(
        string localeDirectory,
        II18nSettingsManager settingsManager,
        string fallbackLocale = DefaultLocale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localeDirectory);
        ArgumentNullException.ThrowIfNull(settingsManager);

        _localeDirectory = localeDirectory;
        _schemaPath = Path.Combine(_localeDirectory, "Meta", "schema.yaml");
        _settingsManager = settingsManager;
        _fallbackLocale = NormalizeLocale(fallbackLocale)
                          ?? throw new ArgumentException("Fallback locale is invalid.", nameof(fallbackLocale));
        _availableLocales = DiscoverAvailableLocales(_localeDirectory, _fallbackLocale);
        _schemaSnapshot = LoadSchemaSnapshot(_schemaPath);
        _currentSnapshot = LoadInitialSnapshot(settingsManager.Locale);
        _settingsManager.LocaleChanged += OnLocaleChanged;
    }

    public event Action? Changed;

    public string Locale => Volatile.Read(ref _currentSnapshot).Locale;

    public IReadOnlyList<string> AvailableLocales => _availableLocales;

    public string T(string key)
    {
        return T(key, EmptyArgs);
    }

    public string T(I18nText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return T(
            text.Key,
            text.Arguments?.ToDictionary(
                argument => argument.Name,
                argument => argument.GetValue(),
                StringComparer.Ordinal) ?? EmptyArgs);
    }

    public string T(string key, IReadOnlyDictionary<string, object?> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(args);

        var snapshot = Volatile.Read(ref _currentSnapshot);
        if (snapshot.Messages.TryGetValue(key, out var template))
        {
            return Format(template, args);
        }

        WarnMissingKey(snapshot.Locale, key);
        return key;
    }

    public bool SetLocale(string locale)
    {
        ThrowIfDisposed();

        var normalizedLocale = NormalizeLocale(locale);
        return normalizedLocale is not null &&
               TryGetOrLoadSnapshot(normalizedLocale, useCache: true, out _) &&
               _settingsManager.SetLocale(normalizedLocale);
    }

    public bool ReloadLocaleFromSettings()
    {
        ThrowIfDisposed();

        return _settingsManager.ReloadLocale();
    }

    public bool ReloadCurrentLocale()
    {
        ThrowIfDisposed();

        var currentSnapshot = Volatile.Read(ref _currentSnapshot);
        if (!TryLoadLocaleSnapshot(currentSnapshot.Locale, out var reloadedSnapshot))
        {
            return false;
        }

        _snapshotCache[currentSnapshot.Locale] = reloadedSnapshot;
        Volatile.Write(ref _currentSnapshot, reloadedSnapshot);
        Changed?.Invoke();
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _settingsManager.LocaleChanged -= OnLocaleChanged;
        _disposed = true;
    }

    private void OnLocaleChanged(string locale)
    {
        if (_disposed)
        {
            return;
        }

        if (TryApplyLocale(locale, useCache: true))
        {
            Changed?.Invoke();
            return;
        }

        if (!string.Equals(locale, _fallbackLocale, StringComparison.Ordinal))
        {
            _settingsManager.SetLocale(_fallbackLocale);
        }
    }

    private static IReadOnlyList<string> DiscoverAvailableLocales(string localeDirectory, string fallbackLocale)
    {
        var discoveredLocales = Directory.EnumerateFiles(localeDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Select(path => NormalizeLocale(Path.GetFileNameWithoutExtension(path)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(locale => locale, StringComparer.Ordinal)
            .ToList();

        if (!discoveredLocales.Contains(fallbackLocale, StringComparer.Ordinal))
        {
            discoveredLocales.Insert(0, fallbackLocale);
        }
        else
        {
            discoveredLocales.Sort((left, right) =>
                left == fallbackLocale ? -1 :
                right == fallbackLocale ? 1 :
                StringComparer.Ordinal.Compare(left, right));
        }

        return discoveredLocales;
    }

    private I18nLocaleSnapshot LoadInitialSnapshot(string requestedLocale)
    {
        if (TryGetOrLoadSnapshot(requestedLocale, useCache: true, out var snapshot))
        {
            return snapshot;
        }

        if (!string.Equals(requestedLocale, _fallbackLocale, StringComparison.Ordinal) &&
            TryGetOrLoadSnapshot(_fallbackLocale, useCache: true, out snapshot))
        {
            return snapshot;
        }

        return I18nLocaleSnapshot.Empty(_fallbackLocale);
    }

    private bool TryApplyLocale(string locale, bool useCache)
    {
        if (!TryGetOrLoadSnapshot(locale, useCache, out var snapshot))
        {
            return false;
        }

        Volatile.Write(ref _currentSnapshot, snapshot);
        return true;
    }

    private bool TryGetOrLoadSnapshot(
        string locale,
        bool useCache,
        out I18nLocaleSnapshot snapshot)
    {
        snapshot = default!;
        var normalizedLocale = NormalizeLocale(locale);
        if (normalizedLocale is null)
        {
            return false;
        }

        if (useCache && _snapshotCache.TryGetValue(normalizedLocale, out var cachedSnapshot))
        {
            snapshot = cachedSnapshot;
            return true;
        }

        if (!TryLoadLocaleSnapshot(normalizedLocale, out snapshot))
        {
            return false;
        }

        _snapshotCache[normalizedLocale] = snapshot;
        return true;
    }

    private bool TryLoadLocaleSnapshot(string locale, out I18nLocaleSnapshot snapshot)
    {
        snapshot = default!;
        var localePath = Path.Combine(_localeDirectory, locale + ".yaml");
        if (!File.Exists(localePath))
        {
            return false;
        }

        using var stream = new FileStream(localePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            snapshot = I18nLocaleSnapshot.Empty(locale);
            return true;
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode rootNode)
        {
            throw new InvalidDataException($"Locale file '{localePath}' must use a mapping root.");
        }

        var messages = new Dictionary<string, I18nMessageTemplate>(StringComparer.Ordinal);
        FlattenMappings(rootNode, prefix: null, messages, localePath);
        snapshot = new I18nLocaleSnapshot(locale, messages.ToFrozenDictionary(StringComparer.Ordinal));
        return true;
    }

    private static void FlattenMappings(
        YamlMappingNode mappingNode,
        string? prefix,
        IDictionary<string, I18nMessageTemplate> messages,
        string localePath)
    {
        foreach (var entry in mappingNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                throw new InvalidDataException($"Locale file '{localePath}' contains an empty key.");
            }

            var key = prefix is null ? keyNode.Value : prefix + "." + keyNode.Value;
            switch (entry.Value)
            {
                case YamlMappingNode childMapping:
                    FlattenMappings(childMapping, key, messages, localePath);
                    break;
                case YamlScalarNode scalarNode:
                    messages[key] = ParseTemplate(key, scalarNode.Value ?? string.Empty, localePath);
                    break;
                default:
                    throw new InvalidDataException(
                        $"Locale file '{localePath}' contains a non-scalar leaf for key '{key}'.");
            }
        }
    }

    private void WarnMissingKey(string locale, string key)
    {
        if (!_missingKeyWarnings.TryAdd(key, 0))
        {
            return;
        }

        var schemaPreview = _schemaSnapshot.RenderExpectedSchema(key, maxLines: 12);
        var message = _schemaSnapshot.TryGetPlaceholders(key, out var placeholders)
            ? $"Missing translation key '{key}' for locale '{locale}'. Schema defines placeholders [{string.Join(", ", placeholders)}]."
            : $"Missing translation key '{key}' for locale '{locale}'. The key does not exist in schema.";
        if (!string.IsNullOrWhiteSpace(schemaPreview))
        {
            message += $"{Environment.NewLine}Expected schema near:{Environment.NewLine}{schemaPreview}";
        }

        LogWrapper.Warn("I18n", message);
    }

    private static I18nMessageTemplate ParseTemplate(string key, string value, string localePath)
    {
        var tokens = new List<I18nToken>();
        var text = new StringBuilder();

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            switch (current)
            {
                case '{':
                    if (index + 1 < value.Length && value[index + 1] == '{')
                    {
                        text.Append('{');
                        index++;
                        continue;
                    }

                    FlushText(tokens, text);
                    var closingIndex = value.IndexOf('}', index + 1);
                    if (closingIndex < 0)
                    {
                        throw CreateTemplateFormatException(key, localePath, "Unmatched '{' brace.");
                    }

                    var argName = value[(index + 1)..closingIndex].Trim();
                    if (string.IsNullOrWhiteSpace(argName) ||
                        argName.Contains('{', StringComparison.Ordinal) ||
                        argName.Contains('}', StringComparison.Ordinal))
                    {
                        throw CreateTemplateFormatException(
                            key,
                            localePath,
                            "Placeholders must use a non-empty named brace token.");
                    }

                    tokens.Add(new I18nToken(I18nTokenKind.Arg, argName));
                    index = closingIndex;
                    break;
                case '}':
                    if (index + 1 < value.Length && value[index + 1] == '}')
                    {
                        text.Append('}');
                        index++;
                        continue;
                    }

                    throw CreateTemplateFormatException(key, localePath, "Unmatched '}' brace.");
                default:
                    text.Append(current);
                    break;
            }
        }

        FlushText(tokens, text);
        return new I18nMessageTemplate(value, [.. tokens]);
    }

    private static void FlushText(ICollection<I18nToken> tokens, StringBuilder text)
    {
        if (text.Length == 0)
        {
            return;
        }

        tokens.Add(new I18nToken(I18nTokenKind.Text, text.ToString()));
        text.Clear();
    }

    private static InvalidDataException CreateTemplateFormatException(string key, string localePath, string message)
    {
        return new InvalidDataException($"Locale file '{localePath}' contains an invalid template for key '{key}': {message}");
    }

    private static string Format(I18nMessageTemplate template, IReadOnlyDictionary<string, object?> args)
    {
        if (template.Tokens.Count == 0)
        {
            return template.RawValue;
        }

        var builder = new StringBuilder(template.RawValue.Length + (args.Count * 8));
        foreach (var token in template.Tokens)
        {
            if (token.Kind == I18nTokenKind.Text)
            {
                builder.Append(token.Value);
                continue;
            }

            if (!args.TryGetValue(token.Value, out var argValue))
            {
                builder.Append('{');
                builder.Append(token.Value);
                builder.Append('}');
                continue;
            }

            builder.Append(argValue);
        }

        return builder.ToString();
    }

    private static string? NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var rawSegments = locale
            .Trim()
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rawSegments.Length == 0 || rawSegments.Any(segment => !segment.All(char.IsLetterOrDigit)))
        {
            return null;
        }

        var builder = new StringBuilder(locale.Length);
        for (var i = 0; i < rawSegments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('-');
            }

            var segment = rawSegments[i];
            if (i == 0)
            {
                builder.Append(segment.ToLowerInvariant());
                continue;
            }

            if (segment.Length == 4 && segment.All(char.IsLetter))
            {
                builder.Append(char.ToUpperInvariant(segment[0]));
                builder.Append(segment[1..].ToLowerInvariant());
                continue;
            }

            if ((segment.Length == 2 || segment.Length == 3) && segment.All(char.IsLetter))
            {
                builder.Append(segment.ToUpperInvariant());
                continue;
            }

            builder.Append(segment);
        }

        return builder.ToString();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(I18nService));
        }
    }

    private enum I18nTokenKind
    {
        Text,
        Arg
    }

    private readonly record struct I18nToken(I18nTokenKind Kind, string Value);

    private sealed record I18nMessageTemplate(string RawValue, IReadOnlyList<I18nToken> Tokens);

    private static I18nSchemaSnapshot LoadSchemaSnapshot(string schemaPath)
    {
        if (!File.Exists(schemaPath))
        {
            return I18nSchemaSnapshot.Empty();
        }

        try
        {
            using var stream = new FileStream(schemaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
            {
                return I18nSchemaSnapshot.Empty();
            }

            if (yaml.Documents[0].RootNode is not YamlMappingNode rootNode)
            {
                throw new InvalidDataException($"Schema file '{schemaPath}' must use a mapping root.");
            }

            var entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
            FlattenSchema(rootNode, prefix: null, entries, schemaPath);
            return new I18nSchemaSnapshot(entries.ToFrozenDictionary(StringComparer.Ordinal));
        }
        catch (Exception ex) when (ex is InvalidDataException or YamlException)
        {
            LogWrapper.Warn(ex, "I18n", $"Failed to load schema '{schemaPath}' for runtime key diagnostics.");
            return I18nSchemaSnapshot.Empty();
        }
    }

    private static void FlattenSchema(
        YamlMappingNode mappingNode,
        string? prefix,
        IDictionary<string, string[]> entries,
        string schemaPath)
    {
        foreach (var entry in mappingNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                throw new InvalidDataException($"Schema file '{schemaPath}' contains an empty key.");
            }

            var key = prefix is null ? keyNode.Value : prefix + "." + keyNode.Value;
            switch (entry.Value)
            {
                case YamlMappingNode childMapping:
                    FlattenSchema(childMapping, key, entries, schemaPath);
                    break;
                case YamlSequenceNode placeholderList:
                    entries[key] = ReadSchemaPlaceholders(placeholderList, schemaPath, key);
                    break;
                default:
                    throw new InvalidDataException(
                        $"Schema file '{schemaPath}' contains a non-mapping, non-sequence node for key '{key}'.");
            }
        }
    }

    private static string[] ReadSchemaPlaceholders(YamlSequenceNode placeholderList, string schemaPath, string key)
    {
        var placeholders = new List<string>(placeholderList.Children.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var child in placeholderList.Children)
        {
            if (child is not YamlScalarNode scalarNode || string.IsNullOrWhiteSpace(scalarNode.Value))
            {
                throw new InvalidDataException($"Schema file '{schemaPath}' contains an invalid placeholder for key '{key}'.");
            }

            var placeholder = scalarNode.Value.Trim();
            if (!seen.Add(placeholder))
            {
                throw new InvalidDataException(
                    $"Schema file '{schemaPath}' contains duplicate placeholder '{placeholder}' for key '{key}'.");
            }

            placeholders.Add(placeholder);
        }

        return [.. placeholders];
    }

    private sealed record I18nSchemaSnapshot(FrozenDictionary<string, string[]> Entries)
    {
        public static I18nSchemaSnapshot Empty()
        {
            return new I18nSchemaSnapshot(
                new Dictionary<string, string[]>(0, StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal));
        }

        public bool TryGetPlaceholders(string key, out IReadOnlyList<string> placeholders)
        {
            if (Entries.TryGetValue(key, out var values))
            {
                placeholders = values;
                return true;
            }

            placeholders = [];
            return false;
        }

        public string RenderExpectedSchema(string key, int maxLines)
        {
            if (Entries.Count == 0)
            {
                return string.Empty;
            }

            var prefix = FindNearestPrefix(key);
            var lines = RenderTree(prefix);
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            if (lines.Count <= maxLines)
            {
                return string.Join(Environment.NewLine, lines);
            }

            return string.Join(
                Environment.NewLine,
                lines.Take(maxLines).Concat([$"... ({lines.Count - maxLines} more lines)"]));
        }

        private string? FindNearestPrefix(string key)
        {
            var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var count = segments.Length; count >= 1; count--)
            {
                var candidate = string.Join(".", segments[..count]);
                if (Entries.ContainsKey(candidate) ||
                    Entries.Keys.Any(entry => entry.StartsWith(candidate + ".", StringComparison.Ordinal)))
                {
                    return candidate;
                }
            }

            return segments.Length > 0 &&
                   Entries.Keys.Any(entry => entry.StartsWith(segments[0] + ".", StringComparison.Ordinal))
                ? segments[0]
                : null;
        }

        private IReadOnlyList<string> RenderTree(string? prefix)
        {
            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in Entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (prefix is not null &&
                    !string.Equals(entry.Key, prefix, StringComparison.Ordinal) &&
                    !entry.Key.StartsWith(prefix + ".", StringComparison.Ordinal))
                {
                    continue;
                }

                var segments = entry.Key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (var index = 0; index < segments.Length - 1; index++)
                {
                    var branchPath = string.Join(".", segments[..(index + 1)]);
                    if (seen.Add(branchPath))
                    {
                        lines.Add(new string(' ', index * 2) + segments[index]);
                    }
                }

                var placeholders = entry.Value.Length == 0 ? "[]" : $"[{string.Join(", ", entry.Value)}]";
                lines.Add(new string(' ', (segments.Length - 1) * 2) + segments[^1] + " " + placeholders);
            }

            return lines;
        }
    }

    private sealed record I18nLocaleSnapshot(
        string Locale,
        FrozenDictionary<string, I18nMessageTemplate> Messages)
    {
        public static I18nLocaleSnapshot Empty(string locale)
        {
            return new I18nLocaleSnapshot(
                locale,
                new Dictionary<string, I18nMessageTemplate>(0, StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}

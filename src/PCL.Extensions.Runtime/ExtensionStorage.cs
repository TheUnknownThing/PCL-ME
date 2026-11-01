using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

public sealed class ExtensionSettings : IExtensionSettings
{
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<string, JsonElement> _values;

    public ExtensionSettings(string path)
    {
        _path = path;
        _values = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
              ?? throw new InvalidDataException($"Invalid extension settings: {path}")
            : new(StringComparer.Ordinal);
    }

    public event Action<string>? Changed;

    public T Get<T>(string key, T defaultValue)
    {
        lock (_gate) return _values.TryGetValue(key, out var value) ? value.Deserialize<T>()! : defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var json = JsonSerializer.SerializeToElement(value);
        lock (_gate)
        {
            if (_values.TryGetValue(key, out var previous) && previous.GetRawText() == json.GetRawText()) return;
            var next = new Dictionary<string, JsonElement>(_values, StringComparer.Ordinal) { [key] = json };
            WriteJson(_path, next);
            _values = next;
        }
        Changed?.Invoke(key);
    }

    internal static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed class ExtensionLocalizer(string directory, string defaultLocale, Func<string> locale) : IExtensionLocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = LoadCatalogs(directory);
    public string Locale => locale();
    public string Get(string key, string? fallback = null)
    {
        if (_catalogs.TryGetValue(Locale, out var selected) && selected.TryGetValue(key, out var value)) return value;
        return _catalogs.TryGetValue(defaultLocale, out var defaults) && defaults.TryGetValue(key, out value) ? value : fallback ?? key;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs(string directory)
    {
        var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(directory, "Locales");
        if (!Directory.Exists(path)) return catalogs;
        foreach (var file in Directory.EnumerateFiles(path, "*.json"))
            catalogs.Add(Path.GetFileNameWithoutExtension(file), JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                ?? throw new InvalidDataException($"Invalid extension locale: {file}"));
        return catalogs;
    }
}

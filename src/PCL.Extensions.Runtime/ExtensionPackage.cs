using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

public static partial class ExtensionPackage
{
    public const int ApiVersion = 1;
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)+$")]
    private static partial Regex IdPattern();

    public static ExtensionManifest ReadManifest(string directory, bool checkPlatform = true)
    {
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(File.ReadAllText(Path.Combine(directory, "extension.json")), JsonOptions)
                       ?? throw new InvalidDataException("The extension manifest is empty.");
        Validate(manifest, directory, checkPlatform);
        return manifest;
    }

    public static void Validate(ExtensionManifest manifest, string directory, bool checkPlatform = true)
    {
        if (!IdPattern().IsMatch(manifest.Id)) throw new InvalidDataException("Extension IDs must be lowercase namespaced identifiers.");
        if (string.IsNullOrWhiteSpace(manifest.Name) || !Version.TryParse(manifest.Version, out _))
            throw new InvalidDataException("An extension needs a name and a numeric version.");
        if (manifest.ApiVersion != ApiVersion) throw new NotSupportedException($"Extension API {manifest.ApiVersion} is not supported.");
        if (manifest.EntryAssembly is null || manifest.EntryType is null)
            throw new InvalidDataException("An extension needs entryAssembly and entryType.");
        var entry = ResolvePackagePath(directory, manifest.EntryAssembly);
        if (!File.Exists(entry)) throw new FileNotFoundException("The extension entry assembly does not exist.", entry);
        var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        if (manifest.Platforms.Any(value => value is not ("windows" or "linux" or "macos")))
            throw new InvalidDataException("Platforms must be windows, linux or macos.");
        if (checkPlatform && manifest.Platforms.Length > 0 && !manifest.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException($"This extension does not support {platform}.");
        if (checkPlatform && manifest.Architectures.Length > 0 && !manifest.Architectures.Contains(RuntimeInformation.ProcessArchitecture.ToString(), StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException("This extension does not support the launcher architecture.");
        if (manifest.Commands.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Commands.Length ||
            manifest.Commands.Any(command => !command.Id.StartsWith(manifest.Id + ".", StringComparison.Ordinal)))
            throw new InvalidDataException("Commands must have unique IDs within the extension namespace.");
        if (manifest.Settings.Select(setting => setting.Key).Distinct(StringComparer.Ordinal).Count() != manifest.Settings.Length)
            throw new InvalidDataException("Setting keys must be unique.");
        foreach (var setting in manifest.Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key) || string.IsNullOrWhiteSpace(setting.Title) || !Enum.IsDefined(setting.Kind))
                throw new InvalidDataException("Settings need a key, title and supported kind.");
            if (setting.Minimum > setting.Maximum) throw new InvalidDataException("The setting minimum cannot exceed its maximum.");
            if (setting.Kind == ExtensionSettingKind.Boolean && !bool.TryParse(setting.DefaultValue, out _))
                throw new InvalidDataException($"Invalid Boolean default: {setting.Key}");
            if (setting.Kind == ExtensionSettingKind.Number &&
                (!double.TryParse(setting.DefaultValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                 || !double.IsFinite(number) || number < setting.Minimum || number > setting.Maximum))
                throw new InvalidDataException($"Invalid number default: {setting.Key}");
            if (setting.Kind == ExtensionSettingKind.Choice &&
                (setting.Choices.Length == 0 || !setting.Choices.Any(choice => choice.Value == setting.DefaultValue)
                 || setting.Choices.Select(choice => choice.Value).Distinct(StringComparer.Ordinal).Count() != setting.Choices.Length))
                throw new InvalidDataException($"Invalid choices: {setting.Key}");
        }
        if (manifest.Dependencies.Any(dependency => dependency.Id == manifest.Id || !IdPattern().IsMatch(dependency.Id) || !Version.TryParse(dependency.MinimumVersion, out _)))
            throw new InvalidDataException("Invalid extension dependency.");
        var locales = Path.Combine(directory, "Locales");
        if (Directory.Exists(locales))
        {
            var catalogs = Directory.EnumerateFiles(locales, "*.json").ToDictionary(path => Path.GetFileNameWithoutExtension(path)!,
                path => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                        ?? throw new InvalidDataException("Invalid extension locale."), StringComparer.OrdinalIgnoreCase);
            if (!catalogs.TryGetValue(manifest.DefaultLocale, out var defaults)) throw new InvalidDataException("The default locale catalog is missing.");
            foreach (var key in manifest.Commands.Select(command => command.TitleKey).Concat(manifest.Settings.Select(setting => setting.TitleKey))
                         .Concat(manifest.Settings.SelectMany(setting => setting.Choices.Select(choice => choice.TitleKey))).OfType<string>())
                if (!defaults.ContainsKey(key)) throw new InvalidDataException($"Missing default translation: {key}");
        }
    }

    public static string ResolvePackagePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(segment => segment is ".." or "." or ""))
            throw new InvalidDataException("Extension paths must stay inside the package.");
        var prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Extension paths must stay inside the package.");
        return path;
    }

    public static ExtensionManifest Extract(string archivePath, string destination, bool checkPlatform = true)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 4096 || archive.Entries.Sum(entry => entry.Length) > 256L * 1024 * 1024)
            throw new InvalidDataException("The extension package exceeds the size limit.");
        Directory.CreateDirectory(destination);
        foreach (var entry in archive.Entries)
        {
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("Extension packages cannot contain symbolic links.");
            var name = entry.FullName.TrimEnd('/');
            if (name.Length == 0) continue;
            var target = ResolvePackagePath(destination, name);
            if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }
        }
        return ReadManifest(destination, checkPlatform);
    }
}

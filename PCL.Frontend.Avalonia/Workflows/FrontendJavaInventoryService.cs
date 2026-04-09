using System.Text.Json;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendJavaInventoryService
{
    private static readonly IJavaParser JavaParser = new CompositeJavaParser(
        new CommandJavaParser(SystemJavaRuntimeEnvironment.Current, new ProcessCommandRunner()));

    public static IReadOnlyList<JavaStorageItem> ParseStorageItems(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<JavaStorageItem>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var executablePath = GetString(item, "Path")
                                     ?? GetNestedString(item, "Installation", "JavaExePath");
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var normalizedPath = NormalizeExecutablePath(executablePath);
                if (string.IsNullOrWhiteSpace(normalizedPath) || !seenPaths.Add(normalizedPath))
                {
                    continue;
                }

                result.Add(new JavaStorageItem
                {
                    Path = normalizedPath,
                    IsEnable = GetBoolean(item, "IsEnable")
                               ?? GetBoolean(item, "IsEnabled")
                               ?? true,
                    Source = GetEnum<JavaSource>(item, "Source")
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    public static IReadOnlyList<FrontendStoredJavaRuntime> ParseStoredJavaRuntimes(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<FrontendStoredJavaRuntime>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var executablePath = GetString(item, "Path")
                                     ?? GetNestedString(item, "Installation", "JavaExePath");
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var normalizedPath = NormalizeExecutablePath(executablePath);
                if (string.IsNullOrWhiteSpace(normalizedPath) || !seenPaths.Add(normalizedPath))
                {
                    continue;
                }

                var installation = TryParseInstallation(normalizedPath);
                var storedVersionText = GetNestedString(item, "Installation", "Version");
                var storedVersion = TryParseVersion(storedVersionText);
                var parsedVersion = installation?.Version ?? storedVersion;
                var majorVersion = installation?.MajorVersion
                                   ?? GetNestedInt(item, "Installation", "MajorVersion")
                                   ?? GetMajorVersion(parsedVersion);
                var is64Bit = installation?.Is64Bit ?? GetNestedBoolean(item, "Installation", "Is64Bit");
                var isJre = installation?.IsJre;
                var brand = installation?.Brand;
                var architecture = installation?.Architecture;
                var displayName = !string.IsNullOrWhiteSpace(storedVersionText)
                    ? storedVersionText
                    : installation?.Version.ToString()
                      ?? Path.GetFileName(Path.GetDirectoryName(normalizedPath))
                      ?? "Java";
                var isEnabled = GetBoolean(item, "IsEnable")
                                ?? GetBoolean(item, "IsEnabled")
                                ?? true;

                result.Add(new FrontendStoredJavaRuntime(
                    normalizedPath,
                    displayName,
                    parsedVersion,
                    majorVersion,
                    isEnabled,
                    is64Bit,
                    isJre,
                    brand,
                    architecture));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    public static FrontendStoredJavaRuntime? TryResolveRuntime(string executablePath, bool isEnabled = true, string? fallbackDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var normalizedPath = NormalizeExecutablePath(executablePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        var installation = TryParseInstallation(normalizedPath);
        var parsedVersion = installation?.Version;
        var displayName = !string.IsNullOrWhiteSpace(fallbackDisplayName)
            ? fallbackDisplayName
            : installation?.Version.ToString()
              ?? Path.GetFileName(Path.GetDirectoryName(normalizedPath))
              ?? "Java";

        return new FrontendStoredJavaRuntime(
            normalizedPath,
            displayName,
            parsedVersion,
            installation?.MajorVersion,
            isEnabled,
            installation?.Is64Bit,
            installation?.IsJre,
            installation?.Brand,
            installation?.Architecture);
    }

    private static JavaInstallation? TryParseInstallation(string executablePath)
    {
        try
        {
            return JavaParser.Parse(executablePath);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeExecutablePath(string executablePath)
    {
        try
        {
            return Path.GetFullPath(executablePath.Trim());
        }
        catch
        {
            return executablePath.Trim();
        }
    }

    private static int? GetMajorVersion(Version? version)
    {
        if (version is null)
        {
            return null;
        }

        return version.Major == 1 ? version.Minor : version.Major;
    }

    private static Version? TryParseVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(rawVersion, @"\d+");
        if (matches.Count == 0)
        {
            return null;
        }

        var parts = new int[Math.Min(4, matches.Count)];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = int.Parse(matches[i].Value);
        }

        return parts.Length switch
        {
            1 => new Version(parts[0], 0),
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            _ => new Version(parts[0], parts[1], parts[2], parts[3])
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? GetNestedBoolean(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(current.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static TEnum? GetEnum<TEnum>(JsonElement element, string propertyName)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) &&
                                      Enum.IsDefined(typeof(TEnum), number) => (TEnum)Enum.ToObject(typeof(TEnum), number),
            JsonValueKind.String when Enum.TryParse<TEnum>(property.GetString(), ignoreCase: true, out var parsed) => parsed,
            _ => null
        };
    }

    private static int? GetNestedInt(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.Number when current.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(current.GetString(), out var parsed) => parsed,
            _ => null
        };
    }
}

internal sealed record FrontendStoredJavaRuntime(
    string ExecutablePath,
    string DisplayName,
    Version? ParsedVersion,
    int? MajorVersion,
    bool IsEnabled,
    bool? Is64Bit,
    bool? IsJre,
    JavaBrandType? Brand,
    MachineType? Architecture);

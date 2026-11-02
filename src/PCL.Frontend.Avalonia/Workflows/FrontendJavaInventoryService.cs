using System.Text.Json;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendJavaInventoryService
{
    private static readonly IJavaParser JavaParser = BuildJavaParser();
    private static readonly object PortableJavaScanLock = new();
    private static IReadOnlyList<FrontendStoredJavaRuntime>? CachedPortableJavaRuntimes;
    private static bool IsPortableJavaScanCached;
    private static Task<IReadOnlyList<FrontendStoredJavaRuntime>>? PortableJavaScanTask;

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
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

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
                    Source = GetEnum<JavaSource>(item, "Source"),
                    Installation = GetNestedString(item, "Installation", "JavaExePath") is { } installationPath
                        ? new JavaStorageInstallationInfo
                        {
                            JavaExePath = NormalizeExecutablePath(installationPath),
                            DisplayName = GetNestedString(item, "Installation", "DisplayName"),
                            Version = GetNestedString(item, "Installation", "Version"),
                            MajorVersion = GetNestedInt(item, "Installation", "MajorVersion"),
                            Is64Bit = GetNestedBoolean(item, "Installation", "Is64Bit"),
                            IsJre = GetNestedBoolean(item, "Installation", "IsJre"),
                            Brand = GetNestedEnum<JavaBrandType>(item, "Installation", "Brand"),
                            Architecture = GetNestedEnum<MachineType>(item, "Installation", "Architecture")
                        }
                        : null
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "SetupJava", $"ParseStorageItems failed. rawLength={rawJson.Length}.");
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
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

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
                var isJre = installation?.IsJre ?? GetNestedBoolean(item, "Installation", "IsJre");
                var brand = installation?.Brand ?? GetNestedEnum<JavaBrandType>(item, "Installation", "Brand");
                var architecture = installation?.Architecture ?? GetNestedEnum<MachineType>(item, "Installation", "Architecture");
                var displayName = GetNestedString(item, "Installation", "DisplayName");
                displayName = !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : !string.IsNullOrWhiteSpace(storedVersionText)
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
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "SetupJava", $"ParseStoredJavaRuntimes failed. rawLength={rawJson.Length}.");
            return [];
        }
    }

    public static IReadOnlyList<FrontendStoredJavaRuntime> ParseAvailableRuntimes(string rawJson)
    {
        var storedRuntimes = ParseStoredJavaRuntimes(rawJson);
        var managedRuntimes = GetCachedManagedJavaRuntimes();
        if (managedRuntimes.Count == 0)
        {
            return storedRuntimes;
        }

        var merged = new Dictionary<string, FrontendStoredJavaRuntime>(StringComparer.OrdinalIgnoreCase);
        foreach (var runtime in storedRuntimes)
        {
            merged[runtime.ExecutablePath] = runtime;
        }

        foreach (var runtime in managedRuntimes)
        {
            if (!merged.TryGetValue(runtime.ExecutablePath, out var existingRuntime))
            {
                merged[runtime.ExecutablePath] = runtime;
                continue;
            }

            merged[runtime.ExecutablePath] = existingRuntime with
            {
                DisplayName = string.IsNullOrWhiteSpace(existingRuntime.DisplayName) ? runtime.DisplayName : existingRuntime.DisplayName,
                ParsedVersion = existingRuntime.ParsedVersion ?? runtime.ParsedVersion,
                MajorVersion = existingRuntime.MajorVersion ?? runtime.MajorVersion,
                Is64Bit = existingRuntime.Is64Bit ?? runtime.Is64Bit,
                IsJre = existingRuntime.IsJre ?? runtime.IsJre,
                Brand = existingRuntime.Brand ?? runtime.Brand,
                Architecture = existingRuntime.Architecture ?? runtime.Architecture
            };
        }

        return merged.Values.ToArray();
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

    public static FrontendStoredJavaRuntime CreateStoredRuntime(
        string executablePath,
        string? displayName,
        string? rawVersion,
        bool isEnabled = true,
        bool? is64Bit = null,
        bool? isJre = null,
        JavaBrandType? brand = null,
        MachineType? architecture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var normalizedPath = NormalizeExecutablePath(executablePath);
        var parsedVersion = TryParseVersion(rawVersion);
        return new FrontendStoredJavaRuntime(
            normalizedPath,
            !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : rawVersion
                  ?? Path.GetFileName(Path.GetDirectoryName(normalizedPath))
                  ?? "Java",
            parsedVersion,
            GetMajorVersion(parsedVersion),
            isEnabled,
            is64Bit,
            isJre,
            brand,
            architecture);
    }

    public static Task WarmPortableJavaScanCacheAsync()
    {
        lock (PortableJavaScanLock)
        {
            if (IsPortableJavaScanCached)
            {
                return Task.CompletedTask;
            }

            PortableJavaScanTask ??= Task.Run(ScanPortableJavaRuntimesAsync);
            return PortableJavaScanTask;
        }
    }

    public static Task RefreshPortableJavaScanCacheAsync()
    {
        lock (PortableJavaScanLock)
        {
            PortableJavaScanTask ??= Task.Run(ScanPortableJavaRuntimesAsync);
            return PortableJavaScanTask;
        }
    }

    private static IReadOnlyList<FrontendStoredJavaRuntime> GetCachedManagedJavaRuntimes()
    {
        lock (PortableJavaScanLock)
        {
            return IsPortableJavaScanCached
                ? CachedPortableJavaRuntimes ?? []
                : [];
        }
    }

    private static async Task<IReadOnlyList<FrontendStoredJavaRuntime>> ScanPortableJavaRuntimesAsync()
    {
        IReadOnlyList<FrontendStoredJavaRuntime> scannedRuntimes;
        try
        {
            var manager = JavaManagerFactory.CreateDefault();
            await manager.ScanJavaAsync(force: true).ConfigureAwait(false);
            scannedRuntimes = manager.GetSortedJavaList()
                .Select(entry => new FrontendStoredJavaRuntime(
                    entry.Installation.JavaExePath,
                    entry.Installation.Version.ToString(),
                    entry.Installation.Version,
                    entry.Installation.MajorVersion,
                    entry.IsEnabled,
                    entry.Installation.Is64Bit,
                    entry.Installation.IsJre,
                    entry.Installation.Brand,
                    entry.Installation.Architecture))
                .ToArray();
        }
        catch
        {
            scannedRuntimes = [];
        }

        lock (PortableJavaScanLock)
        {
            CachedPortableJavaRuntimes = scannedRuntimes;
            IsPortableJavaScanCached = true;
            PortableJavaScanTask = null;
            return CachedPortableJavaRuntimes;
        }
    }

    private static IJavaParser BuildJavaParser()
    {
        var parsers = new List<IJavaParser>
        {
            new CommandJavaParser(SystemJavaRuntimeEnvironment.Current, new ProcessCommandRunner())
        };
        if (OperatingSystem.IsWindows())
        {
            parsers.Add(new PeHeaderParser());
        }

        return new CompositeJavaParser([.. parsers]);
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
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

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
        if (!TryGetNestedProperty(element, path, out var current))
        {
            return null;
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

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
        if (!TryGetNestedProperty(element, path, out var current))
        {
            return null;
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
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

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

    private static TEnum? GetNestedEnum<TEnum>(JsonElement element, params string[] path)
        where TEnum : struct, Enum
    {
        if (!TryGetNestedProperty(element, path, out var current))
        {
            return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.Number when current.TryGetInt32(out var number) &&
                                      Enum.IsDefined(typeof(TEnum), number) => (TEnum)Enum.ToObject(typeof(TEnum), number),
            JsonValueKind.String when Enum.TryParse<TEnum>(current.GetString(), ignoreCase: true, out var parsed) => parsed,
            _ => null
        };
    }

    private static int? GetNestedInt(JsonElement element, params string[] path)
    {
        if (!TryGetNestedProperty(element, path, out var current))
        {
            return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.Number when current.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(current.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static bool TryGetNestedProperty(JsonElement element, IReadOnlyList<string> path, out JsonElement current)
    {
        current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                current = default;
                return false;
            }
        }

        return true;
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

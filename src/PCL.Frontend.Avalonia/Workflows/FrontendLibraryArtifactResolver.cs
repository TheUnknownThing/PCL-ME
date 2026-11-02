using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLibraryArtifactResolver
{
    private static readonly object NativeReplacementCatalogLock = new();
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>? CachedNativeReplacementCatalog;
    private static bool IsNativeReplacementCatalogCached;

    public static MachineType GetCurrentRuntimeArchitecture()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => MachineType.I386,
            Architecture.X64 => MachineType.AMD64,
            Architecture.Arm64 => MachineType.ARM64,
            _ => MachineType.Unknown
        };
    }

    public static bool IsLibraryAllowed(JsonElement library, MachineType runtimeArchitecture)
    {
        if (!library.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (!RuleMatchesCurrentPlatform(rule, runtimeArchitecture))
            {
                continue;
            }

            allowed = !string.Equals(GetString(rule, "action"), "disallow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    public static bool TryResolveArtifactDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out FrontendResolvedLibraryDownload download)
    {
        if (TryResolveArtifactReplacementDownload(library, launcherFolder, runtimeArchitecture, out download))
        {
            return true;
        }

        return TryResolveLibraryDownload(library, "artifact", launcherFolder, out download);
    }

    public static bool TryResolveNativeArchiveDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out FrontendResolvedNativeArchiveDownload nativeArchive)
    {
        nativeArchive = null!;
        var entryName = ResolveNativeEntryName(library, runtimeArchitecture);
        if (TryResolveNativeArchiveReplacementDownload(
                library,
                entryName,
                launcherFolder,
                runtimeArchitecture,
                out var replacementDownload))
        {
            nativeArchive = new FrontendResolvedNativeArchiveDownload(
                replacementDownload.TargetPath,
                replacementDownload.DownloadUrl,
                replacementDownload.Sha1,
                replacementDownload.Size,
                GetExtractExcludes(library));
            return true;
        }

        FrontendResolvedLibraryDownload download;
        if (IsDedicatedNativeLibrary(library))
        {
            if (!TryResolveArtifactDownload(library, launcherFolder, runtimeArchitecture, out download))
            {
                return false;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(entryName) ||
                !TryResolveLibraryDownload(library, entryName, launcherFolder, out download))
            {
                return false;
            }
        }

        nativeArchive = new FrontendResolvedNativeArchiveDownload(
            download.TargetPath,
            download.DownloadUrl,
            download.Sha1,
            download.Size,
            GetExtractExcludes(library));
        return true;
    }

    public static string BuildLibraryUrl(JsonElement library, string relativePath)
    {
        var baseUrl = GetString(library, "url");
        return BuildLibraryUrl(baseUrl, relativePath);
    }

    public static string BuildLibraryUrl(string? baseUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://libraries.minecraft.net/";
        }

        return $"{baseUrl.TrimEnd('/')}/{relativePath.Replace('\\', '/')}";
    }

    public static string DeriveLibraryPathFromName(string libraryName, string? classifier = null)
    {
        var segments = libraryName.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return libraryName.Replace(':', Path.DirectorySeparatorChar);
        }

        var groupPath = segments[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = segments[1];
        var version = segments[2];
        var effectiveClassifier = string.IsNullOrWhiteSpace(classifier)
            ? (segments.Length >= 4 ? segments[3] : null)
            : classifier;
        var extension = "jar";

        if (segments.Length >= 5 && !string.IsNullOrWhiteSpace(segments[4]))
        {
            extension = segments[4];
        }

        if (!string.IsNullOrWhiteSpace(effectiveClassifier))
        {
            ParseLibraryCoordinateExtension(ref effectiveClassifier, ref extension);
        }

        ParseLibraryCoordinateExtension(ref version, ref extension);
        if (string.IsNullOrWhiteSpace(version))
        {
            return libraryName.Replace(':', Path.DirectorySeparatorChar);
        }

        var classifierSuffix = string.IsNullOrWhiteSpace(effectiveClassifier) ? string.Empty : "-" + effectiveClassifier;
        return Path.Combine(groupPath, artifact, version, $"{artifact}-{version}{classifierSuffix}.{extension}");
    }

    private static bool TryResolveNativeArchiveReplacementDownload(
        JsonElement library,
        string? entryName,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out FrontendResolvedLibraryDownload download)
    {
        download = null!;
        var libraryName = GetString(library, "name");
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entryName) &&
            TryResolveArtifactReplacementDownload($"{libraryName}:{entryName}", launcherFolder, runtimeArchitecture, out download))
        {
            return true;
        }

        return TryResolveArtifactReplacementDownload(libraryName, launcherFolder, runtimeArchitecture, out download);
    }

    private static bool TryResolveLibraryDownload(
        JsonElement library,
        string entryName,
        string launcherFolder,
        out FrontendResolvedLibraryDownload download)
    {
        download = null!;
        var libraryName = GetString(library, "name");
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return TryResolveLegacyLibraryDownloadWithoutDownloads(library, entryName, launcherFolder, out download);
        }

        if (!library.TryGetProperty("downloads", out var downloads) || downloads.ValueKind != JsonValueKind.Object)
        {
            if (!string.Equals(entryName, "artifact", StringComparison.Ordinal))
            {
                return false;
            }

            var fallbackPath = DeriveLibraryPathFromName(libraryName);
            var fallbackTargetPath = Path.Combine(launcherFolder, "libraries", fallbackPath.Replace('/', Path.DirectorySeparatorChar));
            download = new FrontendResolvedLibraryDownload(
                fallbackTargetPath,
                BuildLibraryUrl(library, fallbackPath),
                GetString(library, "sha1"),
                null);
            return true;
        }

        JsonElement downloadEntry;
        if (string.Equals(entryName, "artifact", StringComparison.Ordinal))
        {
            if (!downloads.TryGetProperty("artifact", out downloadEntry) || downloadEntry.ValueKind != JsonValueKind.Object)
            {
                if (downloads.TryGetProperty("classifiers", out var classifiers) &&
                    classifiers.ValueKind == JsonValueKind.Object)
                {
                    return false;
                }

                return TryResolveLegacyLibraryDownloadWithoutDownloads(library, entryName, launcherFolder, out download);
            }
        }
        else
        {
            if (!downloads.TryGetProperty("classifiers", out var classifiers) ||
                classifiers.ValueKind != JsonValueKind.Object ||
                !classifiers.TryGetProperty(entryName, out downloadEntry) ||
                downloadEntry.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
        }

        var path = GetString(downloadEntry, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            path = DeriveLibraryPathFromName(
                libraryName,
                string.Equals(entryName, "artifact", StringComparison.Ordinal) ? null : entryName);
        }

        var targetPath = Path.Combine(launcherFolder, "libraries", path.Replace('/', Path.DirectorySeparatorChar));
        var hasExplicitUrl = downloadEntry.TryGetProperty("url", out var urlElement);
        var url = hasExplicitUrl && urlElement.ValueKind == JsonValueKind.String
            ? urlElement.GetString()
            : GetString(downloadEntry, "url");
        if (!hasExplicitUrl || !string.IsNullOrWhiteSpace(url))
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = BuildLibraryUrl(library, path);
            }
        }

        download = new FrontendResolvedLibraryDownload(
            targetPath,
            url,
            GetString(downloadEntry, "sha1"),
            GetLong(downloadEntry, "size"));
        return true;
    }

    private static bool TryResolveLegacyLibraryDownloadWithoutDownloads(
        JsonElement library,
        string entryName,
        string launcherFolder,
        out FrontendResolvedLibraryDownload download)
    {
        download = null!;
        if (!string.Equals(entryName, "artifact", StringComparison.Ordinal))
        {
            return false;
        }

        var libraryName = GetString(library, "name");
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return false;
        }

        var relativePath = DeriveLibraryPathFromName(libraryName);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var targetPath = Path.Combine(launcherFolder, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));
        download = new FrontendResolvedLibraryDownload(
            targetPath,
            BuildLibraryUrl(library, relativePath),
            null,
            null);
        return true;
    }

    private static bool TryResolveArtifactReplacementDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out FrontendResolvedLibraryDownload download)
    {
        download = null!;
        var libraryName = GetString(library, "name");
        return !string.IsNullOrWhiteSpace(libraryName) &&
               TryResolveArtifactReplacementDownload(libraryName, launcherFolder, runtimeArchitecture, out download);
    }

    private static bool TryResolveArtifactReplacementDownload(
        string libraryName,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out FrontendResolvedLibraryDownload download)
    {
        download = null!;
        var platformKey = ResolveNativeReplacementPlatformKey(runtimeArchitecture);
        if (string.IsNullOrWhiteSpace(platformKey))
        {
            return false;
        }

        var catalog = GetNativeReplacementCatalog();
        if (!catalog.TryGetValue(platformKey, out var platformCatalog) ||
            !platformCatalog.TryGetValue(libraryName, out var replacement))
        {
            return false;
        }

        var replacementTargetPath = Path.Combine(
            launcherFolder,
            "libraries",
            replacement.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var replacementUrl = string.IsNullOrWhiteSpace(replacement.DownloadUrl)
            ? "https://repo1.maven.org/maven2/" + replacement.ArtifactPath
            : replacement.DownloadUrl;

        download = new FrontendResolvedLibraryDownload(
            replacementTargetPath,
            replacementUrl,
            replacement.Sha1,
            null);
        return true;
    }

    private static string? ResolveNativeReplacementPlatformKey(MachineType runtimeArchitecture)
    {
        if (OperatingSystem.IsWindows())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "windows-arm64",
                MachineType.I386 => "windows-x86",
                MachineType.AMD64 => "windows-x64",
                _ => null
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "macos-arm64",
                MachineType.AMD64 => "macos-x64",
                _ => null
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "linux-arm64",
                MachineType.I386 => "linux-x86",
                MachineType.AMD64 => "linux-x64",
                _ => null
            };
        }

        return null;
    }

    private static bool TryParseLibraryCoordinate(string? name, out LibraryCoordinate coordinate)
    {
        coordinate = new LibraryCoordinate(string.Empty, string.Empty, string.Empty, null);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var parts = name.Split(':', StringSplitOptions.None);
        if (parts.Length is < 3 or > 4)
        {
            return false;
        }

        coordinate = new LibraryCoordinate(
            parts[0],
            parts[1],
            parts[2],
            parts.Length >= 4 ? parts[3] : null);
        return true;
    }

    private static bool IsDedicatedNativeLibrary(JsonElement library)
    {
        return TryParseLibraryCoordinate(GetString(library, "name"), out var coordinate) &&
               !string.IsNullOrWhiteSpace(coordinate.Classifier) &&
               (coordinate.Classifier.StartsWith("natives-", StringComparison.OrdinalIgnoreCase) ||
                coordinate.Classifier.StartsWith("native-", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> GetNativeReplacementCatalog()
    {
        lock (NativeReplacementCatalogLock)
        {
            if (IsNativeReplacementCatalogCached)
            {
                return CachedNativeReplacementCatalog ?? EmptyNativeReplacementCatalog;
            }

            CachedNativeReplacementCatalog = LoadNativeReplacementCatalog();
            IsNativeReplacementCatalogCached = true;
            return CachedNativeReplacementCatalog;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> LoadNativeReplacementCatalog()
    {
        var catalogPath = FrontendLauncherAssetLocator.GetPath("NativeReplacements", "native-replacements.json");
        if (!File.Exists(catalogPath))
        {
            return EmptyNativeReplacementCatalog;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return EmptyNativeReplacementCatalog;
            }

            var platforms = new Dictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var platformProperty in document.RootElement.EnumerateObject())
            {
                if (platformProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var replacements = new Dictionary<string, ReplacementArtifactInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var replacementProperty in platformProperty.Value.EnumerateObject())
                {
                    if (TryParseReplacementArtifactInfo(replacementProperty.Value, out var replacement))
                    {
                        replacements[replacementProperty.Name] = replacement;
                    }
                }

                if (replacements.Count > 0)
                {
                    platforms[platformProperty.Name] = replacements;
                }
            }

            return platforms.Count == 0 ? EmptyNativeReplacementCatalog : platforms;
        }
        catch
        {
            return EmptyNativeReplacementCatalog;
        }
    }

    private static bool TryParseReplacementArtifactInfo(JsonElement element, out ReplacementArtifactInfo replacement)
    {
        replacement = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("artifact", out var artifact) ||
            artifact.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var path = GetString(artifact, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        replacement = new ReplacementArtifactInfo(
            GetString(element, "name"),
            path,
            GetString(artifact, "url"),
            GetString(artifact, "sha1"));
        return true;
    }

    private static string? ResolveNativeEntryName(JsonElement library, MachineType runtimeArchitecture)
    {
        var osKey = GetCurrentNativeOsKey();
        if (library.TryGetProperty("natives", out var natives) &&
            natives.ValueKind == JsonValueKind.Object &&
            natives.TryGetProperty(osKey, out var classifierValue) &&
            classifierValue.ValueKind == JsonValueKind.String)
        {
            return classifierValue.GetString()?.Replace(
                "${arch}",
                GetNativeArchitectureToken(runtimeArchitecture),
                StringComparison.Ordinal);
        }

        if (!library.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("classifiers", out var classifiers) ||
            classifiers.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var candidate in GetCurrentNativeClassifierCandidates(runtimeArchitecture))
        {
            if (classifiers.TryGetProperty(candidate, out _))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetCurrentNativeClassifierCandidates(MachineType runtimeArchitecture)
    {
        var candidates = new List<string>();
        string[] osNames = OperatingSystem.IsMacOS()
            ? ["osx", "macos", "mac-os", "mac"]
            : OperatingSystem.IsWindows()
                ? ["windows"]
                : ["linux"];
        var archNames = runtimeArchitecture switch
        {
            MachineType.I386 => new[] { "x86", "32" },
            MachineType.ARM64 => new[] { "arm64", "aarch64", "64" },
            _ => new[] { "x86_64", "amd64", "64" }
        };

        foreach (var osName in osNames)
        {
            candidates.Add($"natives-{osName}");
            candidates.Add($"native-{osName}");
            foreach (var archName in archNames)
            {
                candidates.Add($"natives-{osName}-{archName}");
                candidates.Add($"native-{osName}-{archName}");
            }
        }

        return candidates;
    }

    private static string GetCurrentNativeOsKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return "linux";
    }

    private static string GetNativeArchitectureToken(MachineType runtimeArchitecture)
    {
        return runtimeArchitecture == MachineType.I386 ? "32" : "64";
    }

    private static IReadOnlyList<string> GetExtractExcludes(JsonElement library)
    {
        if (!library.TryGetProperty("extract", out var extract) ||
            extract.ValueKind != JsonValueKind.Object ||
            !extract.TryGetProperty("exclude", out var exclude) ||
            exclude.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return exclude.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Replace('\\', '/'))
            .ToArray();
    }

    private static bool RuleMatchesCurrentPlatform(JsonElement rule, MachineType runtimeArchitecture)
    {
        if (!rule.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var osName = GetString(os, "name");
        if (!string.IsNullOrWhiteSpace(osName) && !IsCurrentOs(osName))
        {
            return false;
        }

        var osArch = GetString(os, "arch");
        if (!string.IsNullOrWhiteSpace(osArch) && !IsCurrentArchitecture(osArch, runtimeArchitecture))
        {
            return false;
        }

        return true;
    }

    private static bool IsCurrentOs(string osName)
    {
        return osName.ToLowerInvariant() switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "osx" or "macos" => OperatingSystem.IsMacOS(),
            "linux" => OperatingSystem.IsLinux(),
            _ => true
        };
    }

    private static bool IsCurrentArchitecture(string osArch, MachineType runtimeArchitecture)
    {
        return osArch.ToLowerInvariant() switch
        {
            "x86" => runtimeArchitecture == MachineType.I386,
            "x86_64" or "amd64" => runtimeArchitecture == MachineType.AMD64,
            "arm64" or "aarch64" => runtimeArchitecture == MachineType.ARM64,
            "arm" => runtimeArchitecture is MachineType.ARM or MachineType.ARMNT,
            _ => true
        };
    }

    private static void ParseLibraryCoordinateExtension(ref string? value, ref string extension)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var extensionIndex = value.IndexOf('@');
        if (extensionIndex < 0)
        {
            return;
        }

        if (extensionIndex < value.Length - 1)
        {
            extension = value[(extensionIndex + 1)..];
        }

        value = value[..extensionIndex];
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numericResult))
        {
            return numericResult;
        }

        return long.TryParse(value.ToString(), out var parsedResult) ? parsedResult : null;
    }

    private sealed record ReplacementArtifactInfo(
        string? ReplacementName,
        string ArtifactPath,
        string? DownloadUrl,
        string? Sha1);

    private sealed record LibraryCoordinate(
        string GroupId,
        string ArtifactId,
        string Version,
        string? Classifier);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> EmptyNativeReplacementCatalog =
        new Dictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>(StringComparer.OrdinalIgnoreCase);
}

internal sealed record FrontendResolvedLibraryDownload(
    string TargetPath,
    string? DownloadUrl,
    string? Sha1,
    long? Size);

internal sealed record FrontendResolvedNativeArchiveDownload(
    string TargetPath,
    string? DownloadUrl,
    string? Sha1,
    long? Size,
    IReadOnlyList<string> ExtractExcludes);

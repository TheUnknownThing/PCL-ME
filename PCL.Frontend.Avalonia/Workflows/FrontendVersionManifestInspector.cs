using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendVersionManifestInspector
{
    public static FrontendVersionManifestProfile ReadProfile(string launcherFolder, string versionName)
    {
        if (string.IsNullOrWhiteSpace(launcherFolder) || string.IsNullOrWhiteSpace(versionName))
        {
            return FrontendVersionManifestProfile.Empty;
        }

        return ReadProfileRecursive(
            launcherFolder,
            versionName.Trim(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    public static FrontendVersionManifestProfile ReadProfileFromManifestPath(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return FrontendVersionManifestProfile.Empty;
        }

        var versionDirectory = Path.GetDirectoryName(manifestPath);
        var versionsDirectory = versionDirectory is null ? null : Path.GetDirectoryName(versionDirectory);
        var launcherFolder = versionsDirectory is null ? null : Path.GetDirectoryName(versionsDirectory);
        var versionName = Path.GetFileNameWithoutExtension(manifestPath);

        if (string.IsNullOrWhiteSpace(versionDirectory) ||
            string.IsNullOrWhiteSpace(versionsDirectory) ||
            string.IsNullOrWhiteSpace(launcherFolder) ||
            string.IsNullOrWhiteSpace(versionName))
        {
            return FrontendVersionManifestProfile.Empty;
        }

        return ReadProfile(launcherFolder, versionName);
    }

    public static Dictionary<string, string> CreateLoaderMap(FrontendVersionManifestProfile profile)
    {
        var loaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddLoader(loaders, "forge", profile.HasForge, profile.ForgeVersion);
        AddLoader(loaders, "neoforge", profile.HasNeoForge, profile.NeoForgeVersion);
        AddLoader(loaders, "cleanroom", profile.HasCleanroom, profile.CleanroomVersion);
        AddLoader(loaders, "fabric", profile.HasFabric, profile.FabricVersion);
        AddLoader(loaders, "quilt", profile.HasQuilt, profile.QuiltVersion);
        AddLoader(loaders, "legacyfabric", profile.HasLegacyFabric, profile.LegacyFabricVersion);
        AddLoader(loaders, "optifine", profile.HasOptiFine, profile.OptiFineVersion);
        AddLoader(loaders, "liteloader", profile.HasLiteLoader, profile.LiteLoaderVersion);
        AddLoader(loaders, "labymod", profile.HasLabyMod, profile.LabyModVersion);

        return loaders;
    }

    internal static Version ParseComparableVanillaVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new Version(9999, 0, 0);
        }

        var candidate = NormalizeVanillaVersionName(rawValue);
        if (candidate.StartsWith("1.", StringComparison.OrdinalIgnoreCase))
        {
            var segments = candidate.Split([' ', '_', '-', '.'], StringSplitOptions.None);
            return new Version(
                ParseLeadingIntegerSegment(segments, 1),
                0,
                ParseLeadingIntegerSegment(segments, 2));
        }

        if (Regex.IsMatch(candidate, "^[2-9][0-9]\\.", RegexOptions.CultureInvariant))
        {
            var segments = candidate.Split([' ', '_', '-', '.'], StringSplitOptions.None);
            return new Version(
                ParseLeadingIntegerSegment(segments, 0),
                ParseLeadingIntegerSegment(segments, 1),
                ParseLeadingIntegerSegment(segments, 2));
        }

        return new Version(9999, 0, 0);
    }

    private static FrontendVersionManifestProfile ReadProfileRecursive(
        string launcherFolder,
        string versionName,
        ISet<string> visited)
    {
        if (!visited.Add(versionName))
        {
            return FrontendVersionManifestProfile.Empty;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return FrontendVersionManifestProfile.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var parentVersion = GetString(root, "inheritsFrom");
            var parentProfile = string.IsNullOrWhiteSpace(parentVersion)
                ? FrontendVersionManifestProfile.Empty
                : ReadProfileRecursive(launcherFolder, parentVersion, visited);
            var currentLibraries = ParseLibraryNames(root);
            var allLibraries = parentProfile.LibraryNames
                .Concat(currentLibraries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var releaseTime = GetDateTime(root, "releaseTime") ?? parentProfile.ReleaseTime;
            var versionType = FirstNonEmpty(GetString(root, "type"), parentProfile.VersionType);
            var vanillaVersion = NormalizeVanillaVersionName(
                ResolveVanillaVersion(
                    launcherFolder,
                    versionName,
                    root,
                    parentVersion,
                    releaseTime,
                    versionType));

            return new FrontendVersionManifestProfile(
                IsManifestValid: true,
                VanillaVersion: vanillaVersion,
                ParsedVanillaVersion: ParseComparableVanillaVersion(vanillaVersion),
                VersionType: versionType,
                ReleaseTime: releaseTime,
                AssetsIndexName: GetNestedString(root, "assetIndex", "id")
                                 ?? GetString(root, "assets")
                                 ?? parentProfile.AssetsIndexName,
                HasForge: parentProfile.HasForge || ContainsLibrary(allLibraries, "net.minecraftforge:forge"),
                ForgeVersion: parentProfile.ForgeVersion ?? ExtractLibraryVersion(allLibraries, "net.minecraftforge:forge"),
                NeoForgeVersion: parentProfile.NeoForgeVersion ?? ExtractNeoForgeVersion(root, allLibraries),
                CleanroomVersion: parentProfile.CleanroomVersion ?? ExtractLibraryVersion(allLibraries, "com.cleanroommc"),
                FabricVersion: parentProfile.FabricVersion ?? ExtractLibraryVersion(allLibraries, "net.fabricmc:fabric-loader"),
                LegacyFabricVersion: parentProfile.LegacyFabricVersion ?? ExtractLibraryVersion(allLibraries, "net.legacyfabric"),
                QuiltVersion: parentProfile.QuiltVersion ?? ExtractLibraryVersion(allLibraries, "org.quiltmc:quilt-loader"),
                OptiFineVersion: parentProfile.OptiFineVersion ?? ExtractOptiFineVersion(allLibraries),
                HasLiteLoader: parentProfile.HasLiteLoader || ContainsLibrary(allLibraries, "liteloader"),
                LiteLoaderVersion: parentProfile.LiteLoaderVersion ?? ExtractLibraryVersion(allLibraries, "com.mumfrey:liteloader"),
                LabyModVersion: parentProfile.LabyModVersion ?? ExtractLibraryVersion(allLibraries, "net.labymod"),
                HasLabyMod: parentProfile.HasLabyMod || ContainsLibrary(allLibraries, "labymod"),
                HasFabricApi: parentProfile.HasFabricApi || ContainsLibrary(allLibraries, "fabric-api"),
                FabricApiVersion: parentProfile.FabricApiVersion ?? ExtractLibraryVersion(allLibraries, "net.fabricmc.fabric-api:fabric-api"),
                HasQsl: parentProfile.HasQsl || ContainsLibrary(allLibraries, "quilted-fabric-api") || ContainsLibrary(allLibraries, ":qsl"),
                QslVersion: parentProfile.QslVersion
                            ?? ExtractLibraryVersion(allLibraries, "org.quiltmc.quilted-fabric-api")
                            ?? ExtractLibraryVersion(allLibraries, "org.quiltmc:qsl"),
                HasOptiFabric: parentProfile.HasOptiFabric || ContainsLibrary(allLibraries, "optifabric"),
                OptiFabricVersion: parentProfile.OptiFabricVersion ?? ExtractLibraryVersion(allLibraries, "optifabric"),
                JsonRequiredMajorVersion: GetNestedInt(root, "javaVersion", "majorVersion") ?? parentProfile.JsonRequiredMajorVersion,
                MojangRecommendedMajorVersion: GetNestedInt(root, "javaVersion", "majorVersion") ?? parentProfile.MojangRecommendedMajorVersion,
                MojangRecommendedComponent: GetNestedString(root, "javaVersion", "component") ?? parentProfile.MojangRecommendedComponent,
                LibraryNames: allLibraries);
        }
        catch
        {
            return FrontendVersionManifestProfile.Empty;
        }
    }

    private static IReadOnlyList<string> ParseLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return libraries.EnumerateArray()
            .Select(library => GetString(library, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static string ResolveVanillaVersion(
        string launcherFolder,
        string versionName,
        JsonElement root,
        string? parentVersion,
        DateTime? releaseTime,
        string? versionType)
    {
        if (releaseTime.HasValue && releaseTime.Value.Year > 2000 && releaseTime.Value.Year < 2013)
        {
            return "Old";
        }

        if (string.Equals(versionType, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return "pending";
        }

        var clientVersion = GetString(root, "clientVersion");
        if (!string.IsNullOrWhiteSpace(clientVersion))
        {
            return clientVersion;
        }

        var patchVersion = GetPatchGameVersion(root);
        if (!string.IsNullOrWhiteSpace(patchVersion))
        {
            return patchVersion;
        }

        var argumentsVersion = ExtractMinecraftVersionFromArguments(root);
        if (!string.IsNullOrWhiteSpace(argumentsVersion))
        {
            return argumentsVersion;
        }

        if (!string.IsNullOrWhiteSpace(parentVersion))
        {
            return GetString(root, "jar") ?? parentVersion;
        }

        var downloadsVersion = TryRegexSeek(
            root.TryGetProperty("downloads", out var downloads) ? downloads.ToString() : null,
            RegexPatterns.MinecraftDownloadUrlVersion);
        if (!string.IsNullOrWhiteSpace(downloadsVersion))
        {
            return downloadsVersion;
        }

        var librariesString = root.TryGetProperty("libraries", out var libraries) ? libraries.ToString() : string.Empty;
        var forgeVersion = TryRegexSeek(librariesString, RegexPatterns.ForgeLibVersion);
        if (!string.IsNullOrWhiteSpace(forgeVersion))
        {
            return forgeVersion;
        }

        var optiFineVersion = TryRegexSeek(librariesString, RegexPatterns.OptiFineLibVersion);
        if (!string.IsNullOrWhiteSpace(optiFineVersion))
        {
            return optiFineVersion;
        }

        var fabricLikeVersion = TryRegexSeek(librariesString, RegexPatterns.FabricLikeLibVersion);
        if (!string.IsNullOrWhiteSpace(fabricLikeVersion))
        {
            return fabricLikeVersion;
        }

        var jarName = GetString(root, "jar");
        if (!string.IsNullOrWhiteSpace(jarName))
        {
            return jarName;
        }

        var jarVersionName = TryReadJarVersionName(launcherFolder, versionName);
        if (!string.IsNullOrWhiteSpace(jarVersionName) && jarVersionName.Length < 32)
        {
            return jarVersionName;
        }

        var idVersion = TryRegexSeek(GetString(root, "id"), RegexPatterns.MinecraftJsonVersion);
        if (!string.IsNullOrWhiteSpace(idVersion))
        {
            return idVersion;
        }

        var folderVersion = TryRegexSeek(versionName, RegexPatterns.MinecraftJsonVersion);
        if (!string.IsNullOrWhiteSpace(folderVersion))
        {
            return folderVersion;
        }

        var rawJsonVersion = TryRegexSeek(GetJsonTextWithoutLibraries(root), RegexPatterns.MinecraftJsonVersion);
        return !string.IsNullOrWhiteSpace(rawJsonVersion)
            ? rawJsonVersion
            : "Unknown";
    }

    private static bool ContainsLibrary(IEnumerable<string> libraries, string searchText)
    {
        return libraries.Any(library => library.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractLibraryVersion(IEnumerable<string> libraries, string prefix)
    {
        var match = libraries.FirstOrDefault(library => library.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase));
        return match?.Split(':').LastOrDefault();
    }

    private static string? ExtractNeoForgeVersion(JsonElement root, IEnumerable<string> libraries)
    {
        return ExtractLibraryCoordinateVersion(libraries, "net.neoforged", "neoforge")
               ?? ExtractLibraryCoordinateVersion(libraries, "net.neoforged", "forge")
               ?? ExtractNeoForgeVersionFromArguments(root);
    }

    private static string? ExtractLibraryCoordinateVersion(IEnumerable<string> libraries, string group, string artifact)
    {
        foreach (var library in libraries)
        {
            if (string.IsNullOrWhiteSpace(library))
            {
                continue;
            }

            var segments = library.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length < 3)
            {
                continue;
            }

            if (!string.Equals(segments[0], group, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[1], artifact, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(segments[2]) ? null : segments[2];
        }

        return null;
    }

    private static string? ExtractNeoForgeVersionFromArguments(JsonElement root)
    {
        if (!root.TryGetProperty("arguments", out var argumentsElement) ||
            argumentsElement.ValueKind != JsonValueKind.Object ||
            !argumentsElement.TryGetProperty("game", out var gameArguments))
        {
            return null;
        }

        var flattened = new List<string>();
        CollectArgumentStrings(gameArguments, flattened);
        for (var index = 0; index < flattened.Count - 1; index++)
        {
            var argument = flattened[index];
            if (!string.Equals(argument, "--fml.neoForgeVersion", StringComparison.Ordinal) &&
                !string.Equals(argument, "--fml.forgeVersion", StringComparison.Ordinal))
            {
                continue;
            }

            var version = flattened[index + 1];
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }

    private static void CollectArgumentStrings(JsonElement element, ICollection<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectArgumentStrings(item, values);
                }

                break;
            case JsonValueKind.Object when element.TryGetProperty("value", out var nestedValue):
                CollectArgumentStrings(nestedValue, values);
                break;
        }
    }

    private static string? ExtractOptiFineVersion(IEnumerable<string> libraries)
    {
        var match = libraries.FirstOrDefault(library => library.Contains("optifine", StringComparison.OrdinalIgnoreCase));
        return match?.Split(':').LastOrDefault();
    }

    private static string? GetPatchGameVersion(JsonElement root)
    {
        if (!root.TryGetProperty("patches", out var patches) || patches.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var patch in patches.EnumerateArray())
        {
            if (!string.Equals(GetString(patch, "id"), "game", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = GetString(patch, "version");
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }

    private static string? ExtractMinecraftVersionFromArguments(JsonElement root)
    {
        if (!root.TryGetProperty("arguments", out var argumentsElement) || argumentsElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (argumentsElement.TryGetProperty("game", out var gameArguments))
        {
            var flattenedGameArguments = new List<string>();
            CollectArgumentStrings(gameArguments, flattenedGameArguments);
            for (var index = 0; index < flattenedGameArguments.Count - 1; index++)
            {
                if (!string.Equals(flattenedGameArguments[index], "--fml.mcVersion", StringComparison.Ordinal))
                {
                    continue;
                }

                var version = flattenedGameArguments[index + 1];
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }

            if (argumentsElement.TryGetProperty("jvm", out _))
            {
                foreach (var argument in flattenedGameArguments)
                {
                    var version = TryRegexSeek(argument, RegexPatterns.LabyModVersion);
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return version;
                    }
                }
            }
        }

        return null;
    }

    private static string? TryReadJarVersionName(string launcherFolder, string versionName)
    {
        try
        {
            var jarPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.jar");
            if (!File.Exists(jarPath))
            {
                return null;
            }

            using var fileStream = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var jarArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var versionJson = jarArchive.GetEntry("version.json");
            if (versionJson is null)
            {
                return null;
            }

            using var versionJsonStream = new StreamReader(versionJson.Open());
            using var document = JsonDocument.Parse(versionJsonStream.ReadToEnd());
            return GetString(document.RootElement, "name");
        }
        catch
        {
            return null;
        }
    }

    private static string GetJsonTextWithoutLibraries(JsonElement root)
    {
        try
        {
            var json = JsonNode.Parse(root.GetRawText()) as JsonObject;
            if (json is null)
            {
                return root.ToString();
            }

            json.Remove("libraries");
            return json.ToJsonString();
        }
        catch
        {
            return root.ToString();
        }
    }

    private static string NormalizeVanillaVersionName(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "Unknown";
        }

        return rawValue.Trim()
            .Replace("_unobfuscated", string.Empty, StringComparison.Ordinal)
            .Replace(" Unobfuscated", string.Empty, StringComparison.Ordinal);
    }

    private static int ParseLeadingIntegerSegment(string[] segments, int index)
    {
        if (index < 0 || index >= segments.Length)
        {
            return 0;
        }

        var segment = segments[index];
        if (string.IsNullOrEmpty(segment))
        {
            return 0;
        }

        var digits = new string(segment.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private static string? TryRegexSeek(string? input, Regex regex)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = regex.Match(input);
        return match.Success ? match.Value : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static int? GetNestedInt(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        var rawValue = GetString(element, propertyName);
        return DateTime.TryParse(rawValue, out var value) ? value : null;
    }

    private static void AddLoader(IDictionary<string, string> loaders, string key, bool installed, string? version)
    {
        if (!installed)
        {
            return;
        }

        loaders[key] = string.IsNullOrWhiteSpace(version) ? "已安装" : version;
    }
}

internal sealed record FrontendVersionManifestProfile(
    bool IsManifestValid,
    string VanillaVersion,
    Version? ParsedVanillaVersion,
    string? VersionType,
    DateTime? ReleaseTime,
    string? AssetsIndexName,
    bool HasForge,
    string? ForgeVersion,
    string? NeoForgeVersion,
    string? CleanroomVersion,
    string? FabricVersion,
    string? LegacyFabricVersion,
    string? QuiltVersion,
    string? OptiFineVersion,
    bool HasLiteLoader,
    string? LiteLoaderVersion,
    string? LabyModVersion,
    bool HasLabyMod,
    bool HasFabricApi,
    string? FabricApiVersion,
    bool HasQsl,
    string? QslVersion,
    bool HasOptiFabric,
    string? OptiFabricVersion,
    int? JsonRequiredMajorVersion,
    int? MojangRecommendedMajorVersion,
    string? MojangRecommendedComponent,
    IReadOnlyList<string> LibraryNames)
{
    public static FrontendVersionManifestProfile Empty { get; } = new(
        IsManifestValid: false,
        VanillaVersion: "Unknown",
        ParsedVanillaVersion: null,
        VersionType: null,
        ReleaseTime: null,
        AssetsIndexName: null,
        HasForge: false,
        ForgeVersion: null,
        NeoForgeVersion: null,
        CleanroomVersion: null,
        FabricVersion: null,
        LegacyFabricVersion: null,
        QuiltVersion: null,
        OptiFineVersion: null,
        HasLiteLoader: false,
        LiteLoaderVersion: null,
        LabyModVersion: null,
        HasLabyMod: false,
        HasFabricApi: false,
        FabricApiVersion: null,
        HasQsl: false,
        QslVersion: null,
        HasOptiFabric: false,
        OptiFabricVersion: null,
        JsonRequiredMajorVersion: null,
        MojangRecommendedMajorVersion: null,
        MojangRecommendedComponent: null,
        LibraryNames: Array.Empty<string>());

    public bool HasNeoForge => !string.IsNullOrWhiteSpace(NeoForgeVersion);

    public bool HasCleanroom => !string.IsNullOrWhiteSpace(CleanroomVersion);

    public bool HasFabric => !string.IsNullOrWhiteSpace(FabricVersion);

    public bool HasLegacyFabric => !string.IsNullOrWhiteSpace(LegacyFabricVersion);

    public bool HasQuilt => !string.IsNullOrWhiteSpace(QuiltVersion);

    public bool HasOptiFine => !string.IsNullOrWhiteSpace(OptiFineVersion);

    public bool HasForgeLike => HasForge || HasNeoForge;

    public bool HasFabricLike => HasFabric || HasLegacyFabric || HasQuilt;

    public bool IsModable => HasForgeLike
                             || HasCleanroom
                             || HasFabricLike
                             || HasLiteLoader
                             || HasLabyMod
                             || HasOptiFine;

    public string? PrimaryLoaderName =>
        HasNeoForge ? "NeoForge" :
        HasCleanroom ? "Cleanroom" :
        HasFabric ? "Fabric" :
        HasLegacyFabric ? "Legacy Fabric" :
        HasQuilt ? "Quilt" :
        HasForge ? "Forge" :
        HasOptiFine ? "OptiFine" :
        HasLiteLoader ? "LiteLoader" :
        HasLabyMod ? "LabyMod" :
        null;
}

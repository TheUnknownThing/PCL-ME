using System.Text.Json;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendDownloadCompositionService
{
    public static FrontendDownloadComposition Compose(
        FrontendRuntimePaths runtimePaths,
        FrontendInstanceComposition instanceComposition,
        FrontendVersionSavesComposition versionSavesComposition)
    {
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        var preferredMinecraftVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var versionSourceIndex = ReadValue(sharedConfig, "ToolDownloadVersion", 1);

        return new FrontendDownloadComposition(
            BuildInstallState(instanceComposition),
            BuildCatalogStates(versionSourceIndex, preferredMinecraftVersion),
            BuildFavoritesState(sharedConfig),
            BuildResourceStates(instanceComposition, sharedConfig));
    }

    private static FrontendDownloadInstallState BuildInstallState(FrontendInstanceComposition instanceComposition)
    {
        var selectionName = string.IsNullOrWhiteSpace(instanceComposition.Selection.InstanceName)
            ? "新的安装方案"
            : instanceComposition.Selection.InstanceName;
        return new FrontendDownloadInstallState(
            selectionName,
            instanceComposition.Install.MinecraftVersion,
            instanceComposition.Install.MinecraftIconName,
            instanceComposition.Install.Hints,
            instanceComposition.Install.Options
                .Select(option => new FrontendDownloadInstallOption(option.Title, option.Selection, option.IconName))
                .ToArray());
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildCatalogStates(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        return FrontendDownloadRemoteCatalogService.BuildCatalogStates(versionSourceIndex, preferredMinecraftVersion);
    }

    private static FrontendDownloadFavoritesState BuildFavoritesState(JsonFileProvider sharedConfig)
    {
        var raw = ReadValue(sharedConfig, "CompFavorites", "[]");
        var targets = ParseFavoriteTargets(raw, out var migratedOldFormat);
        var targetNames = targets.Select(target => target.Name).ToArray();
        var sections = targets
            .Select(target => new FrontendDownloadCatalogSection(
                target.Name,
                EnsureCatalogEntries(
                    target.Favorites
                        .OrderBy(favorite => favorite, StringComparer.OrdinalIgnoreCase)
                        .Select(favorite => new FrontendDownloadCatalogEntry(
                            target.Notes.TryGetValue(favorite, out var note) && !string.IsNullOrWhiteSpace(note)
                                ? note
                                : $"项目 {favorite}",
                            $"工程 ID：{favorite}",
                            $"{target.Name} • 已收藏",
                            "查看详情",
                            null))
                        .ToArray(),
                    "当前收藏夹中还没有任何项目。")))
            .ToArray();

        var showWarning = migratedOldFormat || sections.Any(section => section.Entries.Any(item => item.Target is null));
        var warningText = migratedOldFormat
            ? "检测到旧格式收藏夹数据，已按默认收藏夹方式读取。"
            : "当前收藏项缺少在线工程元数据，因此这里只显示本地保存的收藏编号与备注。";

        return new FrontendDownloadFavoritesState(
            targetNames.Length == 0 ? ["默认收藏夹"] : targetNames,
            warningText,
            showWarning && sections.Length > 0,
            sections.Length == 0
                ? [new FrontendDownloadCatalogSection("默认收藏夹", EnsureCatalogEntries([], "当前还没有任何收藏内容。"))]
                : sections);
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> BuildResourceStates(
        FrontendInstanceComposition instanceComposition,
        JsonFileProvider sharedConfig)
    {
        return FrontendCommunityResourceCatalogService.BuildResourceStates(
            instanceComposition,
            ReadValue(sharedConfig, "ToolDownloadMod", 1));
    }

    private static IReadOnlyList<LocalManifestEntry> ReadLocalManifestEntries(string launcherFolder)
    {
        var versionsDirectory = Path.Combine(launcherFolder, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        var results = new List<LocalManifestEntry>();
        foreach (var versionDirectory in Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var versionName = Path.GetFileName(versionDirectory);
            var manifestPath = Path.Combine(versionDirectory, $"{versionName}.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;
                var libraries = root.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array
                    ? librariesElement.EnumerateArray()
                        .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToArray()
                    : [];

                results.Add(new LocalManifestEntry(
                    GetString(root, "id") ?? versionName,
                    GetString(root, "inheritsFrom") ?? versionName,
                    GetString(root, "type") ?? "本地版本",
                    GetDateTime(root, "releaseTime"),
                    manifestPath,
                    ExtractLoaders(libraries)));
            }
            catch
            {
                // Ignore malformed manifests and keep the rest of the launcher folder readable.
            }
        }

        return results;
    }

    private static Dictionary<string, string> ExtractLoaders(IEnumerable<string> libraries)
    {
        var loaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            TryAddLoader(loaders, "forge", library, "net.minecraftforge:forge:");
            TryAddLoader(loaders, "neoforge", library, "net.neoforged:neoforge:");
            TryAddLoader(loaders, "cleanroom", library, "com.cleanroommc:");
            TryAddLoader(loaders, "fabric", library, "net.fabricmc:fabric-loader:");
            TryAddLoader(loaders, "quilt", library, "org.quiltmc:quilt-loader:");
            TryAddLoader(loaders, "legacyfabric", library, "net.legacyfabric:");
            TryAddLoader(loaders, "optifine", library, "optifine:OptiFine:");
            TryAddLoader(loaders, "liteloader", library, "com.mumfrey:liteloader:");
            if (!loaders.ContainsKey("labymod") && library.Contains("labymod", StringComparison.OrdinalIgnoreCase))
            {
                loaders["labymod"] = library.Split(':').LastOrDefault() ?? "已安装";
            }
        }

        return loaders;
    }

    private static void TryAddLoader(
        IDictionary<string, string> loaders,
        string key,
        string library,
        string prefix)
    {
        if (loaders.ContainsKey(key) || !library.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        loaders[key] = library[prefix.Length..];
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> EnsureCatalogEntries(
        IReadOnlyList<FrontendDownloadCatalogEntry> entries,
        string emptyMessage)
    {
        return entries.Count > 0
            ? entries
            : [new FrontendDownloadCatalogEntry("暂无可显示数据", emptyMessage, string.Empty, "查看详情", null)];
    }

    private static IReadOnlyList<FavoriteTarget> ParseFavoriteTargets(string raw, out bool migratedOldFormat)
    {
        migratedOldFormat = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [new FavoriteTarget("默认收藏夹", "default", [], new Dictionary<string, string>())];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Favorite target root must be an array.");
            }

            var targets = document.RootElement
                .EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.Object)
                .Select(ParseFavoriteTarget)
                .Where(target => !string.IsNullOrWhiteSpace(target.Name))
                .ToArray();
            return targets.Length > 0
                ? targets
                : [new FavoriteTarget("默认收藏夹", "default", [], new Dictionary<string, string>())];
        }
        catch
        {
            try
            {
                var favorites = JsonSerializer.Deserialize<HashSet<string>>(raw) ?? [];
                migratedOldFormat = true;
                return [new FavoriteTarget("默认收藏夹", "default", favorites, new Dictionary<string, string>())];
            }
            catch
            {
                return [new FavoriteTarget("默认收藏夹", "default", [], new Dictionary<string, string>())];
            }
        }
    }

    private static string ResolveLauncherFolder(string rawValue, FrontendRuntimePaths runtimePaths)
    {
        var normalized = string.IsNullOrWhiteSpace(rawValue)
            ? "$.minecraft\\"
            : rawValue.Trim();
        normalized = normalized.Replace("$", EnsureTrailingSeparator(runtimePaths.ExecutableDirectory), StringComparison.Ordinal);
        return Path.GetFullPath(normalized);
    }

    private static string ResolvePreferredMinecraftVersion(FrontendInstanceComposition instanceComposition)
    {
        var installVersion = instanceComposition.Install.MinecraftVersion;
        if (!string.IsNullOrWhiteSpace(installVersion) && installVersion.Any(char.IsDigit))
        {
            return installVersion.Trim();
        }

        var selectionVersion = instanceComposition.Selection.VanillaVersion;
        return !string.IsNullOrWhiteSpace(selectionVersion) && selectionVersion.Any(char.IsDigit)
            ? selectionVersion.Trim()
            : string.Empty;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string GetDisplayLoaderName(string loaderKey)
    {
        return loaderKey switch
        {
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "cleanroom" => "Cleanroom",
            "fabric" => "Fabric",
            "quilt" => "Quilt",
            "legacyfabric" => "Legacy Fabric",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            "labymod" => "LabyMod",
            _ => loaderKey
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static FavoriteTarget ParseFavoriteTarget(JsonElement element)
    {
        var name = GetString(element, "Name") ?? "默认收藏夹";
        var id = GetString(element, "Id") ?? "default";
        var favorites = ParseFavoriteIds(element);
        var notes = ParseFavoriteNotes(element);
        return new FavoriteTarget(name, id, favorites, notes);
    }

    private static IReadOnlyCollection<string> ParseFavoriteIds(JsonElement element)
    {
        if (!element.TryGetProperty("Favs", out var favoritesElement)
            && !element.TryGetProperty("Favorites", out favoritesElement))
        {
            return [];
        }

        return favoritesElement.ValueKind switch
        {
            JsonValueKind.Array => favoritesElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .OfType<string>()
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            JsonValueKind.Object => favoritesElement
                .EnumerateObject()
                .Select(property => property.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => []
        };
    }

    private static IReadOnlyDictionary<string, string> ParseFavoriteNotes(JsonElement element)
    {
        if (!element.TryGetProperty("Notes", out var notesElement)
            || notesElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return notesElement
            .EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTime.TryParse(value.GetString(), out var result)
            ? result
            : null;
    }

    private sealed record FavoriteTarget(
        string Name,
        string Id,
        IReadOnlyCollection<string> Favorites,
        IReadOnlyDictionary<string, string> Notes);

    private sealed record LocalManifestEntry(
        string Id,
        string VanillaVersion,
        string Type,
        DateTime? ReleaseTime,
        string SourcePath,
        IReadOnlyDictionary<string, string> Loaders);
}

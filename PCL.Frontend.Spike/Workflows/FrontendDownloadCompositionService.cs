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
        var localConfig = new YamlFileProvider(runtimePaths.LocalConfigPath);
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        var launcherFolder = ResolveLauncherFolder(ReadValue(localConfig, "LaunchFolderSelect", "$.minecraft\\"), runtimePaths);
        var manifests = ReadLocalManifestEntries(launcherFolder);

        return new FrontendDownloadComposition(
            BuildInstallState(instanceComposition),
            BuildCatalogStates(manifests),
            BuildFavoritesState(sharedConfig),
            BuildResourceStates(instanceComposition, versionSavesComposition, launcherFolder));
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
        IReadOnlyList<LocalManifestEntry> manifests)
    {
        return new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>
        {
            [LauncherFrontendSubpageKey.DownloadClient] = BuildClientCatalogState(manifests),
            [LauncherFrontendSubpageKey.DownloadForge] = BuildLoaderCatalogState(manifests, "forge", "Forge 简介", "https://files.minecraftforge.net/"),
            [LauncherFrontendSubpageKey.DownloadNeoForge] = BuildLoaderCatalogState(manifests, "neoforge", "NeoForge 简介", "https://neoforged.net/"),
            [LauncherFrontendSubpageKey.DownloadCleanroom] = BuildLoaderCatalogState(manifests, "cleanroom", "Cleanroom 简介", "https://github.com/CleanroomMC/Cleanroom"),
            [LauncherFrontendSubpageKey.DownloadFabric] = BuildLoaderCatalogState(manifests, "fabric", "Fabric 简介", "https://fabricmc.net/"),
            [LauncherFrontendSubpageKey.DownloadQuilt] = BuildLoaderCatalogState(manifests, "quilt", "Quilt 简介", "https://quiltmc.org/"),
            [LauncherFrontendSubpageKey.DownloadLiteLoader] = BuildLoaderCatalogState(manifests, "liteloader", "LiteLoader 简介", string.Empty),
            [LauncherFrontendSubpageKey.DownloadLabyMod] = BuildLoaderCatalogState(manifests, "labymod", "LabyMod 简介", "https://www.labymod.net/"),
            [LauncherFrontendSubpageKey.DownloadLegacyFabric] = BuildLoaderCatalogState(manifests, "legacyfabric", "Legacy Fabric 简介", "https://legacyfabric.net/"),
            [LauncherFrontendSubpageKey.DownloadOptiFine] = BuildLoaderCatalogState(manifests, "optifine", "OptiFine 简介", "https://optifine.net/")
        };
    }

    private static FrontendDownloadCatalogState BuildClientCatalogState(IReadOnlyList<LocalManifestEntry> manifests)
    {
        var entries = manifests
            .OrderByDescending(entry => entry.ReleaseTime ?? DateTime.MinValue)
            .Take(12)
            .Select(entry => new FrontendDownloadCatalogEntry(
                entry.Id,
                entry.ReleaseTime?.ToString("yyyy/MM/dd HH:mm") ?? "未记录发布时间",
                $"{(string.IsNullOrWhiteSpace(entry.Type) ? "本地版本" : entry.Type)} • {entry.SourcePath}",
                "查看文件",
                entry.SourcePath))
            .ToArray();

        return new FrontendDownloadCatalogState(
            "版本列表",
            "这里现在优先显示当前启动器目录下已经识别到的本地版本清单，便于在不改变页面结构的前提下验证真实版本数据绑定。",
            [],
            [new FrontendDownloadCatalogSection("本地版本", EnsureCatalogEntries(entries, "当前没有检测到任何本地版本。"))]);
    }

    private static FrontendDownloadCatalogState BuildLoaderCatalogState(
        IReadOnlyList<LocalManifestEntry> manifests,
        string loaderKey,
        string introTitle,
        string projectUrl)
    {
        var entries = manifests
            .Where(entry => entry.Loaders.TryGetValue(loaderKey, out _))
            .Select(entry => new FrontendDownloadCatalogEntry(
                $"{GetDisplayLoaderName(loaderKey)} {entry.Loaders[loaderKey]}",
                $"来自本地版本 {entry.Id}",
                $"{(entry.VanillaVersion ?? entry.Id)} • {entry.SourcePath}",
                "查看文件",
                entry.SourcePath))
            .DistinctBy(entry => entry.Title + "|" + entry.Meta)
            .Take(12)
            .ToArray();

        var actions = string.IsNullOrWhiteSpace(projectUrl)
            ? Array.Empty<FrontendDownloadCatalogAction>()
            : [new FrontendDownloadCatalogAction("打开官网", projectUrl, true)];
        return new FrontendDownloadCatalogState(
            introTitle,
            $"这里现在优先列出当前启动器已识别到的本地 {GetDisplayLoaderName(loaderKey)} 版本记录，以便在复制后的下载页中验证真实版本数据来源。",
            actions,
            [new FrontendDownloadCatalogSection("本地已识别版本", EnsureCatalogEntries(entries, $"当前没有检测到任何 {GetDisplayLoaderName(loaderKey)} 本地版本记录。"))]);
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
        FrontendVersionSavesComposition versionSavesComposition,
        string launcherFolder)
    {
        return new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>
        {
            [LauncherFrontendSubpageKey.DownloadMod] = BuildResourceState(
                "Mod",
                true,
                false,
                false,
                "当前列表来自所选实例内已安装的 Mod 文件，可用于验证复制后的搜索、筛选与分页控件。",
                BuildDefaultTagOptions("已启用", "已禁用"),
                instanceComposition.Mods.Entries.Select(entry => ToResourceEntry(entry, "当前实例", instanceComposition.Selection.VanillaVersion, ExtractLoader(entry.Meta))).ToArray()),
            [LauncherFrontendSubpageKey.DownloadPack] = BuildResourceState(
                "整合包",
                false,
                true,
                false,
                "当前列表来自启动器目录下已存在的实例版本，用于在不改变页面结构的前提下验证真实整合包列表。",
                BuildDefaultTagOptions("本地实例"),
                BuildInstancePackEntries(launcherFolder)),
            [LauncherFrontendSubpageKey.DownloadDataPack] = BuildResourceState(
                "数据包",
                false,
                false,
                false,
                "当前列表来自所选存档的 datapacks 文件夹。",
                BuildDefaultTagOptions("数据包"),
                versionSavesComposition.Datapacks.Select(entry => new FrontendDownloadResourceEntry(
                    entry.Title,
                    entry.Summary,
                    "当前存档",
                    instanceComposition.Selection.VanillaVersion,
                    string.Empty,
                    ["数据包"],
                    "查看详情",
                    entry.IconName,
                    entry.Path,
                    0,
                    0,
                    0,
                    0)).ToArray()),
            [LauncherFrontendSubpageKey.DownloadResourcePack] = BuildResourceState(
                "资源包",
                false,
                false,
                false,
                "当前列表来自所选实例的 resourcepacks 文件夹。",
                BuildDefaultTagOptions("资源包"),
                instanceComposition.ResourcePacks.Entries.Select(entry => ToResourceEntry(entry, "当前实例", instanceComposition.Selection.VanillaVersion, ExtractLoader(entry.Meta))).ToArray()),
            [LauncherFrontendSubpageKey.DownloadShader] = BuildResourceState(
                "光影包",
                false,
                false,
                true,
                "当前列表来自所选实例的 shaderpacks 文件夹。",
                BuildDefaultTagOptions("光影包"),
                instanceComposition.Shaders.Entries.Select(entry => ToResourceEntry(entry, "当前实例", instanceComposition.Selection.VanillaVersion, ExtractLoader(entry.Meta))).ToArray()),
            [LauncherFrontendSubpageKey.DownloadWorld] = BuildResourceState(
                "世界",
                false,
                false,
                false,
                "当前列表来自所选实例的 saves 文件夹。",
                BuildDefaultTagOptions("世界"),
                instanceComposition.World.Entries.Select(entry => new FrontendDownloadResourceEntry(
                    entry.Title,
                    entry.Summary,
                    "当前实例",
                    instanceComposition.Selection.VanillaVersion,
                    string.Empty,
                    ["世界"],
                    "查看详情",
                    "GrassPath.png",
                    entry.Path,
                    0,
                    0,
                    0,
                    0)).ToArray())
        };
    }

    private static FrontendDownloadResourceState BuildResourceState(
        string title,
        bool supportsSecondarySource,
        bool showInstallModPackAction,
        bool useShaderLoaderOptions,
        string hintText,
        IReadOnlyList<FrontendDownloadResourceFilterOption> tagOptions,
        IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return new FrontendDownloadResourceState(
            $"{title} 列表",
            supportsSecondarySource,
            showInstallModPackAction,
            useShaderLoaderOptions,
            hintText,
            tagOptions,
            entries);
    }

    private static IReadOnlyList<FrontendDownloadResourceEntry> BuildInstancePackEntries(string launcherFolder)
    {
        var versionsDirectory = Path.Combine(launcherFolder, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => new FrontendDownloadResourceEntry(
                directory.Name,
                $"{directory.LastWriteTime:yyyy/MM/dd HH:mm} • 本地实例目录",
                "当前启动器",
                string.Empty,
                string.Empty,
                ["本地实例"],
                "查看详情",
                "CommandBlock.png",
                directory.FullName,
                0,
                0,
                0,
                0))
            .ToArray();
    }

    private static FrontendDownloadResourceEntry ToResourceEntry(
        FrontendInstanceResourceEntry entry,
        string source,
        string version,
        string loader)
    {
        var tags = entry.Meta.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new FrontendDownloadResourceEntry(
            entry.Title,
            entry.Summary,
            source,
            version,
            loader,
            tags.Length == 0 ? ["本地内容"] : tags,
            "查看详情",
            entry.IconName,
            entry.Path,
            0,
            0,
            0,
            0);
    }

    private static string ExtractLoader(string meta)
    {
        return meta.Contains("Forge", StringComparison.OrdinalIgnoreCase) ? "Forge"
            : meta.Contains("NeoForge", StringComparison.OrdinalIgnoreCase) ? "NeoForge"
            : meta.Contains("Fabric", StringComparison.OrdinalIgnoreCase) ? "Fabric"
            : meta.Contains("Quilt", StringComparison.OrdinalIgnoreCase) ? "Quilt"
            : meta.Contains("OptiFine", StringComparison.OrdinalIgnoreCase) ? "OptiFine"
            : string.Empty;
    }

    private static IReadOnlyList<FrontendDownloadResourceFilterOption> BuildDefaultTagOptions(params string[] tags)
    {
        return
        [
            new FrontendDownloadResourceFilterOption("全部", string.Empty),
            .. tags
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new FrontendDownloadResourceFilterOption(tag, tag))
        ];
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

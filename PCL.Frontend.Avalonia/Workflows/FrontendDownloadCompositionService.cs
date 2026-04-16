using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDownloadCompositionService
{
    public static FrontendDownloadComposition ComposeBootstrap(
        FrontendRuntimePaths runtimePaths,
        FrontendInstanceComposition instanceComposition,
        II18nService? i18n = null)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return new FrontendDownloadComposition(
            BuildInstallState(instanceComposition),
            new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>(),
            BuildBootstrapFavoritesState(sharedConfig),
            new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>());
    }

    public static FrontendDownloadComposition Compose(
        FrontendRuntimePaths runtimePaths,
        FrontendInstanceComposition instanceComposition,
        FrontendVersionSavesComposition versionSavesComposition,
        II18nService? i18n = null)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var preferredMinecraftVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var versionSourceIndex = ReadValue(sharedConfig, "ToolDownloadVersion", 1);
        var communitySourcePreference = ReadValue(sharedConfig, "ToolDownloadMod", 1);

        return new FrontendDownloadComposition(
            BuildInstallState(instanceComposition),
            BuildCatalogStates(versionSourceIndex, preferredMinecraftVersion, i18n),
            BuildFavoritesState(sharedConfig, communitySourcePreference),
            BuildResourceStates(instanceComposition, communitySourcePreference));
    }

    public static Task<FrontendDownloadCatalogState> LoadCatalogStateAsync(
        FrontendRuntimePaths runtimePaths,
        FrontendInstanceComposition instanceComposition,
        LauncherFrontendSubpageKey route,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var preferredMinecraftVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var versionSourceIndex = ReadValue(sharedConfig, "ToolDownloadVersion", 1);
        return FrontendDownloadRemoteCatalogService.LoadCatalogStateAsync(
            route,
            versionSourceIndex,
            preferredMinecraftVersion,
            i18n,
            cancellationToken);
    }

    public static Task<IReadOnlyList<FrontendDownloadCatalogEntry>> LoadCatalogSectionEntriesAsync(
        FrontendRuntimePaths runtimePaths,
        FrontendInstanceComposition instanceComposition,
        LauncherFrontendSubpageKey route,
        string lazyLoadToken,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var preferredMinecraftVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var versionSourceIndex = ReadValue(sharedConfig, "ToolDownloadVersion", 1);
        return FrontendDownloadRemoteCatalogService.LoadCatalogSectionEntriesAsync(
            route,
            lazyLoadToken,
            versionSourceIndex,
            preferredMinecraftVersion,
            i18n,
            cancellationToken);
    }

    public static Task<FrontendDownloadFavoritesState> LoadFavoritesStateAsync(
        FrontendRuntimePaths runtimePaths,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sharedConfig = runtimePaths.OpenSharedConfigProvider();
            var communitySourcePreference = ReadValue(sharedConfig, "ToolDownloadMod", 1);
            return BuildFavoritesState(sharedConfig, communitySourcePreference);
        }, cancellationToken);
    }

    private static FrontendDownloadInstallState BuildInstallState(FrontendInstanceComposition instanceComposition)
    {
        var selectionName = string.IsNullOrWhiteSpace(instanceComposition.Selection.InstanceName)
            ? "New install plan"
            : instanceComposition.Selection.InstanceName;
        return new FrontendDownloadInstallState(
            selectionName,
            instanceComposition.Install.MinecraftVersion,
            instanceComposition.Install.MinecraftIconName,
            instanceComposition.Install.Hints,
            instanceComposition.Install.Options
                .Select(option => new FrontendDownloadInstallOption(option.Title, option.SelectionState, option.IconName))
                .ToArray());
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildCatalogStates(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        return FrontendDownloadRemoteCatalogService.BuildCatalogStates(versionSourceIndex, preferredMinecraftVersion, i18n);
    }

    private static FrontendDownloadFavoritesState BuildFavoritesState(
        JsonFileProvider sharedConfig,
        int communitySourcePreference)
    {
        var raw = ReadValue(sharedConfig, "CompFavorites", "[]");
        var targets = ParseFavoriteTargets(raw, out var migratedOldFormat);
        var lookup = FrontendCommunityProjectService.LookupProjects(
            targets.SelectMany(target => target.Favorites),
            communitySourcePreference);
        var targetStates = targets
            .Select(target => new FrontendDownloadFavoriteTargetState(
                target.Name,
                target.Id,
                BuildFavoriteSections(target, lookup.Projects)))
            .ToArray();

        var unresolvedCount = targets.Sum(target => target.Favorites.Count(favorite => !lookup.Projects.ContainsKey(favorite)));
        var warnings = new List<string>();
        if (migratedOldFormat)
        {
            warnings.Add("Legacy favorite data was detected and has been read as the default favorites target.");
        }

        if (unresolvedCount > 0)
        {
            warnings.Add($"{unresolvedCount} favorites could not be resolved from online metadata for now, so the locally stored project IDs were kept.");
        }

        warnings.AddRange(lookup.Errors);
        var warningText = string.Join(" ", warnings.Distinct(StringComparer.Ordinal));
        var hasEntries = targetStates.Any(target => target.Sections.Any(section => section.Entries.Count > 0));
        var showWarning = warnings.Count > 0 && hasEntries;

        return new FrontendDownloadFavoritesState(
            targetStates.Length == 0
                ? [new FrontendDownloadFavoriteTargetState("Default favorites", "default", [])]
                : targetStates,
            warningText,
            showWarning);
    }

    private static FrontendDownloadFavoritesState BuildBootstrapFavoritesState(JsonFileProvider sharedConfig)
    {
        var raw = ReadValue(sharedConfig, "CompFavorites", "[]");
        var targets = ParseFavoriteTargets(raw, out var migratedOldFormat);
        var emptyLookup = new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        var targetStates = targets
            .Select(target => new FrontendDownloadFavoriteTargetState(
                target.Name,
                target.Id,
                BuildFavoriteSections(target, emptyLookup)))
            .ToArray();
        var hasEntries = targetStates.Any(target => target.Sections.Any(section => section.Entries.Count > 0));

        return new FrontendDownloadFavoritesState(
            targetStates.Length == 0
                ? [new FrontendDownloadFavoriteTargetState("Default favorites", "default", [])]
                : targetStates,
            migratedOldFormat ? "Legacy favorite data was detected and has been read as the default favorites target." : string.Empty,
            migratedOldFormat && hasEntries);
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> BuildResourceStates(
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference)
    {
        return FrontendCommunityResourceCatalogService.BuildResourceStates(
            instanceComposition,
            communitySourcePreference);
    }

    private static FrontendDownloadCatalogEntry CreateFavoriteEntry(
        FavoriteTarget target,
        string favorite,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> projects)
    {
        var note = target.Notes.TryGetValue(favorite, out var storedNote) && !string.IsNullOrWhiteSpace(storedNote)
            ? storedNote.Trim()
            : null;
        if (projects.TryGetValue(favorite, out var project))
        {
            var infoParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(note))
            {
                infoParts.Add(note);
            }

            if (!string.IsNullOrWhiteSpace(project.Summary))
            {
                infoParts.Add(project.Summary);
            }

            return new FrontendDownloadCatalogEntry(
                project.Title,
                string.Join(" • ", infoParts.Where(part => !string.IsNullOrWhiteSpace(part))),
                BuildFavoriteMeta(
                    target.Name,
                    project.Source,
                    project.ProjectType,
                    project.Author,
                    project.UpdatedLabel,
                    project.DownloadCount),
                "view_details",
                FrontendCommunityProjectService.CreateCompDetailTarget(project.ProjectId),
                Identity: project.ProjectId,
                IconUrl: project.IconUrl,
                IconPath: project.IconPath,
                IconName: ResolveFavoriteIconName(project.ProjectType),
                OriginSubpage: ResolveFavoriteOriginSubpage(project.ProjectType));
        }

        return new FrontendDownloadCatalogEntry(
            note ?? $"Project {favorite}",
            $"Project ID: {favorite}",
            $"Favorite target {target.Name} • Metadata not resolved",
            "view_details",
            FrontendCommunityProjectService.CreateCompDetailTarget(favorite),
            Identity: favorite,
            IconName: ResolveFavoriteIconName(null),
            OriginSubpage: null);
    }

    private static IReadOnlyList<FrontendDownloadCatalogSection> BuildFavoriteSections(
        FavoriteTarget target,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> projects)
    {
        var groupedEntries = target.Favorites
            .Select(favorite => CreateFavoriteEntry(target, favorite, projects))
            .GroupBy(entry => ResolveFavoriteCategory(entry, projects), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetFavoriteCategorySortOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new FrontendDownloadCatalogSection(
                group.Key,
                group.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray()))
            .ToArray();
        return groupedEntries;
    }

    private static string ResolveFavoriteCategory(
        FrontendDownloadCatalogEntry entry,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> projects)
    {
        if (!string.IsNullOrWhiteSpace(entry.Identity)
            && projects.TryGetValue(entry.Identity, out var project)
            && !string.IsNullOrWhiteSpace(project.ProjectType))
        {
            return project.ProjectType!;
        }

        return "other";
    }

    private static int GetFavoriteCategorySortOrder(string category)
    {
        return category switch
        {
            "mod" => 0,
            "data_pack" => 1,
            "modpack" => 2,
            "resource_pack" => 3,
            "shader" => 4,
            "world" => 5,
            "other" => 99,
            _ => 50
        };
    }

    private static string ResolveFavoriteIconName(string? projectType)
    {
        return projectType switch
        {
            "mod" => "CommandBlock.png",
            "modpack" => "CommandBlock.png",
            "data_pack" => "RedstoneLampOn.png",
            "resource_pack" => "Grass.png",
            "shader" => "GoldBlock.png",
            "world" => "GrassPath.png",
            _ => "Grass.png"
        };
    }

    private static LauncherFrontendSubpageKey? ResolveFavoriteOriginSubpage(string? projectType)
    {
        return projectType switch
        {
            "mod" => LauncherFrontendSubpageKey.DownloadMod,
            "modpack" => LauncherFrontendSubpageKey.DownloadPack,
            "data_pack" => LauncherFrontendSubpageKey.DownloadDataPack,
            "resource_pack" => LauncherFrontendSubpageKey.DownloadResourcePack,
            "shader" => LauncherFrontendSubpageKey.DownloadShader,
            "world" => LauncherFrontendSubpageKey.DownloadWorld,
            _ => null
        };
    }

    private static string BuildFavoriteMeta(
        string targetName,
        string source,
        string? projectType,
        string? author,
        string? updatedLabel,
        int downloadCount)
    {
        static string Escape(string? value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        return string.Join(
            "|",
            new[]
            {
                "favorite_meta",
                Escape(targetName),
                Escape(source),
                Escape(projectType),
                Escape(author),
                Escape(updatedLabel),
                Escape(downloadCount > 0 ? downloadCount.ToString() : string.Empty)
            });
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
                var profile = FrontendVersionManifestInspector.ReadProfile(launcherFolder, versionName);
                var loaderMap = FrontendVersionManifestInspector.CreateLoaderMap(profile);

                results.Add(new LocalManifestEntry(
                    versionName,
                    profile.VanillaVersion,
                    profile.VersionType ?? "Local version",
                    profile.ReleaseTime,
                    manifestPath,
                    loaderMap));
            }
            catch
            {
                // Ignore malformed manifests and keep the rest of the launcher folder readable.
            }
        }

        return results;
    }
    private static IReadOnlyList<FavoriteTarget> ParseFavoriteTargets(string raw, out bool migratedOldFormat)
    {
        migratedOldFormat = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [new FavoriteTarget("Default favorites", "default", [], new Dictionary<string, string>())];
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
                : [new FavoriteTarget("Default favorites", "default", [], new Dictionary<string, string>())];
        }
        catch
        {
            try
            {
                var favorites = JsonSerializer.Deserialize<HashSet<string>>(raw) ?? [];
                migratedOldFormat = true;
                return [new FavoriteTarget("Default favorites", "default", favorites, new Dictionary<string, string>())];
            }
            catch
            {
                return [new FavoriteTarget("Default favorites", "default", [], new Dictionary<string, string>())];
            }
        }
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

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#} hundred-million",
            >= 10_000 => $"{value / 10_000d:0.#} ten-thousand",
            _ => value.ToString()
        };
    }

    private static FavoriteTarget ParseFavoriteTarget(JsonElement element)
    {
        var name = GetString(element, "Name") ?? "Default favorites";
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

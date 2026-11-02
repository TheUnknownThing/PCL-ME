using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

    private static SourceFetchResult FetchCurseForgeEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference,
        FrontendCommunityResourceQuery query,
        int offset)
    {
        try
        {
            var officialUrl = BuildCurseForgeSearchUrl(config, preferredVersion, query, useMirror: false, offset);
            var mirrorUrl = BuildCurseForgeSearchUrl(config, preferredVersion, query, useMirror: true, offset);
            var response = ReadJsonObject("CurseForge", officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey: true);
            var data = response["data"]?.AsArray() ?? [];
            var pagination = response["pagination"] as JsonObject;
            var resultCount = GetInt(pagination, "resultCount");
            var totalCount = GetInt(pagination, "totalCount");
            var currentIndex = GetInt(pagination, "index");

            var entries = data
                .Select(item => item as JsonObject)
                .Where(item => item is not null)
                .Select(item => BuildCurseForgeEntry(config, item!, preferredVersion))
                .Where(entry => entry is not null)
                .Cast<FrontendDownloadResourceEntry>()
                .ToArray();

            var receivedCount = resultCount > 0 ? resultCount : data.Count;
            var hasMoreEntries = receivedCount > 0
                && (totalCount <= 0 || currentIndex + receivedCount < totalCount);
            return new SourceFetchResult(entries, null, receivedCount, totalCount > 0 ? totalCount : null, hasMoreEntries);
        }
        catch (Exception ex)
        {
            return new SourceFetchResult([], $"CurseForge is temporarily unavailable: {ex.Message}", 0, null, false);
        }
    }

    private static FrontendDownloadResourceEntry? BuildCurseForgeEntry(
        RouteConfig config,
        JsonObject item,
        string? preferredVersion)
    {
        var title = GetString(item, "name");
        var projectId = GetInt(item, "id");
        if (string.IsNullOrWhiteSpace(title) || projectId <= 0)
        {
            return null;
        }

        var categories = item["categories"] as JsonArray ?? [];
        if (config.Route == LauncherFrontendSubpageKey.DownloadResourcePack
            && categories.Any(category => GetInt(category as JsonObject, "id") == 5193))
        {
            return null;
        }

        var translatedTags = categories
            .Select(category => TranslateCurseForgeCategory(GetInt(category as JsonObject, "id")))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var latestFiles = item["latestFilesIndexes"] as JsonArray ?? [];
        var primaryFile = SelectCurseForgeFileIndex(latestFiles, preferredVersion);
        var normalizedVersions = latestFiles
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node => NormalizeMinecraftVersion(GetString(node!, "gameVersion")))
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ParseVersion)
            .ToArray();
        var loaders = config.UseShaderLoaderOptions
            ? [.. ResolveSupportedLoaders(translatedTags, true)]
            : latestFiles
                .Select(node => node as JsonObject)
                .Where(node => node is not null)
                .Select(node => ResolveCurseForgeLoader(node!, config.UseShaderLoaderOptions, translatedTags))
                .Where(loader => !string.IsNullOrWhiteSpace(loader))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var website = GetString(item["links"] as JsonObject, "websiteUrl");
        if (string.IsNullOrWhiteSpace(website))
        {
            var slug = GetString(item, "slug");
            if (!string.IsNullOrWhiteSpace(slug))
            {
                website = $"https://www.curseforge.com/minecraft/{config.CurseForgeSectionPath}/{slug}";
            }
        }

        var downloads = GetInt(item, "downloadCount");
        var releasedAt = ParseDateTimeOffset(item["dateReleased"]);
        var updatedAt = ParseDateTimeOffset(item["dateModified"]) ?? releasedAt;
        var logo = item["logo"] as JsonObject;
        var iconUrl = GetString(logo, "thumbnailUrl") ?? GetString(logo, "url");
        var iconPath = FrontendCommunityIconCache.TryGetCachedIconPath(iconUrl);

        return new FrontendDownloadResourceEntry(
            title,
            BuildEntryInfo(GetString(item, "summary"), null, updatedAt, downloads),
            "CurseForge",
            primaryFile is null ? string.Empty : GetString(primaryFile, "gameVersion"),
            primaryFile is null ? ResolveShaderLoaderFromTags(translatedTags) : ResolveCurseForgeLoader(primaryFile, config.UseShaderLoaderOptions, translatedTags),
            translatedTags.Length == 0 ? [config.Title] : translatedTags,
            normalizedVersions,
            loaders,
            "view_details",
            iconUrl,
            iconPath,
            config.IconName,
            FrontendCommunityProjectService.CreateCompDetailTarget(projectId.ToString()),
            downloads,
            0,
            ToRank(releasedAt),
            ToRank(updatedAt));
    }

    private static string BuildCurseForgeSearchUrl(
        RouteConfig config,
        string? preferredVersion,
        FrontendCommunityResourceQuery query,
        bool useMirror,
        int offset = 0)
    {
        var baseUrl = useMirror
            ? "https://mod.mcimirror.top/curseforge/v1/mods/search"
            : "https://api.curseforge.com/v1/mods/search";
        var parameters = new List<string>
        {
            "gameId=432",
            $"classId={config.CurseForgeClassId}",
            $"pageSize={SearchPageSize}",
            "sortOrder=desc"
        };
        if (offset > 0)
        {
            parameters.Add($"index={offset}");
        }

        parameters.Add(query.Sort switch
        {
            "relevance" => "sortField=4",
            "downloads" => "sortField=6",
            "follows" => "sortField=2",
            "release" => "sortField=11",
            "update" => "sortField=3",
            _ => string.IsNullOrWhiteSpace(query.SearchText) ? "sortField=6" : "sortField=4"
        });

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            parameters.Add($"gameVersion={Uri.EscapeDataString(preferredVersion)}");
        }

        var categoryId = MapTagToCurseForgeCategory(config.Route, query.Tag);
        if (categoryId is not null)
        {
            parameters.Add($"categoryId={categoryId.Value}");
        }

        var modLoaderType = MapLoaderToCurseForgeLoaderType(config.Route, query.Loader);
        if (modLoaderType is not null)
        {
            parameters.Add($"modLoaderType={modLoaderType.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            parameters.Add($"searchFilter={Uri.EscapeDataString(query.SearchText)}");
        }

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }

    private static JsonObject? SelectCurseForgeFileIndex(JsonArray latestFiles, string? preferredVersion)
    {
        var entries = latestFiles
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .Where(node => !string.IsNullOrWhiteSpace(NormalizeMinecraftVersion(GetString(node, "gameVersion"))))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var preferredMatch = entries.FirstOrDefault(entry =>
                string.Equals(NormalizeMinecraftVersion(GetString(entry, "gameVersion")), preferredVersion, StringComparison.OrdinalIgnoreCase));
            if (preferredMatch is not null)
            {
                return preferredMatch;
            }
        }

        return entries
            .OrderByDescending(entry => ParseVersion(NormalizeMinecraftVersion(GetString(entry, "gameVersion"))))
            .FirstOrDefault();
    }

    private static string ResolveCurseForgeLoader(JsonObject fileIndex, bool useShaderLoaderOptions, IReadOnlyList<string> tags)
    {
        if (useShaderLoaderOptions)
        {
            return ResolveShaderLoaderFromTags(tags);
        }

        return GetInt(fileIndex, "modLoader") switch
        {
            1 => "Forge",
            4 => "Fabric",
            5 => "Quilt",
            6 => "NeoForge",
            _ => string.Empty
        };
    }

    private static string TranslateCurseForgeCategory(int categoryId)
    {
        return categoryId switch
        {
            406 => "worldgen",
            407 => "biomes",
            410 => "dimensions",
            408 => "ores_resources",
            409 => "structures",
            412 => "technology",
            415 => "logistics",
            4843 => "automation",
            417 => "energy",
            4558 => "redstone",
            436 => "food_cooking",
            416 => "farming",
            414 => "transportation",
            420 => "storage",
            419 => "magic",
            422 => "adventure",
            424 => "decoration",
            411 => "mobs",
            434 => "equipment_tools",
            6814 => "performance",
            9026 => "creative_mode",
            423 => "information",
            435 => "multiplayer",
            5191 => "tweaks",
            421 => "library",
            4484 => "multiplayer",
            4479 => "hardcore",
            4483 => "combat",
            4478 => "quests",
            4472 => "technology",
            4473 => "magic",
            4475 => "adventure",
            4476 => "exploration",
            4477 => "minigames",
            4471 => "scifi",
            4736 => "skyblock",
            5128 => "vanilla_plus",
            4487 => "FTB",
            4480 => "map_based",
            4481 => "lightweight",
            4482 => "large",
            403 => "vanilla_style",
            400 => "realistic",
            401 => "modern",
            402 => "medieval",
            399 => "steampunk",
            5244 => "fonts",
            404 => "dynamic_effects",
            4465 => "mod_compatible",
            393 => "16x",
            394 => "32x",
            395 => "64x",
            396 => "128x",
            397 => "256x",
            398 => "resolution_512x",
            5193 => "data_pack",
            6553 => "realistic",
            6554 => "fantasy_style",
            6555 => "vanilla_style",
            6948 => "adventure",
            6949 => "fantasy",
            6950 => "library",
            6952 => "magic",
            6946 => "modded",
            6951 => "technology",
            6953 => "utility",
            248 => "adventure",
            249 => "creative",
            250 => "minigames",
            251 => "parkour",
            252 => "puzzle",
            253 => "survival",
            4464 => "mod_world",
            _ => string.Empty
        };
    }

}

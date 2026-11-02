using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

    private static SourceFetchResult FetchModrinthEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference,
        FrontendCommunityResourceQuery query,
        int offset)
    {
        try
        {
            var officialUrl = BuildModrinthSearchUrl(config, preferredVersion, query, useMirror: false, offset);
            var mirrorUrl = BuildModrinthSearchUrl(config, preferredVersion, query, useMirror: true, offset);
            var response = ReadJsonObject("Modrinth", officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey: false);
            var hits = response["hits"]?.AsArray() ?? [];
            var totalHits = GetInt(response, "total_hits");

            var entries = hits
                .Select(hit => hit as JsonObject)
                .Where(hit => hit is not null)
                .Select(hit => BuildModrinthEntry(config, hit!, preferredVersion))
                .Where(entry => entry is not null)
                .Cast<FrontendDownloadResourceEntry>()
                .ToArray();

            var receivedCount = hits.Count;
            var hasMoreEntries = receivedCount > 0
                && (totalHits <= 0 || offset + receivedCount < totalHits);
            return new SourceFetchResult(entries, null, receivedCount, totalHits > 0 ? totalHits : null, hasMoreEntries);
        }
        catch (Exception ex)
        {
            return new SourceFetchResult([], $"Modrinth is temporarily unavailable: {ex.Message}", 0, null, false);
        }
    }

    private static FrontendDownloadResourceEntry? BuildModrinthEntry(
        RouteConfig config,
        JsonObject hit,
        string? preferredVersion)
    {
        var title = GetString(hit, "title");
        var slug = GetString(hit, "slug");
        var projectId = GetString(hit, "project_id") ?? slug;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        var projectType = GetString(hit, "project_type");
        var rawCategories = (hit["display_categories"] as JsonArray ?? hit["categories"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var translatedTags = TranslateModrinthCategories(rawCategories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var versions = (hit["versions"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var normalizedVersions = versions
            .Select(NormalizeMinecraftVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ParseVersion)
            .ToArray();
        var loaders = ResolveSupportedLoaders(rawCategories, config.UseShaderLoaderOptions);

        var downloads = GetInt(hit, "downloads");
        var follows = GetInt(hit, "follows");
        var createdAt = ParseDateTimeOffset(hit["date_created"]);
        var updatedAt = ParseDateTimeOffset(hit["date_modified"]);
        var summary = BuildEntryInfo(
            GetString(hit, "description"),
            GetString(hit, "author"),
            updatedAt,
            downloads);
        var iconUrl = GetString(hit, "icon_url");
        var iconPath = FrontendCommunityIconCache.TryGetCachedIconPath(iconUrl);

        return new FrontendDownloadResourceEntry(
            title,
            summary,
            "Modrinth",
            ResolvePrimaryVersion(normalizedVersions, preferredVersion),
            ResolvePrimaryLoader(rawCategories, config.UseShaderLoaderOptions),
            translatedTags.Length == 0 ? [config.Title] : translatedTags,
            normalizedVersions,
            loaders,
            "view_details",
            iconUrl,
            iconPath,
            config.IconName,
            FrontendCommunityProjectService.CreateCompDetailTarget(projectId),
            downloads,
            follows,
            ToRank(createdAt),
            ToRank(updatedAt));
    }

    private static string BuildModrinthSearchUrl(RouteConfig config, string? preferredVersion, bool useMirror)
    {
        return BuildModrinthSearchUrl(
            config,
            preferredVersion,
            new FrontendCommunityResourceQuery(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            useMirror);
    }

    private static string BuildModrinthSearchUrl(
        RouteConfig config,
        string? preferredVersion,
        FrontendCommunityResourceQuery query,
        bool useMirror,
        int offset = 0)
    {
        var baseUrl = useMirror
            ? "https://mod.mcimirror.top/modrinth/v2/search"
            : "https://api.modrinth.com/v2/search";
        var facets = new List<string> { $"[\"project_type:{config.ModrinthProjectType}\"]" };

        if (!string.IsNullOrWhiteSpace(config.ModrinthCategory))
        {
            facets.Add($"[\"categories:{config.ModrinthCategory}\"]");
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            facets.Add($"[\"versions:{preferredVersion}\"]");
        }

        var mappedTag = MapTagToModrinthCategory(config.Route, query.Tag);
        if (!string.IsNullOrWhiteSpace(mappedTag))
        {
            facets.Add($"[\"categories:{mappedTag}\"]");
        }

        var mappedLoader = MapLoaderToModrinthCategory(config.Route, query.Loader);
        if (!string.IsNullOrWhiteSpace(mappedLoader))
        {
            facets.Add($"[\"categories:{mappedLoader}\"]");
        }

        var index = query.Sort switch
        {
            "relevance" => "relevance",
            "downloads" => "downloads",
            "follows" => "follows",
            "release" => "newest",
            "update" => "updated",
            _ => string.IsNullOrWhiteSpace(query.SearchText) ? "downloads" : "relevance"
        };
        var parameters = new List<string>
        {
            $"limit={SearchPageSize}",
            $"index={Uri.EscapeDataString(index)}",
            $"facets={Uri.EscapeDataString($"[{string.Join(",", facets)}]")}"
        };
        if (offset > 0)
        {
            parameters.Add($"offset={offset}");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            parameters.Add($"query={Uri.EscapeDataString(query.SearchText)}");
        }

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }

    private static IReadOnlyList<string> TranslateModrinthCategories(IEnumerable<string> categories)
    {
        return categories
            .Select(category => category switch
            {
                "technology" => "technology",
                "magic" => "magic",
                "adventure" => "adventure",
                "utility" => "utility",
                "optimization" => "performance",
                "vanilla-like" => "vanilla_style",
                "realistic" => "realistic",
                "worldgen" => "worldgen",
                "food" => "food_cooking",
                "game-mechanics" => "game_mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "equipment" => "equipment_tools",
                "social" => "multiplayer",
                "library" => "library",
                "multiplayer" => "multiplayer",
                "challenging" => "hardcore",
                "combat" => "combat",
                "quests" => "quests",
                "kitchen-sink" => "kitchen_sink",
                "lightweight" => "lightweight",
                "simplistic" => "simple",
                "tweaks" => "tweaks",
                "8x-" => "resolution_8x",
                "16x" => "16x",
                "32x" => "32x",
                "48x" => "48x",
                "64x" => "64x",
                "128x" => "128x",
                "256x" => "256x",
                "512x+" => "resolution_512x",
                "audio" => "audio",
                "fonts" => "fonts",
                "models" => "models",
                "gui" => "gui",
                "locale" => "locale",
                "core-shaders" => "core_shaders",
                "modded" => "mod_compatible",
                "fantasy" => "fantasy_style",
                "semi-realistic" => "semi_realistic",
                "cartoon" => "cartoon",
                "colored-lighting" => "colored_lighting",
                "path-tracing" => "path_tracing",
                "pbr" => "PBR",
                "reflections" => "reflections",
                "iris" => "Iris",
                "optifine" => "OptiFine",
                "vanilla" => "vanilla_compatible",
                "datapack" => "data_pack",
                _ => string.Empty
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();
    }

}

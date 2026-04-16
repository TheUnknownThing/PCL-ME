using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendCommunityResourceCatalogService
{
    private const int SearchPageSize = 40;
    private const int DefaultTargetResultCount = SearchPageSize * 2;
    private const int MaxSearchRoundsPerQuery = 12;
    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);
    private static CacheEntry<IReadOnlyList<string>>? MinecraftVersionOptionsCache;
    private static readonly IReadOnlyList<RouteConfig> RouteConfigs =
    [
        new RouteConfig(LauncherFrontendSubpageKey.DownloadMod, "Mod", "CommandBlock.png", false, "mod", null, 6, "mc-mods"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadPack, "pack", "CommandBlock.png", false, "modpack", null, 4471, "modpacks"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadDataPack, "data_pack", "RedstoneLampOn.png", false, "mod", "datapack", 6945, "data-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadResourcePack, "resource_pack", "Grass.png", false, "resourcepack", null, 12, "texture-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadShader, "shader", "GoldBlock.png", true, "shader", null, 6552, "shaders"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadWorld, "world", "GrassPath.png", false, null, null, 17, "worlds")
    ];

    public static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> BuildResourceStates(
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference)
    {
        var routeStates = new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>();

        foreach (var config in RouteConfigs)
        {
            routeStates[config.Route] = QueryResources(
                config.Route,
                new FrontendCommunityResourceQuery(
                    SearchText: string.Empty,
                    Source: string.Empty,
                    Tag: string.Empty,
                    Sort: string.Empty,
                    Version: string.Empty,
                    Loader: string.Empty),
                instanceComposition,
                communitySourcePreference).State;
        }

        return routeStates;
    }

    public static FrontendCommunityResourceQueryResult QueryResources(
        LauncherFrontendSubpageKey route,
        FrontendCommunityResourceQuery query,
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference,
        int targetResultCount = DefaultTargetResultCount)
    {
        var config = GetRouteConfig(route);
        var preferredVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var effectiveTargetResultCount = Math.Max(SearchPageSize, targetResultCount);
        var normalizedVersion = NormalizeMinecraftVersion(query.Version);
        var normalizedSearchText = query.SearchText.Trim();
        var normalizedSource = query.Source.Trim();
        var normalizedTag = query.Tag.Trim();
        var normalizedSort = query.Sort.Trim();
        var normalizedLoader = query.Loader.Trim();
        var cacheKey = string.Join("|",
            config.Route,
            preferredVersion ?? "*",
            communitySourcePreference,
            normalizedSearchText,
            normalizedSource,
            normalizedTag,
            normalizedSort,
            normalizedVersion ?? "*",
            normalizedLoader,
            effectiveTargetResultCount);
        _ = cacheKey;

        var effectiveQuery = query with
        {
            SearchText = normalizedSearchText,
            Source = normalizedSource,
            Tag = normalizedTag,
            Sort = normalizedSort,
            Version = normalizedVersion ?? string.Empty,
            Loader = normalizedLoader
        };
        var state = BuildState(config, communitySourcePreference, effectiveQuery, effectiveTargetResultCount);
        return new FrontendCommunityResourceQueryResult(
            state,
            GetMinecraftVersionOptions(
                preferredVersion,
                normalizedVersion,
                state.Entries.SelectMany(entry => entry.SupportedVersions.Count == 0 ? [entry.Version] : entry.SupportedVersions)),
            GetSourceOptions(config));
    }

    private static FrontendDownloadResourceState BuildState(
        RouteConfig config,
        int communitySourcePreference,
        FrontendCommunityResourceQuery query,
        int targetResultCount)
    {
        var selectedVersion = string.IsNullOrWhiteSpace(query.Version)
            ? null
            : query.Version;
        var versionAwareResult = FetchEntries(config, selectedVersion, communitySourcePreference, query, targetResultCount);

        var filteredEntries = ApplyFinalClientSideFilters(versionAwareResult.Entries, query)
            .ToArray();
        var entries = filteredEntries
            .Take(targetResultCount)
            .ToArray();

        return new FrontendDownloadResourceState(
            config.Title,
            config.ModrinthProjectType is not null && config.CurseForgeClassId is not null,
            false,
            config.UseShaderLoaderOptions,
            BuildHintText(config, selectedVersion, versionAwareResult.SourceErrors, entries.Length > 0, usedVersionFallback: false),
            BuildTagOptions(entries),
            versionAwareResult.TotalCount ?? filteredEntries.Length,
            filteredEntries.Length > targetResultCount || versionAwareResult.CanContinue,
            entries);
    }

    private static FetchResult FetchEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference,
        FrontendCommunityResourceQuery query,
        int targetResultCount)
    {
        var maxSearchRounds = Math.Max(MaxSearchRoundsPerQuery, (int)Math.Ceiling(targetResultCount / (double)SearchPageSize) + 1);
        var wantsCurseForge = !string.Equals(query.Source, "Modrinth", StringComparison.OrdinalIgnoreCase);
        var wantsModrinth = !string.Equals(query.Source, "CurseForge", StringComparison.OrdinalIgnoreCase);
        var sourceStates = new List<SourcePaginationState>(2);

        if (wantsModrinth && !string.IsNullOrWhiteSpace(config.ModrinthProjectType))
        {
            sourceStates.Add(new SourcePaginationState("Modrinth"));
        }

        if (wantsCurseForge && config.CurseForgeClassId is not null)
        {
            sourceStates.Add(new SourcePaginationState("CurseForge"));
        }

        var entries = new List<FrontendDownloadResourceEntry>();
        var errors = new List<string>();
        var sourceTotalCountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var round = 0; round < maxSearchRounds; round++)
        {
            var activeStates = sourceStates
                .Where(state => state.HasMoreEntries)
                .ToArray();
            if (activeStates.Length == 0)
            {
                break;
            }

            var tasks = activeStates
                .Select(state => Task.Run(() => state.SourceName switch
                {
                    "Modrinth" => FetchModrinthEntries(config, preferredVersion, communitySourcePreference, query, state.Offset),
                    "CurseForge" => FetchCurseForgeEntries(config, preferredVersion, communitySourcePreference, query, state.Offset),
                    _ => new SourceFetchResult([], "Unknown community source.", 0, null, false)
                }))
                .ToArray();

            Task.WaitAll(tasks);

            var madeRemoteProgress = false;
            foreach (var (state, task) in activeStates.Zip(tasks))
            {
                var result = task.Result;
                state.Advance(result);
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    errors.Add(result.ErrorMessage!);
                }

                if (result.TotalCount is > 0)
                {
                    sourceTotalCountMap[state.SourceName] = result.TotalCount.Value;
                }

                if (result.Entries.Count > 0)
                {
                    entries.AddRange(result.Entries);
                }

                madeRemoteProgress |= result.ReceivedCount > 0;
            }

            if (ApplyFinalClientSideFilters(entries, query).Take(targetResultCount).Count() >= targetResultCount)
            {
                break;
            }

            if (!madeRemoteProgress)
            {
                break;
            }
        }

        int? totalCount = null;
        if (sourceStates.Count > 0 && sourceStates.All(state => sourceTotalCountMap.ContainsKey(state.SourceName)))
        {
            totalCount = sourceStates.Sum(state => sourceTotalCountMap[state.SourceName]);
        }

        return new FetchResult(
            entries,
            errors.Distinct(StringComparer.Ordinal).ToArray(),
            totalCount,
            sourceStates.Any(state => state.HasMoreEntries));
    }

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

    private static JsonObject ReadJsonObject(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey)
    {
        var candidates = BuildCandidateUrls(officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey);
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (candidate.UseCurseForgeApiKey)
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", CurseForgeApiKey);
                }

                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonNode.Parse(content)?.AsObject()
                       ?? throw new InvalidOperationException($"{sourceName} returned an invalid JSON object.");
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(string.Join("；", errors.Distinct(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<RequestCandidate> BuildCandidateUrls(
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey)
    {
        var candidates = new List<RequestCandidate>();
        var canUseOfficial = !officialRequiresApiKey || !string.IsNullOrWhiteSpace(CurseForgeApiKey);

        switch (communitySourcePreference)
        {
            case 0:
                candidates.Add(new RequestCandidate(mirrorUrl, false));
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                break;
            case 1:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
            default:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new RequestCandidate(mirrorUrl, false));
        }

        return candidates
            .DistinctBy(candidate => candidate.Url)
            .ToArray();
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

    private static string BuildHintText(
        RouteConfig config,
        string? preferredVersion,
        IReadOnlyList<string> sourceErrors,
        bool hasEntries,
        bool usedVersionFallback)
    {
        if (sourceErrors.Count == 0 && !usedVersionFallback)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (usedVersionFallback && !string.IsNullOrWhiteSpace(preferredVersion))
        {
            parts.Add($"version_fallback|{preferredVersion}|{config.Title}");
        }

        if (sourceErrors.Count > 0)
        {
            parts.Add(hasEntries
                ? $"partial_results|{string.Join(" ; ", sourceErrors)}"
                : $"unavailable|{string.Join(" ; ", sourceErrors)}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static IReadOnlyList<FrontendDownloadResourceFilterOption> BuildTagOptions(
        IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return
        [
            new FrontendDownloadResourceFilterOption("all", string.Empty),
            .. entries
                .SelectMany(entry => entry.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .Select(group => new FrontendDownloadResourceFilterOption(group.First(), group.First()))
        ];
    }

    private static IEnumerable<FrontendDownloadResourceEntry> ApplyFinalClientSideFilters(
        IEnumerable<FrontendDownloadResourceEntry> entries,
        FrontendCommunityResourceQuery query)
    {
        var filtered = entries;

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filtered = filtered.Where(entry => entry.Tags.Any(tag => string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filtered = filtered.Where(entry =>
                entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Info.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Tags.Any(tag => tag.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Version))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Version, query.Version, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedVersions.Any(version => string.Equals(version, query.Version, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Loader))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Loader, query.Loader, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedLoaders.Any(loader => string.Equals(loader, query.Loader, StringComparison.OrdinalIgnoreCase)));
        }

        return query.Sort switch
        {
            "downloads" => filtered.OrderByDescending(entry => entry.DownloadCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "follows" => filtered.OrderByDescending(entry => entry.FollowCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "release" => filtered.OrderByDescending(entry => entry.ReleaseRank).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "update" => filtered.OrderByDescending(entry => entry.UpdateRank).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "relevance" => filtered.OrderByDescending(entry => entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(entry => entry.DownloadCount)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => string.IsNullOrWhiteSpace(query.SearchText)
                ? filtered.OrderByDescending(entry => entry.DownloadCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(entry => entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(entry => entry.DownloadCount)
                    .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string ResolvePreferredMinecraftVersion(FrontendInstanceComposition instanceComposition)
    {
        var candidate = NormalizeMinecraftVersion(instanceComposition.Selection.VanillaVersion);
        return string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate;
    }

    private static string? NormalizeMinecraftVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = Regex.Match(rawValue, @"\d+\.\d+(?:\.\d+)?");
        return match.Success ? match.Value : null;
    }

    private static string ResolvePrimaryVersion(IEnumerable<string> versions, string? preferredVersion)
    {
        var normalizedVersions = versions
            .Select(NormalizeMinecraftVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && normalizedVersions.Contains(preferredVersion, StringComparer.OrdinalIgnoreCase))
        {
            return preferredVersion;
        }

        return normalizedVersions
            .OrderByDescending(version => ParseVersion(version))
            .FirstOrDefault()
               ?? string.Empty;
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

    private static Version ParseVersion(string? rawValue)
    {
        return Version.TryParse(rawValue, out var version)
            ? version
            : new Version(0, 0);
    }

    private static string ResolvePrimaryLoader(IEnumerable<string> rawCategories, bool useShaderLoaderOptions)
    {
        var categorySet = rawCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (useShaderLoaderOptions)
        {
            if (categorySet.Contains("iris"))
            {
                return "Iris";
            }

            if (categorySet.Contains("optifine"))
            {
                return "OptiFine";
            }

            if (categorySet.Contains("vanilla"))
            {
                return "vanilla_compatible";
            }

            return string.Empty;
        }

        return categorySet.Contains("neoforge") ? "NeoForge"
            : categorySet.Contains("forge") ? "Forge"
            : categorySet.Contains("fabric") ? "Fabric"
            : categorySet.Contains("quilt") ? "Quilt"
            : string.Empty;
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

    private static string ResolveShaderLoaderFromTags(IEnumerable<string> tags)
    {
        var tagSet = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tagSet.Contains("Iris"))
        {
            return "Iris";
        }

        if (tagSet.Contains("OptiFine"))
        {
            return "OptiFine";
        }

        if (tagSet.Contains("vanilla_compatible"))
        {
            return "vanilla_compatible";
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveSupportedLoaders(IEnumerable<string> rawValues, bool useShaderLoaderOptions)
    {
        var values = rawValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaders = new List<string>();

        if (useShaderLoaderOptions)
        {
            if (values.Contains("vanilla") || values.Contains("vanilla_compatible"))
            {
                loaders.Add("vanilla_compatible");
            }

            if (values.Contains("optifine") || values.Contains("OptiFine"))
            {
                loaders.Add("OptiFine");
            }

            if (values.Contains("iris") || values.Contains("Iris"))
            {
                loaders.Add("Iris");
            }

            return loaders;
        }

        if (values.Contains("forge") || values.Contains("Forge"))
        {
            loaders.Add("Forge");
        }

        if (values.Contains("neoforge") || values.Contains("NeoForge"))
        {
            loaders.Add("NeoForge");
        }

        if (values.Contains("fabric") || values.Contains("Fabric"))
        {
            loaders.Add("Fabric");
        }

        if (values.Contains("quilt") || values.Contains("Quilt"))
        {
            loaders.Add("Quilt");
        }

        return loaders;
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

    private static string BuildEntryInfo(string? description, string? author, DateTimeOffset? _, int __)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add(author.Trim());
        }

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static int ToRank(DateTimeOffset? value)
    {
        return value is null ? 0 : (int)Math.Clamp(value.Value.ToUnixTimeSeconds(), 0, int.MaxValue);
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var rawValue = node?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static string GetString(JsonObject? obj, string propertyName)
    {
        return obj?[propertyName]?.GetValue<string>()?.Trim() ?? string.Empty;
    }

    private static int GetInt(JsonObject? obj, string propertyName)
    {
        if (obj?[propertyName] is null)
        {
            return 0;
        }

        return obj[propertyName]!.GetValue<int>();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(15));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-ME-Frontend-Avalonia");
        return client;
    }

    private static RouteConfig GetRouteConfig(LauncherFrontendSubpageKey route)
    {
        return RouteConfigs.First(config => config.Route == route);
    }

    private static IReadOnlyList<string> GetSourceOptions(RouteConfig config)
    {
        var options = new List<string>();
        if (config.CurseForgeClassId is not null)
        {
            options.Add("CurseForge");
        }

        if (!string.IsNullOrWhiteSpace(config.ModrinthProjectType))
        {
            options.Add("Modrinth");
        }

        return options;
    }

    private static IReadOnlyList<string> GetMinecraftVersionOptions(
        string? preferredVersion,
        string? selectedVersion,
        IEnumerable<string> resultVersions)
    {
        if (MinecraftVersionOptionsCache is null
            || DateTimeOffset.UtcNow - MinecraftVersionOptionsCache.CreatedAt > TimeSpan.FromHours(6))
        {
            MinecraftVersionOptionsCache = new CacheEntry<IReadOnlyList<string>>(FetchMinecraftVersionOptions(), DateTimeOffset.UtcNow);
        }

        var versions = new List<string>();
        AddIfVersionMissing(versions, preferredVersion);
        AddIfVersionMissing(versions, selectedVersion);

        foreach (var version in MinecraftVersionOptionsCache.State)
        {
            AddIfVersionMissing(versions, version);
        }

        foreach (var version in resultVersions)
        {
            AddIfVersionMissing(versions, version);
        }

        return versions
            .OrderByDescending(ParseVersion)
            .ToArray();
    }

    private static IReadOnlyList<string> FetchMinecraftVersionOptions()
    {
        var sources = new[]
        {
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"
        };
        foreach (var source in sources)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, source);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var root = JsonNode.Parse(content)?.AsObject();
                var versions = root?["versions"]?.AsArray()
                    .Select(node => node as JsonObject)
                    .Where(node => node is not null && string.Equals(GetString(node, "type"), "release", StringComparison.OrdinalIgnoreCase))
                    .Select(node => NormalizeMinecraftVersion(GetString(node, "id")))
                    .Where(version => !string.IsNullOrWhiteSpace(version))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(ParseVersion)
                    .ToArray();
                if (versions is { Length: > 0 })
                {
                    return versions;
                }
            }
            catch
            {
                // Fall back to the static list below.
            }
        }

        return
        [
            "26.1.1",
            "26.1",
            "1.21.11",
            "1.21.1",
            "1.20.6",
            "1.20.1",
            "1.19.4",
            "1.19.2",
            "1.18.2",
            "1.16.5",
            "1.12.2",
            "1.10.2",
            "1.8.9",
            "1.7.10"
        ];
    }

    private static void AddIfVersionMissing(ICollection<string> versions, string? version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (!string.IsNullOrWhiteSpace(normalized) && !versions.Contains(normalized))
        {
            versions.Add(normalized);
        }
    }

    private static string? MapTagToModrinthCategory(LauncherFrontendSubpageKey route, string tag)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => tag switch
            {
                "worldgen" => "worldgen",
                "technology" => "technology",
                "game_mechanics" => "game-mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "magic" => "magic",
                "adventure" => "adventure",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "utility" => "utility",
                "equipment_tools" => "equipment",
                "performance" => "optimization",
                "multiplayer" => "social",
                "library" => "library",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "multiplayer" => "multiplayer",
                "performance" => "optimization",
                "hardcore" => "challenging",
                "combat" => "combat",
                "quests" => "quests",
                "technology" => "technology",
                "magic" => "magic",
                "adventure" => "adventure",
                "kitchen_sink" => "kitchen-sink",
                "lightweight" => "lightweight",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "worldgen" => "worldgen",
                "technology" => "technology",
                "game_mechanics" => "game-mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "magic" => "magic",
                "adventure" => "adventure",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "utility" => "utility",
                "performance" => "optimization",
                "multiplayer" => "social",
                "library" => "library",
                "modded" => "modded",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "vanilla_style" => "vanilla-like",
                "realistic" => "realistic",
                "simple" => "simplistic",
                "combat" => "combat",
                "tweaks" => "tweaks",
                "audio" => "audio",
                "fonts" => "fonts",
                "models" => "models",
                "locale" => "locale",
                "gui" => "gui",
                "core_shaders" => "core-shaders",
                "mod_compatible" => "modded",
                "16x" => "16x",
                "32x" => "32x",
                "48x" => "48x",
                "64x" => "64x",
                "128x" => "128x",
                "256x" => "256x",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadShader => tag switch
            {
                "vanilla_style" => "vanilla-like",
                "fantasy_style" => "fantasy",
                "realistic" => "realistic",
                "semi_realistic" => "semi-realistic",
                "cartoon" => "cartoon",
                "colored_lighting" => "colored-lighting",
                "path_tracing" => "path-tracing",
                "PBR" => "pbr",
                "reflections" => "reflections",
                _ => null
            },
            _ => null
        };
    }

    private static int? MapTagToCurseForgeCategory(LauncherFrontendSubpageKey route, string tag)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => tag switch
            {
                "worldgen" => 406,
                "technology" => 412,
                "game_mechanics" => 423,
                "transportation" => 414,
                "storage" => 420,
                "magic" => 419,
                "adventure" => 422,
                "decoration" => 424,
                "mobs" => 411,
                "performance" => 6814,
                "multiplayer" => 435,
                "library" => 421,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "multiplayer" => 4484,
                "hardcore" => 4479,
                "combat" => 4483,
                "quests" => 4478,
                "technology" => 4472,
                "magic" => 4473,
                "adventure" => 4475,
                "exploration" => 4476,
                "minigames" => 4477,
                "skyblock" => 4736,
                "vanilla_plus" => 5128,
                "FTB" => 4487,
                "map_based" => 4480,
                "lightweight" => 4481,
                "large" => 4482,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "adventure" => 6948,
                "fantasy" => 6949,
                "library" => 6950,
                "magic" => 6952,
                "modded" => 6946,
                "technology" => 6951,
                "utility" => 6953,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "vanilla_style" => 403,
                "realistic" => 400,
                "modern" => 401,
                "medieval" => 402,
                "steampunk" => 399,
                "fonts" => 5244,
                "dynamic_effects" => 404,
                "mod_compatible" => 4465,
                "16x" => 393,
                "32x" => 394,
                "64x" => 395,
                "128x" => 396,
                "256x" => 397,
                "resolution_512x" => 398,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadShader => tag switch
            {
                "realistic" => 6553,
                "fantasy_style" => 6554,
                "vanilla_style" => 6555,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadWorld => tag switch
            {
                "adventure" => 248,
                "creative" => 249,
                "minigames" => 250,
                "parkour" => 251,
                "puzzle" => 252,
                "survival" => 253,
                "mod_world" => 4464,
                _ => null
            },
            _ => null
        };
    }

    private static string? MapLoaderToModrinthCategory(LauncherFrontendSubpageKey route, string loader)
    {
        if (string.IsNullOrWhiteSpace(loader))
        {
            return null;
        }

        return route == LauncherFrontendSubpageKey.DownloadShader
            ? loader switch
            {
                "Iris" => "iris",
                "OptiFine" => "optifine",
                "vanilla_compatible" => "vanilla",
                _ => null
            }
            : loader switch
            {
                "Forge" => "forge",
                "NeoForge" => "neoforge",
                "Fabric" => "fabric",
                "Quilt" => "quilt",
                _ => null
            };
    }

    private static int? MapLoaderToCurseForgeLoaderType(LauncherFrontendSubpageKey route, string loader)
    {
        if (route == LauncherFrontendSubpageKey.DownloadShader || string.IsNullOrWhiteSpace(loader))
        {
            return null;
        }

        return loader switch
        {
            "Forge" => 1,
            "Fabric" => 4,
            "Quilt" => 5,
            "NeoForge" => 6,
            _ => null
        };
    }

    private sealed record RouteConfig(
        LauncherFrontendSubpageKey Route,
        string Title,
        string IconName,
        bool UseShaderLoaderOptions,
        string? ModrinthProjectType,
        string? ModrinthCategory,
        int? CurseForgeClassId,
        string CurseForgeSectionPath);

    private sealed record CacheEntry(FrontendDownloadResourceState State, DateTimeOffset CreatedAt);

    private sealed record CacheEntry<TState>(TState State, DateTimeOffset CreatedAt);

    private sealed record FetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        IReadOnlyList<string> SourceErrors,
        int? TotalCount,
        bool CanContinue);

    private sealed record SourceFetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        string? ErrorMessage,
        int ReceivedCount,
        int? TotalCount,
        bool HasMoreEntries);

    private sealed record RequestCandidate(string Url, bool UseCurseForgeApiKey);

    private sealed class SourcePaginationState(string sourceName)
    {
        public string SourceName { get; } = sourceName;

        public int Offset { get; private set; }

        public bool HasMoreEntries { get; private set; } = true;

        public void Advance(SourceFetchResult result)
        {
            Offset += Math.Max(0, result.ReceivedCount);
            HasMoreEntries = result.HasMoreEntries;
        }
    }
}

internal sealed record FrontendCommunityResourceQuery(
    string SearchText,
    string Source,
    string Tag,
    string Sort,
    string Version,
    string Loader);

internal sealed record FrontendCommunityResourceQueryResult(
    FrontendDownloadResourceState State,
    IReadOnlyList<string> VersionOptions,
    IReadOnlyList<string> SourceOptions);

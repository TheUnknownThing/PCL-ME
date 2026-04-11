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
        new RouteConfig(LauncherFrontendSubpageKey.DownloadPack, "整合包", "CommandBlock.png", false, "modpack", null, 4471, "modpacks"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadDataPack, "数据包", "RedstoneLampOn.png", false, "mod", "datapack", 6945, "data-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadResourcePack, "资源包", "Grass.png", false, "resourcepack", null, 12, "texture-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadShader, "光影包", "GoldBlock.png", true, "shader", null, 6552, "shaders"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadWorld, "世界", "GrassPath.png", false, null, null, 17, "worlds")
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
            $"{config.Title} 列表",
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
                    _ => new SourceFetchResult([], "未知社区来源。", 0, null, false)
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
            return new SourceFetchResult([], $"Modrinth 暂时不可用：{ex.Message}", 0, null, false);
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
            return new SourceFetchResult([], $"CurseForge 暂时不可用：{ex.Message}", 0, null, false);
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
            "查看详情",
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
            "查看详情",
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
                       ?? throw new InvalidOperationException($"{sourceName} 返回了无效的 JSON 对象。");
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
            parts.Add($"没有找到适配 Minecraft {preferredVersion} 的 {config.Title} 热门结果，已回退到社区通用榜单。");
        }

        if (sourceErrors.Count > 0)
        {
            parts.Add(hasEntries
                ? $"已显示可访问来源的实时结果，另有部分来源失败：{string.Join("；", sourceErrors)}"
                : $"当前无法获取实时社区结果：{string.Join("；", sourceErrors)}");
        }

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<FrontendDownloadResourceFilterOption> BuildTagOptions(
        IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return
        [
            new FrontendDownloadResourceFilterOption("全部", string.Empty),
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
                return "原版可用";
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

        if (tagSet.Contains("原版可用"))
        {
            return "原版可用";
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveSupportedLoaders(IEnumerable<string> rawValues, bool useShaderLoaderOptions)
    {
        var values = rawValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaders = new List<string>();

        if (useShaderLoaderOptions)
        {
            if (values.Contains("vanilla") || values.Contains("原版可用"))
            {
                loaders.Add("原版可用");
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
                "technology" => "科技",
                "magic" => "魔法",
                "adventure" => "冒险",
                "utility" => "实用",
                "optimization" => "性能优化",
                "vanilla-like" => "原版风",
                "realistic" => "写实风",
                "worldgen" => "世界元素",
                "food" => "食物/烹饪",
                "game-mechanics" => "游戏机制",
                "transportation" => "运输",
                "storage" => "仓储",
                "decoration" => "装饰",
                "mobs" => "生物",
                "equipment" => "装备",
                "social" => "服务器",
                "library" => "支持库",
                "multiplayer" => "多人",
                "challenging" => "硬核",
                "combat" => "战斗",
                "quests" => "任务",
                "kitchen-sink" => "水槽包",
                "lightweight" => "轻量",
                "simplistic" => "简洁",
                "tweaks" => "改良",
                "8x-" => "极简",
                "16x" => "16x",
                "32x" => "32x",
                "48x" => "48x",
                "64x" => "64x",
                "128x" => "128x",
                "256x" => "256x",
                "512x+" => "超高清",
                "audio" => "含声音",
                "fonts" => "含字体",
                "models" => "含模型",
                "gui" => "含 UI",
                "locale" => "含语言",
                "core-shaders" => "核心着色器",
                "modded" => "兼容 Mod",
                "fantasy" => "幻想风",
                "semi-realistic" => "半写实风",
                "cartoon" => "卡通风",
                "colored-lighting" => "彩色光照",
                "path-tracing" => "路径追踪",
                "pbr" => "PBR",
                "reflections" => "反射",
                "iris" => "Iris",
                "optifine" => "OptiFine",
                "vanilla" => "原版可用",
                "datapack" => "数据包",
                _ => string.Empty
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();
    }

    private static string TranslateCurseForgeCategory(int categoryId)
    {
        return categoryId switch
        {
            406 => "世界元素",
            407 => "生物群系",
            410 => "维度",
            408 => "矿物/资源",
            409 => "天然结构",
            412 => "科技",
            415 => "管道/物流",
            4843 => "自动化",
            417 => "能源",
            4558 => "红石",
            436 => "食物/烹饪",
            416 => "农业",
            414 => "运输",
            420 => "仓储",
            419 => "魔法",
            422 => "冒险",
            424 => "装饰",
            411 => "生物",
            434 => "装备",
            6814 => "性能优化",
            9026 => "创造模式",
            423 => "信息显示",
            435 => "服务器",
            5191 => "改良",
            421 => "支持库",
            4484 => "多人",
            4479 => "硬核",
            4483 => "战斗",
            4478 => "任务",
            4472 => "科技",
            4473 => "魔法",
            4475 => "冒险",
            4476 => "探索",
            4477 => "小游戏",
            4471 => "科幻",
            4736 => "空岛",
            5128 => "原版改良",
            4487 => "FTB",
            4480 => "基于地图",
            4481 => "轻量",
            4482 => "大型",
            403 => "原版风",
            400 => "写实风",
            401 => "现代风",
            402 => "中世纪",
            399 => "蒸汽朋克",
            5244 => "含字体",
            404 => "动态效果",
            4465 => "兼容 Mod",
            393 => "16x",
            394 => "32x",
            395 => "64x",
            396 => "128x",
            397 => "256x",
            398 => "超高清",
            5193 => "数据包",
            6553 => "写实风",
            6554 => "幻想风",
            6555 => "原版风",
            6948 => "冒险",
            6949 => "幻想",
            6950 => "支持库",
            6952 => "魔法",
            6946 => "Mod 相关",
            6951 => "科技",
            6953 => "实用",
            248 => "冒险",
            249 => "创造",
            250 => "小游戏",
            251 => "跑酷",
            252 => "解谜",
            253 => "生存",
            4464 => "Mod 世界",
            _ => string.Empty
        };
    }

    private static string BuildEntryInfo(string? description, string? author, DateTimeOffset? updatedAt, int downloads)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(author))
        {
            metaParts.Add(author.Trim());
        }

        if (updatedAt is not null)
        {
            metaParts.Add($"更新于 {updatedAt.Value.LocalDateTime:yyyy/MM/dd}");
        }

        if (downloads > 0)
        {
            metaParts.Add($"{downloads:N0} 次下载");
        }

        if (metaParts.Count > 0)
        {
            parts.Add(string.Join(" • ", metaParts));
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
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CE-Frontend-Avalonia");
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
                "世界元素" => "worldgen",
                "科技" => "technology",
                "游戏机制" => "game-mechanics",
                "运输" => "transportation",
                "仓储" => "storage",
                "魔法" => "magic",
                "冒险" => "adventure",
                "装饰" => "decoration",
                "生物" => "mobs",
                "实用" => "utility",
                "装备与工具" => "equipment",
                "性能优化" => "optimization",
                "服务器" => "social",
                "支持库" => "library",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "多人" => "multiplayer",
                "性能优化" => "optimization",
                "硬核" => "challenging",
                "战斗" => "combat",
                "任务" => "quests",
                "科技" => "technology",
                "魔法" => "magic",
                "冒险" => "adventure",
                "水槽包" => "kitchen-sink",
                "轻量整合" => "lightweight",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "世界元素" => "worldgen",
                "科技" => "technology",
                "游戏机制" => "game-mechanics",
                "运输" => "transportation",
                "仓储" => "storage",
                "魔法" => "magic",
                "冒险" => "adventure",
                "装饰" => "decoration",
                "生物" => "mobs",
                "实用" => "utility",
                "性能优化" => "optimization",
                "服务器" => "social",
                "支持库" => "library",
                "Mod 相关" => "modded",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "原版风" => "vanilla-like",
                "写实风" => "realistic",
                "简洁" => "simplistic",
                "战斗" => "combat",
                "改良" => "tweaks",
                "含声音" => "audio",
                "含字体" => "fonts",
                "含模型" => "models",
                "含语言" => "locale",
                "含 UI" => "gui",
                "核心着色器" => "core-shaders",
                "兼容 Mod" => "modded",
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
                "原版风" => "vanilla-like",
                "幻想风" => "fantasy",
                "写实风" => "realistic",
                "半写实风" => "semi-realistic",
                "卡通风" => "cartoon",
                "彩色光照" => "colored-lighting",
                "路径追踪" => "path-tracing",
                "PBR" => "pbr",
                "反射" => "reflections",
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
                "世界元素" => 406,
                "科技" => 412,
                "游戏机制" => 423,
                "运输" => 414,
                "仓储" => 420,
                "魔法" => 419,
                "冒险" => 422,
                "装饰" => 424,
                "生物" => 411,
                "性能优化" => 6814,
                "服务器" => 435,
                "支持库" => 421,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "多人" => 4484,
                "硬核" => 4479,
                "战斗" => 4483,
                "任务" => 4478,
                "科技" => 4472,
                "魔法" => 4473,
                "冒险" => 4475,
                "探索" => 4476,
                "小游戏" => 4477,
                "空岛" => 4736,
                "原版改良" => 5128,
                "FTB" => 4487,
                "基于地图" => 4480,
                "轻量整合" => 4481,
                "大型整合" => 4482,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "冒险" => 6948,
                "幻想" => 6949,
                "支持库" => 6950,
                "魔法" => 6952,
                "Mod 相关" => 6946,
                "科技" => 6951,
                "实用" => 6953,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "原版风" => 403,
                "写实风" => 400,
                "现代风" => 401,
                "中世纪" => 402,
                "蒸汽朋克" => 399,
                "含字体" => 5244,
                "动态效果" => 404,
                "兼容 Mod" => 4465,
                "16x" => 393,
                "32x" => 394,
                "64x" => 395,
                "128x" => 396,
                "256x" => 397,
                "512x 或更高" => 398,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadShader => tag switch
            {
                "写实风" => 6553,
                "幻想风" => 6554,
                "原版风" => 6555,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadWorld => tag switch
            {
                "冒险" => 248,
                "创造" => 249,
                "小游戏" => 250,
                "跑酷" => 251,
                "解谜" => 252,
                "生存" => 253,
                "Mod 世界" => 4464,
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
                "原版可用" => "vanilla",
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

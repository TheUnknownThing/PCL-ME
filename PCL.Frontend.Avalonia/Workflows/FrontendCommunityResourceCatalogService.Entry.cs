using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

    private const int SearchPageSize = 40;

    private const int DefaultTargetResultCount = SearchPageSize * 2;

    private const int MaxSearchRoundsPerQuery = 12;

    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();

    private static readonly HttpClient HttpClient = CreateHttpClient();

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
        var effectiveQuery = query with
        {
            SearchText = normalizedSearchText,
            Source = normalizedSource,
            Tag = normalizedTag,
            Sort = normalizedSort,
            Version = normalizedVersion ?? string.Empty,
            Loader = normalizedLoader
        };
        var cacheKey = BuildQueryCacheKey(route, effectiveQuery, preferredVersion, communitySourcePreference);
        if (TryGetFreshQueryResult(cacheKey, effectiveTargetResultCount, out var cachedResult))
        {
            return cachedResult;
        }

        var state = BuildState(config, communitySourcePreference, effectiveQuery, effectiveTargetResultCount);
        var result = new FrontendCommunityResourceQueryResult(
            state,
            GetMinecraftVersionOptions(
                preferredVersion,
                normalizedVersion,
                state.Entries.SelectMany(entry => entry.SupportedVersions.Count == 0 ? [entry.Version] : entry.SupportedVersions)),
            GetSourceOptions(config));
        QueryResultCache[cacheKey] = new CacheEntry<CachedQueryResult>(
            new CachedQueryResult(result, effectiveTargetResultCount),
            DateTimeOffset.UtcNow);
        return result;
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

}

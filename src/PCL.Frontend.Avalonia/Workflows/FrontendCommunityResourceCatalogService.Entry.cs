using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendCommunityResourceCatalogService
{

    private const int SearchPageSize = 40;

    private const int DefaultTargetResultCount = SearchPageSize * 2;

    private const int MaxSearchRoundsPerQuery = 12;

    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();

    public async Task<IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>> BuildResourceStatesAsync(
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference,
        CancellationToken cancellationToken = default)
    {
        var routeStates = new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>();

        foreach (var config in RouteConfigs)
        {
            routeStates[config.Route] = (await QueryResourcesAsync(
                config.Route,
                new FrontendCommunityResourceQuery(
                    SearchText: string.Empty,
                    Source: string.Empty,
                    Tag: string.Empty,
                    Sort: string.Empty,
                    Version: string.Empty,
                    Loader: string.Empty),
                instanceComposition,
                communitySourcePreference,
                cancellationToken: cancellationToken).ConfigureAwait(false)).State;
        }

        return routeStates;
    }

    public async Task<FrontendCommunityResourceQueryResult> QueryResourcesAsync(
        LauncherFrontendSubpageKey route,
        FrontendCommunityResourceQuery query,
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference,
        int targetResultCount = DefaultTargetResultCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        var now = DateTimeOffset.UtcNow;
        var session = _queryResultCache.AddOrUpdate(
            cacheKey,
            _ => new CacheEntry<QuerySession>(new QuerySession(), now),
            (_, entry) => now - entry.CreatedAt < TimeSpan.FromMinutes(10)
                ? entry
                : new CacheEntry<QuerySession>(new QuerySession(), now)).State;

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cached = session.CachedResult;
            if (cached is not null
                && (cached.Result.State.Entries.Count >= effectiveTargetResultCount || !cached.Result.State.HasMoreEntries))
            {
                return cached.Result;
            }

            var fetchResult = await FetchEntriesAsync(
                config, communitySourcePreference, effectiveQuery, effectiveTargetResultCount,
                cached?.FetchResult, cancellationToken).ConfigureAwait(false);
            var state = BuildState(config, effectiveQuery, effectiveTargetResultCount, fetchResult);
            var versions = await GetMinecraftVersionOptionsAsync(
                preferredVersion,
                normalizedVersion,
                state.Entries.SelectMany(entry => entry.SupportedVersions.Count == 0 ? [entry.Version] : entry.SupportedVersions),
                cancellationToken).ConfigureAwait(false);
            var result = new FrontendCommunityResourceQueryResult(state, versions, GetSourceOptions(config));

            // Publish the entries and provider cursors together, only after the entire
            // request succeeds. A cancelled continuation must leave the previous snapshot intact.
            cancellationToken.ThrowIfCancellationRequested();
            session.CachedResult = new CachedQueryResult(result, fetchResult);
            return result;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private static FrontendDownloadResourceState BuildState(
        RouteConfig config,
        FrontendCommunityResourceQuery query,
        int targetResultCount,
        FetchResult versionAwareResult)
    {
        var selectedVersion = string.IsNullOrWhiteSpace(query.Version)
            ? null
            : query.Version;

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
            versionAwareResult.CanContinue ? versionAwareResult.TotalCount ?? filteredEntries.Length : filteredEntries.Length,
            filteredEntries.Length > targetResultCount || versionAwareResult.CanContinue,
            entries);
    }

    private async Task<FetchResult> FetchEntriesAsync(
        RouteConfig config,
        int communitySourcePreference,
        FrontendCommunityResourceQuery query,
        int targetResultCount,
        FetchResult? cachedResult,
        CancellationToken cancellationToken)
    {
        var wantsCurseForge = !string.Equals(query.Source, "Modrinth", StringComparison.OrdinalIgnoreCase);
        var wantsModrinth = !string.Equals(query.Source, "CurseForge", StringComparison.OrdinalIgnoreCase);
        var sourceStates = cachedResult?.SourceStates.ToList() ?? [];

        if (cachedResult is null)
        {
            if (wantsModrinth && !string.IsNullOrWhiteSpace(config.ModrinthProjectType))
            {
                sourceStates.Add(new SourcePaginationState("Modrinth"));
            }

            if (wantsCurseForge && config.CurseForgeClassId is not null)
            {
                sourceStates.Add(new SourcePaginationState("CurseForge"));
            }
        }

        var entries = cachedResult?.Entries.ToList() ?? [];
        var errors = new List<string>();
        var failedSources = new HashSet<string>(StringComparer.Ordinal);
        var remainingCount = Math.Max(0, targetResultCount - entries.Count);
        var maxSearchRounds = Math.Max(MaxSearchRoundsPerQuery, (int)Math.Ceiling(remainingCount / (double)SearchPageSize) + 1);

        for (var round = 0; round < maxSearchRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Count >= targetResultCount)
            {
                break;
            }

            var activeStates = sourceStates.Where(state => state.HasMoreEntries && !failedSources.Contains(state.SourceName)).ToArray();
            if (activeStates.Length == 0)
            {
                break;
            }

            var tasks = activeStates.Select(state => state.SourceName switch
            {
                "Modrinth" => FetchModrinthEntriesAsync(config, query.Version, communitySourcePreference, query, state.Offset, cancellationToken),
                "CurseForge" => FetchCurseForgeEntriesAsync(config, query.Version, communitySourcePreference, query, state.Offset, cancellationToken),
                _ => throw new InvalidOperationException("Unknown community source.")
            });
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var madeRemoteProgress = false;
            foreach (var (state, result) in activeStates.Zip(results))
            {
                sourceStates[sourceStates.IndexOf(state)] = state.Advance(result);
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    errors.Add(result.ErrorMessage!);
                    failedSources.Add(state.SourceName);
                }

                entries.AddRange(ApplyFinalClientSideFilters(result.Entries, query));
                madeRemoteProgress |= result.ReceivedCount > 0;
            }

            if (!madeRemoteProgress)
            {
                break;
            }
        }

        int? totalCount = sourceStates.Count > 0 && sourceStates.All(state => state.TotalCount is not null)
            ? sourceStates.Sum(state => state.TotalCount!.Value)
            : null;

        return new FetchResult(
            entries,
            errors.Distinct(StringComparer.Ordinal).ToArray(),
            totalCount,
            sourceStates.Any(state => state.HasMoreEntries),
            sourceStates);
    }

}

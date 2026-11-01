using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendDownloadRemoteCatalogService
{
    public static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildCatalogStates(
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var cacheKey = new RemoteCatalogCacheKey(versionSourceIndex, normalizedVersion);
        RemoteCatalogCacheEntry? staleEntry;

        lock (CacheSync)
        {
            if (Cache.TryGetValue(cacheKey, out var cachedEntry)
                && DateTimeOffset.UtcNow - cachedEntry.FetchedAtUtc <= CacheLifetime)
            {
                return cachedEntry.States;
            }

            Cache.TryGetValue(cacheKey, out staleEntry);
        }

        try
        {
            var states = FetchCatalogStates(versionSourceIndex, normalizedVersion, i18n);
            lock (CacheSync)
            {
                Cache[cacheKey] = new RemoteCatalogCacheEntry(DateTimeOffset.UtcNow, states);
            }

            return states;
        }
        catch (Exception ex)
        {
            if (staleEntry is not null)
            {
                return staleEntry.States.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value with { StaleError = ex.Message, LoadError = null });
            }

            return BuildFailureStates(ex.Message, i18n);
        }
    }

    public static Task<FrontendDownloadCatalogState> LoadCatalogStateAsync(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => BuildCatalogState(route, versionSourceIndex, preferredMinecraftVersion, i18n),
            cancellationToken);
    }

    public static Task<IReadOnlyList<FrontendDownloadCatalogEntry>> LoadCatalogSectionEntriesAsync(
        LauncherFrontendSubpageKey route,
        string lazyLoadToken,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => FetchCatalogSectionEntries(route, lazyLoadToken, versionSourceIndex, NormalizeMinecraftVersion(preferredMinecraftVersion), i18n),
            cancellationToken);
    }

    public static string GetLoadingText(LauncherFrontendSubpageKey route, II18nService? i18n = null)
    {
        return GetGoldCatalogDescriptor(route, i18n).LoadingText;
    }

    public static FrontendDownloadCatalogState BuildCatalogState(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var cacheKey = new RouteCatalogCacheKey(route, versionSourceIndex, normalizedVersion);
        RouteCatalogCacheEntry? staleEntry;

        lock (CacheSync)
        {
            if (RouteCache.TryGetValue(cacheKey, out var cachedEntry)
                && DateTimeOffset.UtcNow - cachedEntry.FetchedAtUtc <= CacheLifetime)
            {
                return cachedEntry.State;
            }

            RouteCache.TryGetValue(cacheKey, out staleEntry);
        }

        try
        {
            var state = FetchCatalogState(route, versionSourceIndex, normalizedVersion, i18n);
            lock (CacheSync)
            {
                RouteCache[cacheKey] = new RouteCatalogCacheEntry(DateTimeOffset.UtcNow, state);
            }

            return state;
        }
        catch (Exception ex)
        {
            if (staleEntry is not null)
            {
                return staleEntry.State with { StaleError = ex.Message, LoadError = null };
            }

            return BuildFailureState(route, ex.Message, i18n);
        }
    }
}

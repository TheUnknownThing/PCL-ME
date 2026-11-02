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
    private const string SnapshotSectionKey = "snapshot_versions";
    private static readonly HttpClient HttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));
    private static readonly object CacheSync = new();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<RemoteCatalogCacheKey, RemoteCatalogCacheEntry> Cache = [];
    private static readonly Dictionary<RouteCatalogCacheKey, RouteCatalogCacheEntry> RouteCache = [];
    private static readonly LauncherFrontendSubpageKey[] CatalogRoutes =
    [
        LauncherFrontendSubpageKey.DownloadClient,
        LauncherFrontendSubpageKey.DownloadOptiFine,
        LauncherFrontendSubpageKey.DownloadForge,
        LauncherFrontendSubpageKey.DownloadNeoForge,
        LauncherFrontendSubpageKey.DownloadCleanroom,
        LauncherFrontendSubpageKey.DownloadFabric,
        LauncherFrontendSubpageKey.DownloadLegacyFabric,
        LauncherFrontendSubpageKey.DownloadQuilt,
        LauncherFrontendSubpageKey.DownloadLiteLoader,
        LauncherFrontendSubpageKey.DownloadLabyMod
    ];
}

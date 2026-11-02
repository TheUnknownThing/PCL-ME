using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

    private sealed record RouteConfig(
        LauncherFrontendSubpageKey Route,
        string Title,
        string IconName,
        bool UseShaderLoaderOptions,
        string? ModrinthProjectType,
        string? ModrinthCategory,
        int? CurseForgeClassId,
        string CurseForgeSectionPath);

    private sealed record CacheEntry<TState>(TState State, DateTimeOffset CreatedAt);

    private sealed record CachedQueryResult(
        FrontendCommunityResourceQueryResult Result,
        int TargetResultCount);

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

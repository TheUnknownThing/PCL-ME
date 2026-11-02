using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendCommunityResourceCatalogService
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
        FetchResult FetchResult);

    private sealed record FetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        IReadOnlyList<string> SourceErrors,
        int? TotalCount,
        bool CanContinue,
        IReadOnlyList<SourcePaginationState> SourceStates);

    private sealed record SourceFetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        string? ErrorMessage,
        int ReceivedCount,
        int? TotalCount,
        bool HasMoreEntries);

    private sealed record RequestCandidate(string Url, bool UseCurseForgeApiKey);

    private sealed class QuerySession
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public CachedQueryResult? CachedResult { get; set; }
    }

    private sealed record SourcePaginationState(
        string SourceName,
        int Offset = 0,
        bool HasMoreEntries = true,
        int? TotalCount = null)
    {
        public SourcePaginationState Advance(SourceFetchResult result) => result.ErrorMessage is not null ? this : this with
        {
            Offset = Offset + Math.Max(0, result.ReceivedCount),
            HasMoreEntries = result.HasMoreEntries,
            TotalCount = result.TotalCount ?? TotalCount
        };
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

using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.Models;

internal sealed record FrontendCommunityProjectSummary(
    string ProjectId,
    string Title,
    string Summary,
    string Source,
    string? Author,
    string? ProjectType,
    string? Website,
    string UpdatedLabel,
    int DownloadCount,
    int FollowCount);

internal sealed record FrontendCommunityProjectLookupResult(
    IReadOnlyDictionary<string, FrontendCommunityProjectSummary> Projects,
    IReadOnlyList<string> Errors);

internal sealed record FrontendCommunityProjectState(
    string ProjectId,
    string Title,
    string Summary,
    string Description,
    string Source,
    string Status,
    string UpdatedLabel,
    int DownloadCount,
    int FollowCount,
    string CompatibilitySummary,
    string CategorySummary,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections,
    string WarningText,
    bool ShowWarning);

internal sealed record FrontendHomePageMarketState(
    string Summary,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections,
    string WarningText,
    bool ShowWarning);

internal sealed record FrontendHomePageMarketSectionRequest(
    LauncherFrontendSubpageKey Route,
    string Title,
    int Limit);

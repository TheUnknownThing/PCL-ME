namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendCommunityProjectSummary(
    string ProjectId,
    string Title,
    string Summary,
    string Source,
    string? Author,
    string? ProjectType,
    string? Website,
    string? IconUrl,
    string? IconPath,
    string UpdatedLabel,
    int DownloadCount,
    int FollowCount);

internal sealed record FrontendCommunityProjectLookupResult(
    IReadOnlyDictionary<string, FrontendCommunityProjectSummary> Projects,
    IReadOnlyList<string> Errors);

internal enum FrontendCommunityProjectReleaseChannel
{
    Release,
    Beta,
    Alpha
}

internal enum FrontendCommunityProjectDependencyKind
{
    Embedded,
    Optional,
    Required,
    Tool,
    Include,
    Incompatible,
    Broken
}

internal sealed record FrontendCommunityProjectDependencyEntry(
    string ProjectId,
    string Title,
    string Summary,
    string Meta,
    string? IconUrl,
    string? IconPath,
    string? Target,
    FrontendCommunityProjectDependencyKind Kind);

internal sealed record FrontendCommunityProjectReleaseEntry(
    string Title,
    string Info,
    string Meta,
    string ActionText,
    string? Target,
    string? SuggestedFileName,
    bool IsDirectDownload,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<FrontendCommunityProjectDependencyEntry> Dependencies,
    long PublishedUnixTime,
    FrontendCommunityProjectReleaseChannel Channel,
    string? ReleaseId = null,
    string? FileId = null,
    string? Sha1 = null,
    string? Sha512 = null);

internal sealed record FrontendCommunityProjectState(
    string ProjectId,
    string Title,
    string Summary,
    string Description,
    string Source,
    string? IconUrl,
    string? IconPath,
    string Website,
    string Status,
    string UpdatedLabel,
    int DownloadCount,
    int FollowCount,
    string CompatibilitySummary,
    string CategorySummary,
    IReadOnlyList<FrontendCommunityProjectReleaseEntry> Releases,
    IReadOnlyList<FrontendDownloadCatalogEntry> Links,
    string WarningText,
    bool ShowWarning);

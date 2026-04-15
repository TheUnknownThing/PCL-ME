using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendDownloadComposition(
    FrontendDownloadInstallState Install,
    IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> CatalogStates,
    FrontendDownloadFavoritesState Favorites,
    IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> ResourceStates);

internal sealed record FrontendDownloadInstallState(
    string Name,
    string MinecraftVersion,
    string? MinecraftIconName,
    IReadOnlyList<string> Hints,
    IReadOnlyList<FrontendDownloadInstallOption> Options);

internal sealed record FrontendDownloadInstallOption(
    string Title,
    string Selection,
    string? IconName);

internal sealed record FrontendDownloadCatalogState(
    string IntroTitle,
    string IntroBody,
    string LoadingText,
    IReadOnlyList<FrontendDownloadCatalogAction> Actions,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections);

internal sealed record FrontendDownloadCatalogAction(
    string Text,
    string Target,
    bool IsHighlight);

internal enum FrontendDownloadCatalogEntryActionKind
{
    OpenTarget = 0,
    DownloadFile = 1
}

internal sealed record FrontendDownloadCatalogSection(
    string Title,
    IReadOnlyList<FrontendDownloadCatalogEntry> Entries,
    bool IsCollapsible = false,
    bool IsInitiallyExpanded = true,
    string? LazyLoadToken = null,
    string LoadingText = "");

internal sealed record FrontendDownloadCatalogEntry(
    string Title,
    string Info,
    string Meta,
    string ActionText,
    string? Target,
    FrontendDownloadCatalogEntryActionKind ActionKind = FrontendDownloadCatalogEntryActionKind.OpenTarget,
    string? SuggestedFileName = null,
    string? Identity = null,
    string? IconUrl = null,
    string? IconPath = null,
    string? IconName = null,
    LauncherFrontendSubpageKey? OriginSubpage = null);

internal sealed record FrontendDownloadFavoriteTargetState(
    string Name,
    string Id,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections);

internal sealed record FrontendDownloadFavoritesState(
    IReadOnlyList<FrontendDownloadFavoriteTargetState> Targets,
    string WarningText,
    bool ShowWarning);

internal sealed record FrontendDownloadResourceState(
    string SurfaceTitle,
    bool SupportsSecondarySource,
    bool ShowInstallModPackAction,
    bool UseShaderLoaderOptions,
    string HintText,
    IReadOnlyList<FrontendDownloadResourceFilterOption> TagOptions,
    int TotalEntryCount,
    bool HasMoreEntries,
    IReadOnlyList<FrontendDownloadResourceEntry> Entries);

internal sealed record FrontendDownloadResourceFilterOption(
    string Label,
    string FilterValue);

internal sealed record FrontendDownloadResourceEntry(
    string Title,
    string Info,
    string Source,
    string Version,
    string Loader,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> SupportedLoaders,
    string ActionText,
    string? IconUrl,
    string? IconPath,
    string? IconName,
    string? TargetPath,
    int DownloadCount,
    int FollowCount,
    int ReleaseRank,
    int UpdateRank);

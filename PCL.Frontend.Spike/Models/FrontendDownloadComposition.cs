using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.Models;

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
    IReadOnlyList<FrontendDownloadCatalogAction> Actions,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections);

internal sealed record FrontendDownloadCatalogAction(
    string Text,
    string Target,
    bool IsHighlight);

internal sealed record FrontendDownloadCatalogSection(
    string Title,
    IReadOnlyList<FrontendDownloadCatalogEntry> Entries);

internal sealed record FrontendDownloadCatalogEntry(
    string Title,
    string Info,
    string Meta,
    string ActionText,
    string? Target);

internal sealed record FrontendDownloadFavoritesState(
    IReadOnlyList<string> Targets,
    string WarningText,
    bool ShowWarning,
    IReadOnlyList<FrontendDownloadCatalogSection> Sections);

internal sealed record FrontendDownloadResourceState(
    string SurfaceTitle,
    bool SupportsSecondarySource,
    bool ShowInstallModPackAction,
    bool UseShaderLoaderOptions,
    string HintText,
    IReadOnlyList<FrontendDownloadResourceFilterOption> TagOptions,
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
    string ActionText,
    string? IconName,
    string? TargetPath,
    int DownloadCount,
    int FollowCount,
    int ReleaseRank,
    int UpdateRank);

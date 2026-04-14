using Avalonia.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private FrontendDownloadComposition _downloadComposition = new(
        new FrontendDownloadInstallState(string.Empty, "Minecraft", "Grass.png", [], []),
        new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>(),
        new FrontendDownloadFavoritesState([new FrontendDownloadFavoriteTargetState(string.Empty, "default", [])], string.Empty, false),
        new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>());
    private bool _downloadCompositionHasRemoteState;
    private IReadOnlyList<string> _downloadFavoriteTargetOptions = [];

    private void ReloadDownloadComposition(bool includeRemoteState = false)
    {
        _downloadFavoriteRefreshCts?.Cancel();
        _downloadFavoriteRefreshCts = null;
        _downloadFavoriteRefreshVersion++;
        _isDownloadFavoriteLoading = false;
        _downloadComposition = includeRemoteState
            ? FrontendDownloadCompositionService.Compose(
                _shellActionService.RuntimePaths,
                _instanceComposition,
                _versionSavesComposition)
            : FrontendDownloadCompositionService.ComposeBootstrap(
                _shellActionService.RuntimePaths,
                _instanceComposition);
        _downloadCompositionHasRemoteState = includeRemoteState;
        SyncDownloadFavoriteTargets();
    }

    private void EnsureDownloadCompositionRemoteStateLoaded()
    {
        if (_downloadCompositionHasRemoteState || _isDownloadFavoriteLoading)
        {
            return;
        }

        _downloadFavoriteRefreshCts?.Cancel();
        var refreshVersion = ++_downloadFavoriteRefreshVersion;
        var cts = new CancellationTokenSource();
        _downloadFavoriteRefreshCts = cts;
        _isDownloadFavoriteLoading = true;
        _ = LoadDownloadFavoriteStateAsync(refreshVersion, cts.Token);
    }

    private async Task LoadDownloadFavoriteStateAsync(int refreshVersion, CancellationToken cancellationToken)
    {
        try
        {
            var favoritesState = await FrontendDownloadCompositionService.LoadFavoritesStateAsync(
                _shellActionService.RuntimePaths,
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || refreshVersion != _downloadFavoriteRefreshVersion)
                {
                    return;
                }

                _downloadFavoriteRefreshCts = null;
                _isDownloadFavoriteLoading = false;
                _downloadComposition = _downloadComposition with { Favorites = favoritesState };
                _downloadCompositionHasRemoteState = true;
                SyncDownloadFavoriteTargets();
                if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
                {
                    RefreshDownloadFavoriteSurface();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Route changes or refreshes can supersede the current favorite metadata request.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (refreshVersion != _downloadFavoriteRefreshVersion)
                {
                    return;
                }

                _downloadFavoriteRefreshCts = null;
                _isDownloadFavoriteLoading = false;
                _downloadComposition = _downloadComposition with
                {
                    Favorites = _downloadComposition.Favorites with
                    {
                        WarningText = $"收藏夹在线元数据加载失败：{ex.Message}",
                        ShowWarning = _downloadComposition.Favorites.Targets.Any(target => target.Sections.Any(section => section.Entries.Count > 0))
                    }
                };

                if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
                {
                    DownloadFavoriteWarningText = _downloadComposition.Favorites.WarningText;
                    ShowDownloadFavoriteWarning = _downloadComposition.Favorites.ShowWarning;
                }
            });
        }
    }

    private void SyncDownloadFavoriteTargets()
    {
        _downloadFavoriteTargetOptions = _downloadComposition.Favorites.Targets.Count == 0
            ? [T("download.favorites.targets.default_name")]
            : _downloadComposition.Favorites.Targets.Select(target => target.Name).ToArray();
        _selectedDownloadFavoriteTargetIndex = Math.Clamp(_selectedDownloadFavoriteTargetIndex, 0, _downloadFavoriteTargetOptions.Count - 1);
        RaisePropertyChanged(nameof(DownloadFavoriteTargetOptions));
        RaisePropertyChanged(nameof(SelectedDownloadFavoriteTargetIndex));
    }
}

using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private FrontendDownloadComposition _downloadComposition = new(
        new FrontendDownloadInstallState("新的安装方案", "Minecraft", "Grass.png", [], []),
        new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>(),
        new FrontendDownloadFavoritesState(["默认收藏夹"], string.Empty, false, []),
        new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>());
    private IReadOnlyList<string> _downloadFavoriteTargetOptions = ["默认收藏夹"];

    private void ReloadDownloadComposition()
    {
        _downloadComposition = FrontendDownloadCompositionService.Compose(
            _shellActionService.RuntimePaths,
            _instanceComposition,
            _versionSavesComposition);
        _downloadFavoriteTargetOptions = _downloadComposition.Favorites.Targets.Count == 0
            ? ["默认收藏夹"]
            : _downloadComposition.Favorites.Targets;
        _selectedDownloadFavoriteTargetIndex = Math.Clamp(_selectedDownloadFavoriteTargetIndex, 0, _downloadFavoriteTargetOptions.Count - 1);
    }
}

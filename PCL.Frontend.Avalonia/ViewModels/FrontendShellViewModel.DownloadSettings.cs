using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public string DownloadInstallName
    {
        get => _downloadInstallName;
        set
        {
            if (SetProperty(ref _downloadInstallName, value))
            {
                OnDownloadInstallNameChanged();
            }
        }
    }

    public string DownloadCatalogIntroTitle
    {
        get => _downloadCatalogIntroTitle;
        private set
        {
            if (SetProperty(ref _downloadCatalogIntroTitle, value))
            {
                RaisePropertyChanged(nameof(HasDownloadCatalogIntro));
            }
        }
    }

    public string DownloadCatalogIntroBody
    {
        get => _downloadCatalogIntroBody;
        private set => SetProperty(ref _downloadCatalogIntroBody, value);
    }

    public string DownloadCatalogLoadingText
    {
        get => _downloadCatalogLoadingText;
        private set => SetProperty(ref _downloadCatalogLoadingText, value);
    }

    public bool HasDownloadCatalogIntro => !string.IsNullOrWhiteSpace(DownloadCatalogIntroTitle);

    public bool ShowDownloadCatalogLoadingCard => _isDownloadCatalogLoading;

    public bool ShowDownloadCatalogContent => !_isDownloadCatalogLoading;

    public string DownloadFavoriteSearchQuery
    {
        get => _downloadFavoriteSearchQuery;
        set
        {
            if (SetProperty(ref _downloadFavoriteSearchQuery, value) && IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
            {
                RefreshDownloadFavoriteSurface();
            }
        }
    }

    public IReadOnlyList<string> DownloadFavoriteTargetOptions => _downloadFavoriteTargetOptions;

    public int SelectedDownloadFavoriteTargetIndex
    {
        get => _selectedDownloadFavoriteTargetIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, DownloadFavoriteTargetOptions.Count, out var nextValue))
            {
                return;
            }

            if (!SetProperty(ref _selectedDownloadFavoriteTargetIndex, nextValue))
            {
                return;
            }

            if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
            {
                RefreshDownloadFavoriteSurface();
            }

            if (ShowCompDetailSurface)
            {
                RebuildCommunityProjectSurfaceCollections();
                RaiseCommunityProjectProperties();
            }
        }
    }

    public string DownloadFavoriteWarningText
    {
        get => _downloadFavoriteWarningText;
        private set => SetProperty(ref _downloadFavoriteWarningText, value);
    }

    public bool ShowDownloadFavoriteWarning
    {
        get => _showDownloadFavoriteWarning;
        private set => SetProperty(ref _showDownloadFavoriteWarning, value);
    }

    public bool HasDownloadFavoriteSections => DownloadFavoriteSections.Count > 0;

    public bool HasNoDownloadFavoriteSections => !HasDownloadFavoriteSections;
}

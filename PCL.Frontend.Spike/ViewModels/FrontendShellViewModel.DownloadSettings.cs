using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public string DownloadInstallName
    {
        get => _downloadInstallName;
        set => SetProperty(ref _downloadInstallName, value);
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

    public bool HasDownloadCatalogIntro => !string.IsNullOrWhiteSpace(DownloadCatalogIntroTitle);

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
            var nextValue = Math.Clamp(value, 0, DownloadFavoriteTargetOptions.Count - 1);
            if (SetProperty(ref _selectedDownloadFavoriteTargetIndex, nextValue) && IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
            {
                RefreshDownloadFavoriteSurface();
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

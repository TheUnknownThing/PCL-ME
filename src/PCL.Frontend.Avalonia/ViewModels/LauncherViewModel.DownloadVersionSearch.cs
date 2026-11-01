namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private string _downloadCatalogSearchQuery = string.Empty;

    public string DownloadCatalogSearchQuery
    {
        get => _downloadCatalogSearchQuery;
        set
        {
            if (SetProperty(ref _downloadCatalogSearchQuery, value))
            {
                foreach (var section in DownloadCatalogSections)
                {
                    section.ApplySearch(value);
                }

                RaisePropertyChanged(nameof(ShowDownloadCatalogSearchEmpty));
            }
        }
    }

    public bool ShowDownloadCatalogSearchEmpty => !string.IsNullOrWhiteSpace(DownloadCatalogSearchQuery) &&
                                                  !DownloadCatalogSections.Any(section => section.IsSearchMatch);

    private void SetDownloadCatalogSections(IEnumerable<DownloadCatalogSectionViewModel> sections)
    {
        foreach (var section in DownloadCatalogSections)
        {
            section.PropertyChanged -= OnDownloadCatalogSearchMatchChanged;
        }

        ReplaceItems(DownloadCatalogSections, sections);
        foreach (var section in DownloadCatalogSections)
        {
            section.PropertyChanged += OnDownloadCatalogSearchMatchChanged;
            section.ApplySearch(DownloadCatalogSearchQuery);
        }

        RaisePropertyChanged(nameof(ShowDownloadCatalogSearchEmpty));
    }

    private void OnDownloadCatalogSearchMatchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadCatalogSectionViewModel.IsSearchMatch))
        {
            RaisePropertyChanged(nameof(ShowDownloadCatalogSearchEmpty));
        }
    }
}

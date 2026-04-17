using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool HasDownloadResourceEntries => DownloadResourceEntries.Count > 0;

    public bool HasNoDownloadResourceEntries => !HasDownloadResourceEntries;

    public bool ShowDownloadResourceLoadingCard => _isDownloadResourceLoading;

    public bool ShowDownloadResourceContent => !_isDownloadResourceLoading;

    public bool ShowDownloadResourcePagination => _downloadResourceTotalPages > 1;

    public string DownloadResourcePageLabel => _downloadResourceTotalPages <= 0
        ? "0"
        : (_downloadResourcePageIndex + 1).ToString();

    public ActionCommand ResetDownloadResourceFiltersCommand => _resetDownloadResourceFiltersCommand;

    public ActionCommand InstallDownloadResourceModPackCommand => _installDownloadResourceModPackCommand;

    public ActionCommand SearchDownloadResourceCommand => _searchDownloadResourceCommand;

    public ActionCommand FirstDownloadResourcePageCommand => _firstDownloadResourcePageCommand;

    public ActionCommand PreviousDownloadResourcePageCommand => _previousDownloadResourcePageCommand;

    public ActionCommand NextDownloadResourcePageCommand => _nextDownloadResourcePageCommand;

    public bool CanGoToPreviousDownloadResourcePage => _downloadResourcePageIndex > 0;

    public bool CanGoToNextDownloadResourcePage => _downloadResourcePageIndex < _downloadResourceTotalPages - 1;

    public bool CanNotGoToPreviousDownloadResourcePage => !CanGoToPreviousDownloadResourcePage;

    public bool CanNotGoToNextDownloadResourcePage => !CanGoToNextDownloadResourcePage;

    public string DownloadResourceResultSummary
    {
        get
        {
            var loadedCount = _allDownloadResourceEntries.Count;
            var totalCount = _downloadResourceTotalEntryCount > 0 ? _downloadResourceTotalEntryCount : loadedCount;
            var shownCount = Math.Min((_downloadResourcePageIndex + 1) * DownloadResourcePageSize, totalCount);
            var totalText = totalCount.ToString();
            return T("download.resource.results.summary", ("shown_count", shownCount), ("total_count", totalText));
        }
    }
    private void GoToFirstDownloadResourcePage()
    {
        if (_downloadResourcePageIndex == 0)
        {
            return;
        }

        _downloadResourcePageIndex = 0;
        ApplyDownloadResourceFilters(resetPage: false);
    }

    private void GoToPreviousDownloadResourcePage()
    {
        if (_downloadResourcePageIndex <= 0)
        {
            return;
        }

        _downloadResourcePageIndex--;
        ApplyDownloadResourceFilters(resetPage: false);
    }

    private void GoToNextDownloadResourcePage()
    {
        var nextPageIndex = _downloadResourcePageIndex + 1;
        if (nextPageIndex < _downloadResourceTotalPages)
        {
            if (nextPageIndex < GetLoadedDownloadResourcePageCount() || !_downloadResourceHasMoreEntries)
            {
                _downloadResourcePageIndex = nextPageIndex;
                ApplyDownloadResourceFilters(resetPage: false);
                return;
            }

            ScheduleDownloadResourceRefresh(immediate: true, resetPage: false, targetPageIndex: nextPageIndex);
        }
    }

    private void GoToDownloadResourcePage(int pageIndex)
    {
        var clampedPageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, _downloadResourceTotalPages - 1));
        if (clampedPageIndex == _downloadResourcePageIndex)
        {
            return;
        }

        if (clampedPageIndex < GetLoadedDownloadResourcePageCount() || !_downloadResourceHasMoreEntries)
        {
            _downloadResourcePageIndex = clampedPageIndex;
            ApplyDownloadResourceFilters(resetPage: false);
            return;
        }

        ScheduleDownloadResourceRefresh(immediate: true, resetPage: false, targetPageIndex: clampedPageIndex);
    }
    private void NotifyDownloadResourcePageCommandState()
    {
        _firstDownloadResourcePageCommand.NotifyCanExecuteChanged();
        _previousDownloadResourcePageCommand.NotifyCanExecuteChanged();
        _nextDownloadResourcePageCommand.NotifyCanExecuteChanged();
        RaisePropertyChanged(nameof(CanGoToPreviousDownloadResourcePage));
        RaisePropertyChanged(nameof(CanGoToNextDownloadResourcePage));
        RaisePropertyChanged(nameof(CanNotGoToPreviousDownloadResourcePage));
        RaisePropertyChanged(nameof(CanNotGoToNextDownloadResourcePage));
        RaisePropertyChanged(nameof(DownloadResourceResultSummary));
        RebuildDownloadResourcePaginationItems();
    }

    private void RebuildDownloadResourcePaginationItems()
    {
        var knownTotalPages = Math.Max(1, _downloadResourceTotalPages);
        var currentPage = Math.Clamp(_downloadResourcePageIndex + 1, 1, knownTotalPages);
        var pages = new SortedSet<int> { 1, currentPage - 1, currentPage, currentPage + 1 };
        pages.Add(knownTotalPages);

        pages.RemoveWhere(page => page < 1 || page > knownTotalPages);

        var items = new List<DownloadResourcePaginationItemViewModel>();
        var previousPage = 0;

        foreach (var page in pages)
        {
            if (previousPage > 0 && page - previousPage > 1)
            {
                items.Add(new DownloadResourcePaginationItemViewModel(
                    BuildPaginationDots(page - previousPage - 1),
                    null,
                    false,
                    isEllipsis: true));
            }

            var targetPage = page;
            items.Add(new DownloadResourcePaginationItemViewModel(
                targetPage.ToString(),
                new ActionCommand(() => GoToDownloadResourcePage(targetPage - 1)),
                isCurrent: targetPage == currentPage,
                isEllipsis: false));
            previousPage = page;
        }

        ReplaceItems(DownloadResourcePaginationItems, items);
    }

    private static string BuildPaginationDots(int hiddenPageCount)
    {
        _ = hiddenPageCount;
        return "...";
    }
}

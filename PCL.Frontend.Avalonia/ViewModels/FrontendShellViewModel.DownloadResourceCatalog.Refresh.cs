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
    private void ScheduleDownloadResourceRefresh(bool immediate, bool resetPage, int? targetPageIndex = null)
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            return;
        }

        _downloadResourceRefreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _downloadResourceRefreshCts = cts;
        var refreshVersion = ++_downloadResourceRefreshVersion;
        var route = _currentRoute.Subpage;
        var query = BuildCurrentDownloadResourceQuery();
        var targetResultCount = GetDownloadResourceTargetResultCount(targetPageIndex);
        var communitySourcePreference = SelectedCommunityDownloadSourceIndex;
        var instanceComposition = _instanceComposition;
        var hasVisibleEntries = DownloadResourceEntries.Count > 0 || _allDownloadResourceEntries.Count > 0;

        DownloadResourceLoadingText = T(
            "download.resource.surface.loading",
            ("surface_name", GetLocalizedDownloadResourceSurfaceName(route)));
        SetDownloadResourceLoading(!hasVisibleEntries);
        if (resetPage)
        {
            _downloadResourcePageIndex = 0;
            RaisePropertyChanged(nameof(DownloadResourcePageLabel));
            RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
            NotifyDownloadResourcePageCommandState();
        }

        _ = RefreshDownloadResourceResultsAsync(
            route,
            query,
            instanceComposition,
            communitySourcePreference,
            targetResultCount,
            refreshVersion,
            cts.Token,
            immediate ? 0 : 300,
            resetPage,
            targetPageIndex);
    }

    private async Task RefreshDownloadResourceResultsAsync(
        LauncherFrontendSubpageKey route,
        FrontendCommunityResourceQuery query,
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference,
        int targetResultCount,
        int refreshVersion,
        CancellationToken cancellationToken,
        int delayMilliseconds,
        bool resetPage,
        int? targetPageIndex)
    {
        try
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }

            var result = await Task.Run(
                () => FrontendCommunityResourceCatalogService.QueryResources(route, query, instanceComposition, communitySourcePreference, targetResultCount),
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested
                    || refreshVersion != _downloadResourceRefreshVersion
                    || _currentRoute.Subpage != route
                    || !IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
                {
                    return;
                }

                ApplyDownloadResourceQueryResult(result, query, resetPage, targetPageIndex);
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer query supersedes this one.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (refreshVersion != _downloadResourceRefreshVersion)
                {
                    return;
                }

                _downloadResourceRuntimeStates.Remove(route);
                DownloadResourceHintText = T("download.resource.hints.search_failed", ("message", ex.Message));
                ShowDownloadResourceHint = true;
                DownloadResourceEmptyStateHintText = T("download.resource.hints.retry_later");
                SetDownloadResourceLoading(false);
            });
        }
    }

    private FrontendCommunityResourceQuery BuildCurrentDownloadResourceQuery()
    {
        var selectedLoader = ShowDownloadResourceLoaderFilter
            ? GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex)
            : string.Empty;
        return new FrontendCommunityResourceQuery(
            DownloadResourceSearchQuery.Trim(),
            GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex),
            GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex),
            GetSelectedFilterValue(DownloadResourceSortOptions, SelectedDownloadResourceSortIndex),
            GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex),
            selectedLoader);
    }

    private void RefreshDownloadResourceFiltersForSelectedInstance()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            return;
        }

        if (!ShouldAutoSyncDownloadResourceFiltersWithInstance())
        {
            RaiseDownloadResourceFilterState();
            return;
        }

        ApplyCurrentInstanceDownloadResourceFilterSelection();
        RaiseDownloadResourceFilterState();
        ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
    }

    private void ApplyDownloadResourceQueryResult(
        FrontendCommunityResourceQueryResult result,
        FrontendCommunityResourceQuery query,
        bool resetPage,
        int? targetPageIndex)
    {
        var selectedSource = query.Source;
        var selectedTag = query.Tag;
        var selectedVersion = query.Version;
        var selectedLoader = ShowDownloadResourceLoaderFilter ? query.Loader : string.Empty;

        _downloadResourceRuntimeStates[_currentRoute.Subpage] = result.State;
        _downloadResourceHasMoreEntries = result.State.HasMoreEntries;
        _downloadResourceTotalEntryCount = result.State.TotalEntryCount;
        DownloadResourceHintText = LocalizeDownloadResourceHintText(result.State.HintText);
        // Keep the loading card copy stable until it has fully transitioned out.
        DownloadResourceEmptyStateHintText = string.Empty;
        ShowDownloadResourceHint = !string.IsNullOrWhiteSpace(DownloadResourceHintText);

        _downloadResourceSourceOptions = BuildDownloadResourceSourceOptions(result.State, selectedSource);
        _downloadResourceTagOptions = MergeFilterOptions(
            BuildFallbackDownloadResourceTagOptions(),
            result.State.TagOptions.Select(option => CreateDownloadResourceFilterOption(option.Label, option.FilterValue)),
            selectedTag);
        _downloadResourceVersionOptions =
            MergeFilterOptions(
                [CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty)],
                result.VersionOptions.Select(version => CreateDownloadResourceFilterOption(version, version)),
                selectedVersion);

        _selectedDownloadResourceSourceIndex = FindFilterOptionIndex(DownloadResourceSourceOptions, selectedSource);
        _selectedDownloadResourceTagIndex = FindFilterOptionIndex(DownloadResourceTagOptions, selectedTag);
        _selectedDownloadResourceVersionIndex = FindFilterOptionIndex(DownloadResourceVersionOptions, selectedVersion);
        _selectedDownloadResourceLoaderIndex = FindFilterOptionIndex(DownloadResourceLoaderOptions, selectedLoader);
        _allDownloadResourceEntries = CreateDownloadResourceEntries(result.State.Entries);
        if (!resetPage && targetPageIndex is not null)
        {
            _downloadResourcePageIndex = Math.Max(_downloadResourcePageIndex, targetPageIndex.Value);
        }

        RaiseDownloadResourceFilterState();
        ApplyDownloadResourceFilters(resetPage);
        SetDownloadResourceLoading(false);
    }

    private static int GetDownloadResourceTargetResultCount(int? targetPageIndex)
    {
        var effectivePageIndex = Math.Max(0, targetPageIndex ?? 0);
        return Math.Max(DownloadResourcePageSize * 2, (effectivePageIndex + 2) * DownloadResourcePageSize);
    }

    private int GetLoadedDownloadResourcePageCount()
    {
        return Math.Max(1, (int)Math.Ceiling(_allDownloadResourceEntries.Count / (double)DownloadResourcePageSize));
    }

    private void SetDownloadResourceLoading(bool isLoading)
    {
        if (_isDownloadResourceLoading == isLoading)
        {
            return;
        }

        _isDownloadResourceLoading = isLoading;
        RaisePropertyChanged(nameof(ShowDownloadResourceLoadingCard));
        RaisePropertyChanged(nameof(ShowDownloadResourceContent));
    }
}

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
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void ScheduleDownloadResourceRefresh(
        bool immediate,
        bool resetPage,
        int? targetPageIndex = null,
        FrontendCommunityResourceQuery? queryOverride = null)
    {
        if (!IsCurrentStandardRightPane(StandardRightPaneKind.DownloadResource))
        {
            return;
        }

        _downloadResourceRefreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _downloadResourceRefreshCts = cts;
        var refreshVersion = ++_downloadResourceRefreshVersion;
        var route = _currentRoute.Subpage;
        var query = queryOverride ?? BuildCurrentDownloadResourceQuery();
        var targetResultCount = GetDownloadResourceTargetResultCount(targetPageIndex);
        var communitySourcePreference = SelectedCommunityDownloadSourceIndex;
        var instanceComposition = _instanceComposition;
        var hasVisibleEntries = DownloadResourceEntries.Count > 0 || _allDownloadResourceEntries.Count > 0;
        var shouldShowLoadingAnimation = resetPage || !hasVisibleEntries;

        DownloadResourceLoadingText = T(
            "download.resource.surface.loading",
            ("surface_name", GetLocalizedDownloadResourceSurfaceName(route)));
        SetDownloadResourceLoading(shouldShowLoadingAnimation);
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
                    || !IsCurrentStandardRightPane(StandardRightPaneKind.DownloadResource))
                {
                    return;
                }

                ApplyDownloadResourceRuntimeState(result.State, query, resetPage, targetPageIndex);
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

                DownloadResourceHintText = T("download.resource.hints.search_failed", ("message", ex.Message));
                ShowDownloadResourceHint = true;
                DownloadResourceEmptyStateHintText = T("download.resource.hints.retry_later");
                SetDownloadResourceLoading(false);
            });
        }
    }

    private FrontendCommunityResourceQuery BuildCurrentDownloadResourceQuery(
        string? versionOverride = null,
        string? loaderOverride = null)
    {
        var selectedLoader = ShowDownloadResourceLoaderFilter
            ? loaderOverride ?? GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex)
            : string.Empty;
        var selectedVersion = versionOverride ?? GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex);
        return new FrontendCommunityResourceQuery(
            DownloadResourceSearchQuery.Trim(),
            GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex),
            GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex),
            GetSelectedFilterValue(DownloadResourceSortOptions, SelectedDownloadResourceSortIndex),
            selectedVersion,
            selectedLoader);
    }

    private void RefreshDownloadResourceFiltersForSelectedInstance()
    {
        if (!IsCurrentStandardRightPane(StandardRightPaneKind.DownloadResource))
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
        var instanceDrivenQuery = BuildCurrentDownloadResourceQuery(
            ResolveSelectedDownloadResourceVersionFilter(),
            ResolveSelectedDownloadResourceLoaderFilter());
        ScheduleDownloadResourceRefresh(
            immediate: true,
            resetPage: true,
            queryOverride: instanceDrivenQuery);
    }

    private void ApplyDownloadResourceRuntimeState(
        FrontendDownloadResourceState runtimeState,
        FrontendCommunityResourceQuery query,
        bool resetPage,
        int? targetPageIndex)
    {
        var selectedSource = query.Source;
        var selectedTag = query.Tag;
        var selectedSort = query.Sort;
        var selectedVersion = query.Version;
        var selectedLoader = ShowDownloadResourceLoaderFilter ? query.Loader : string.Empty;

        _downloadResourceRuntimeStates[_currentRoute.Subpage] = runtimeState;
        _downloadResourceHasMoreEntries = runtimeState.HasMoreEntries;
        _downloadResourceTotalEntryCount = runtimeState.TotalEntryCount;
        DownloadResourceHintText = LocalizeDownloadResourceHintText(runtimeState.HintText);
        // Keep the loading card copy stable until it has fully transitioned out.
        DownloadResourceEmptyStateHintText = string.Empty;
        ShowDownloadResourceHint = !string.IsNullOrWhiteSpace(DownloadResourceHintText);

        _downloadResourceSourceOptions = BuildDownloadResourceSourceOptions(runtimeState, selectedSource);
        _downloadResourceTagOptions = MergeFilterOptions(
            BuildFallbackDownloadResourceTagOptions(),
            runtimeState.TagOptions.Select(option => CreateDownloadResourceFilterOption(option.Label, option.FilterValue)),
            selectedTag);
        _downloadResourceVersionOptions =
            MergeFilterOptions(
                [CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty)],
                BuildDownloadResourceVersionValues(runtimeState, selectedVersion)
                    .Select(version => CreateDownloadResourceFilterOption(version, version)),
                selectedVersion);

        _selectedDownloadResourceSourceIndex = FindFilterOptionIndex(DownloadResourceSourceOptions, selectedSource);
        _selectedDownloadResourceTagIndex = FindFilterOptionIndex(DownloadResourceTagOptions, selectedTag);
        _selectedDownloadResourceSortIndex = FindFilterOptionIndex(DownloadResourceSortOptions, selectedSort);
        _selectedDownloadResourceVersionIndex = FindFilterOptionIndex(DownloadResourceVersionOptions, selectedVersion);
        _selectedDownloadResourceLoaderIndex = FindFilterOptionIndex(DownloadResourceLoaderOptions, selectedLoader);
        _downloadResourceSearchQuery = query.SearchText;
        _allDownloadResourceEntries = CreateDownloadResourceEntries(runtimeState.Entries);
        if (targetPageIndex is not null)
        {
            _downloadResourcePageIndex = Math.Max(0, targetPageIndex.Value);
        }

        RaiseDownloadResourceFilterState();
        ApplyDownloadResourceFilters(resetPage);
        SetDownloadResourceLoading(false);
    }

    private IReadOnlyList<string> BuildDownloadResourceVersionValues(
        FrontendDownloadResourceState runtimeState,
        string? selectedVersion)
    {
        var preferredVersion = ResolveSelectedDownloadResourceVersionFilter();
        var versions = new List<string>();
        AddDownloadResourceVersionIfMissing(versions, preferredVersion);
        AddDownloadResourceVersionIfMissing(versions, selectedVersion);

        foreach (var option in BuildDefaultDownloadResourceVersionOptions(preferredVersion).Skip(1))
        {
            AddDownloadResourceVersionIfMissing(versions, option.FilterValue);
        }

        foreach (var version in runtimeState.Entries.SelectMany(
                     entry => entry.SupportedVersions.Count == 0 ? [entry.Version] : entry.SupportedVersions))
        {
            AddDownloadResourceVersionIfMissing(versions, version);
        }

        return versions
            .OrderByDescending(ParseVersion)
            .ToArray();
    }

    private static void AddDownloadResourceVersionIfMissing(ICollection<string> versions, string? version)
    {
        var normalizedVersion = NormalizeMinecraftVersion(version);
        if (!string.IsNullOrWhiteSpace(normalizedVersion)
            && !versions.Contains(normalizedVersion, StringComparer.OrdinalIgnoreCase))
        {
            versions.Add(normalizedVersion);
        }
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

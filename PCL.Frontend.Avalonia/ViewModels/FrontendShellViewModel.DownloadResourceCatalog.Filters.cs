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
    public string DownloadResourceSearchQuery
    {
        get => _downloadResourceSearchQuery;
        set
        {
            SetProperty(ref _downloadResourceSearchQuery, value);
        }
    }
    public IReadOnlyList<DownloadResourceFilterOptionViewModel> DownloadResourceSourceOptions => _downloadResourceSourceOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> DownloadResourceTagOptions => _downloadResourceTagOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> DownloadResourceSortOptions => _downloadResourceSortOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> DownloadResourceVersionOptions => _downloadResourceVersionOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> DownloadResourceLoaderOptions => _downloadResourceLoaderOptions;

    public DownloadResourceFilterOptionViewModel? SelectedDownloadResourceSourceOption
    {
        get => _selectedDownloadResourceSourceOption;
        set => SetSelectedDownloadResourceOption(
            DownloadResourceSourceOptions,
            value,
            ref _selectedDownloadResourceSourceOption,
            nameof(SelectedDownloadResourceSourceOption),
            ref _selectedDownloadResourceSourceIndex,
            nameof(SelectedDownloadResourceSourceIndex),
            UpdateDownloadResourceHint);
    }

    public DownloadResourceFilterOptionViewModel? SelectedDownloadResourceTagOption
    {
        get => _selectedDownloadResourceTagOption;
        set => SetSelectedDownloadResourceOption(
            DownloadResourceTagOptions,
            value,
            ref _selectedDownloadResourceTagOption,
            nameof(SelectedDownloadResourceTagOption),
            ref _selectedDownloadResourceTagIndex,
            nameof(SelectedDownloadResourceTagIndex));
    }

    public DownloadResourceFilterOptionViewModel? SelectedDownloadResourceSortOption
    {
        get => _selectedDownloadResourceSortOption;
        set => SetSelectedDownloadResourceOption(
            DownloadResourceSortOptions,
            value,
            ref _selectedDownloadResourceSortOption,
            nameof(SelectedDownloadResourceSortOption),
            ref _selectedDownloadResourceSortIndex,
            nameof(SelectedDownloadResourceSortIndex));
    }

    public DownloadResourceFilterOptionViewModel? SelectedDownloadResourceVersionOption
    {
        get => _selectedDownloadResourceVersionOption;
        set => SetSelectedDownloadResourceOption(
            DownloadResourceVersionOptions,
            value,
            ref _selectedDownloadResourceVersionOption,
            nameof(SelectedDownloadResourceVersionOption),
            ref _selectedDownloadResourceVersionIndex,
            nameof(SelectedDownloadResourceVersionIndex));
    }

    public DownloadResourceFilterOptionViewModel? SelectedDownloadResourceLoaderOption
    {
        get => _selectedDownloadResourceLoaderOption;
        set => SetSelectedDownloadResourceOption(
            DownloadResourceLoaderOptions,
            value,
            ref _selectedDownloadResourceLoaderOption,
            nameof(SelectedDownloadResourceLoaderOption),
            ref _selectedDownloadResourceLoaderIndex,
            nameof(SelectedDownloadResourceLoaderIndex));
    }

    public int SelectedDownloadResourceSourceIndex
    {
        get => _selectedDownloadResourceSourceIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceSourceOptions);
            if (SetProperty(ref _selectedDownloadResourceSourceIndex, nextValue) &&
                IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
            {
                UpdateDownloadResourceHint();
            }
        }
    }

    public int SelectedDownloadResourceTagIndex
    {
        get => _selectedDownloadResourceTagIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceTagOptions);
            SetProperty(ref _selectedDownloadResourceTagIndex, nextValue);
        }
    }

    public int SelectedDownloadResourceSortIndex
    {
        get => _selectedDownloadResourceSortIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceSortOptions);
            SetProperty(ref _selectedDownloadResourceSortIndex, nextValue);
        }
    }

    public int SelectedDownloadResourceVersionIndex
    {
        get => _selectedDownloadResourceVersionIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceVersionOptions);
            SetProperty(ref _selectedDownloadResourceVersionIndex, nextValue);
        }
    }

    public int SelectedDownloadResourceLoaderIndex
    {
        get => _selectedDownloadResourceLoaderIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceLoaderOptions);
            SetProperty(ref _selectedDownloadResourceLoaderIndex, nextValue);
        }
    }
    private void ResetDownloadResourceFilterState()
    {
        _downloadResourceSearchQuery = string.Empty;
        _selectedDownloadResourceSourceIndex = 0;
        _selectedDownloadResourceTagIndex = 0;
        _selectedDownloadResourceSortIndex = 0;
        _downloadResourcePageIndex = 0;
        _downloadResourceTotalPages = 1;
        _downloadResourceTotalEntryCount = 0;
        _downloadResourceHasMoreEntries = false;
        if (ShouldAutoSyncDownloadResourceFiltersWithInstance())
        {
            ApplyCurrentInstanceDownloadResourceFilterSelection();
        }
        else
        {
            _selectedDownloadResourceVersionIndex = 0;
            _selectedDownloadResourceLoaderIndex = 0;
        }

        SyncSelectedDownloadResourceOptions();
        UpdateDownloadResourceHint();
    }

    private void ResetDownloadResourceFilters()
    {
        ResetDownloadResourceFilterState();
        RaiseDownloadResourceFilterState();
        ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
        AddActivity(
            T("download.resource.activities.reset_filters"),
            T("download.resource.activities.reset_filters_message", ("surface_title", DownloadResourceSurfaceTitle)));
    }

    private void SearchDownloadResource()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            return;
        }

        ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
    }

    private void ApplyDownloadResourceFilters(bool resetPage)
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            return;
        }

        if (resetPage)
        {
            _downloadResourcePageIndex = 0;
        }

        IEnumerable<DownloadResourceEntryViewModel> entries = _allDownloadResourceEntries;

        var searchQuery = DownloadResourceSearchQuery.Trim();
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            entries = entries.Where(entry => entry.SearchText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        var sourceFilter = GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex);
        if (!string.IsNullOrWhiteSpace(sourceFilter))
        {
            entries = entries.Where(entry => string.Equals(entry.Source, sourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        var tagFilter = GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex);
        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            entries = entries.Where(entry => entry.Tags.Any(tag => string.Equals(tag, tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var versionFilter = GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex);
        if (!string.IsNullOrWhiteSpace(versionFilter))
        {
            entries = entries.Where(entry =>
                string.Equals(entry.Version, versionFilter, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedVersions.Any(version => string.Equals(version, versionFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var loaderFilter = ShowDownloadResourceLoaderFilter
            ? GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(loaderFilter))
        {
            entries = entries.Where(entry =>
                string.Equals(entry.Loader, loaderFilter, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedLoaders.Any(loader => string.Equals(loader, loaderFilter, StringComparison.OrdinalIgnoreCase)));
        }

        entries = GetSelectedFilterValue(DownloadResourceSortOptions, SelectedDownloadResourceSortIndex) switch
        {
            "relevance" => entries.OrderByDescending(entry => string.IsNullOrWhiteSpace(DownloadResourceSearchQuery)
                || entry.Title.Contains(DownloadResourceSearchQuery, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(entry => entry.DownloadCount),
            "downloads" => entries.OrderByDescending(entry => entry.DownloadCount),
            "follows" => entries.OrderByDescending(entry => entry.FollowCount),
            "release" => entries.OrderByDescending(entry => entry.ReleaseRank),
            "update" => entries.OrderByDescending(entry => entry.UpdateRank),
            _ => entries
        };

        var filteredEntries = entries.ToArray();
        _downloadResourceTotalPages = _downloadResourceTotalEntryCount > 0
            ? Math.Max(1, (int)Math.Ceiling(_downloadResourceTotalEntryCount / (double)DownloadResourcePageSize))
            : Math.Max(1, (int)Math.Ceiling(filteredEntries.Length / (double)DownloadResourcePageSize));
        _downloadResourcePageIndex = Math.Clamp(_downloadResourcePageIndex, 0, _downloadResourceTotalPages - 1);

        var pagedEntries = filteredEntries
            .Skip(_downloadResourcePageIndex * DownloadResourcePageSize)
            .Take(DownloadResourcePageSize)
            .ToArray();

        ReplaceItems(DownloadResourceEntries, pagedEntries);
        QueueDownloadResourceIconLoad(pagedEntries);
        RememberCurrentDownloadResourceViewState();
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        RaisePropertyChanged(nameof(DownloadResourcePageLabel));
        RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
        NotifyDownloadResourcePageCommandState();
    }

    private void RaiseDownloadResourceFilterState()
    {
        SyncSelectedDownloadResourceOptions();
        RaisePropertyChanged(nameof(DownloadResourceSearchQuery));
        RaisePropertyChanged(nameof(DownloadResourceSearchWatermark));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceCardTitle));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceTitle));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceSummary));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceActionText));
        RaisePropertyChanged(nameof(ShowDownloadResourceCurrentInstanceCard));
        RaisePropertyChanged(nameof(DownloadResourceSourceOptions));
        RaisePropertyChanged(nameof(DownloadResourceTagOptions));
        RaisePropertyChanged(nameof(DownloadResourceSortOptions));
        RaisePropertyChanged(nameof(DownloadResourceVersionOptions));
        RaisePropertyChanged(nameof(DownloadResourceLoaderOptions));
        RaisePropertyChanged(nameof(ShowDownloadResourceLoaderFilter));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSourceIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceTagIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSortIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceVersionIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceLoaderIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSourceOption));
        RaisePropertyChanged(nameof(SelectedDownloadResourceTagOption));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSortOption));
        RaisePropertyChanged(nameof(SelectedDownloadResourceVersionOption));
        RaisePropertyChanged(nameof(SelectedDownloadResourceLoaderOption));
        RaisePropertyChanged(nameof(ShowDownloadResourceInstallModPackAction));
        RaisePropertyChanged(nameof(DownloadResourcePageLabel));
        RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
    }

    private void UpdateDownloadResourceHint()
    {
        ShowDownloadResourceHint = !string.IsNullOrWhiteSpace(DownloadResourceHintText);
    }

    private void RememberCurrentDownloadResourceViewState()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            return;
        }

        _downloadResourceViewStates[_currentRoute.Subpage] = new DownloadResourceSurfaceViewState(
            DownloadResourceSearchQuery.Trim(),
            GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex),
            GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex),
            GetSelectedFilterValue(DownloadResourceSortOptions, SelectedDownloadResourceSortIndex),
            GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex),
            ShowDownloadResourceLoaderFilter
                ? GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex)
                : string.Empty,
            _downloadResourcePageIndex);
    }

    private void PreviewDownloadResourceFilters(bool resetPage)
    {
        if (_allDownloadResourceEntries.Count == 0)
        {
            return;
        }

        ApplyDownloadResourceFilters(resetPage);
    }
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDownloadResourceSortOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.default"), string.Empty),
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.relevance"), "relevance"),
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.downloads"), "downloads"),
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.follows"), "follows"),
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.release"), "release"),
            new DownloadResourceFilterOptionViewModel(T("download.resource.sort.update"), "update")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultDownloadResourceVersionOptions(string? preferredVersion = null)
    {
        return MergeFilterOptions(
            [
            new DownloadResourceFilterOptionViewModel(T("common.filters.any"), string.Empty),
            new DownloadResourceFilterOptionViewModel("26.1.1", "26.1.1"),
            new DownloadResourceFilterOptionViewModel("26.1", "26.1"),
            new DownloadResourceFilterOptionViewModel("1.21.11", "1.21.11"),
            new DownloadResourceFilterOptionViewModel("1.21.1", "1.21.1"),
            new DownloadResourceFilterOptionViewModel("1.20.6", "1.20.6"),
            new DownloadResourceFilterOptionViewModel("1.20.1", "1.20.1"),
            new DownloadResourceFilterOptionViewModel("1.19.4", "1.19.4"),
            new DownloadResourceFilterOptionViewModel("1.19.2", "1.19.2"),
            new DownloadResourceFilterOptionViewModel("1.18.2", "1.18.2"),
            new DownloadResourceFilterOptionViewModel("1.16.5", "1.16.5"),
            new DownloadResourceFilterOptionViewModel("1.12.2", "1.12.2"),
            new DownloadResourceFilterOptionViewModel("1.10.2", "1.10.2"),
            new DownloadResourceFilterOptionViewModel("1.8.9", "1.8.9"),
            new DownloadResourceFilterOptionViewModel("1.7.10", "1.7.10")
            ],
            [],
            preferredVersion);
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultResourceLoaderOptions()
    {
        return BuildDefaultResourceLoaderOptions(hideQuiltLoader: false);
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultResourceLoaderOptions(bool hideQuiltLoader)
    {
        var visibleLoaders = FrontendLoaderVisibilityService.FilterVisibleLoaders(
            ["Forge", "NeoForge", "Fabric", "Quilt"],
            hideQuiltLoader);
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty),
            .. visibleLoaders.Select(loader => CreateDownloadResourceFilterOption(loader, loader))
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultShaderLoaderOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty),
            CreateDownloadResourceFilterOption("OptiFine", "OptiFine"),
            CreateDownloadResourceFilterOption("Iris", "Iris")
        ];
    }

    private static int FindFilterOptionIndex(IReadOnlyList<DownloadResourceFilterOptionViewModel> options, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index].FilterValue, value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private void ApplyCurrentInstanceDownloadResourceFilterSelection()
    {
        var preferredVersion = ResolveSelectedDownloadResourceVersionFilter();
        _downloadResourceVersionOptions = MergeFilterOptions(
            BuildDefaultDownloadResourceVersionOptions(preferredVersion),
            DownloadResourceVersionOptions.Skip(1),
            preferredVersion);
        _selectedDownloadResourceVersionIndex = FindFilterOptionIndex(DownloadResourceVersionOptions, preferredVersion ?? string.Empty);

        var preferredLoader = ResolveSelectedDownloadResourceLoaderFilter();
        _selectedDownloadResourceLoaderIndex = ShowDownloadResourceLoaderFilter
            ? FindFilterOptionIndex(DownloadResourceLoaderOptions, preferredLoader ?? string.Empty)
            : 0;
    }

    private string? ResolveSelectedDownloadResourceVersionFilter()
    {
        return NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
    }

    private string? ResolveSelectedDownloadResourceLoaderFilter()
    {
        return ResolvePreferredInstanceLoaderLabel(_instanceComposition, _currentRoute.Subpage);
    }

    private void SyncSelectedDownloadResourceOptions()
    {
        _selectedDownloadResourceSourceOption = GetFilterOptionAt(DownloadResourceSourceOptions, _selectedDownloadResourceSourceIndex);
        _selectedDownloadResourceTagOption = GetFilterOptionAt(DownloadResourceTagOptions, _selectedDownloadResourceTagIndex);
        _selectedDownloadResourceSortOption = GetFilterOptionAt(DownloadResourceSortOptions, _selectedDownloadResourceSortIndex);
        _selectedDownloadResourceVersionOption = GetFilterOptionAt(DownloadResourceVersionOptions, _selectedDownloadResourceVersionIndex);
        _selectedDownloadResourceLoaderOption = GetFilterOptionAt(DownloadResourceLoaderOptions, _selectedDownloadResourceLoaderIndex);
    }

    private void SetSelectedDownloadResourceOption(
        IReadOnlyList<DownloadResourceFilterOptionViewModel> options,
        DownloadResourceFilterOptionViewModel? value,
        ref DownloadResourceFilterOptionViewModel? field,
        string selectedOptionPropertyName,
        ref int indexField,
        string selectedIndexPropertyName,
        Action? afterIndexChanged = null)
    {
        if (value is null && options.Count > 0)
        {
            return;
        }

        var nextValue = ResolveSelectedFilterOption(options, value, indexField);
        if (ReferenceEquals(field, nextValue))
        {
            return;
        }

        field = nextValue;
        RaisePropertyChanged(selectedOptionPropertyName);

        var nextIndex = FindFilterOptionIndex(options, nextValue?.FilterValue ?? string.Empty);
        if (indexField == nextIndex)
        {
            return;
        }

        indexField = nextIndex;
        RaisePropertyChanged(selectedIndexPropertyName);
        afterIndexChanged?.Invoke();
    }

    private static DownloadResourceFilterOptionViewModel? ResolveSelectedFilterOption(
        IReadOnlyList<DownloadResourceFilterOptionViewModel> options,
        DownloadResourceFilterOptionViewModel? selectedOption,
        int fallbackIndex)
    {
        if (options.Count == 0)
        {
            return null;
        }

        if (selectedOption is not null)
        {
            var matchedOption = options.FirstOrDefault(option =>
                ReferenceEquals(option, selectedOption)
                || (string.Equals(option.FilterValue, selectedOption.FilterValue, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(option.Label, selectedOption.Label, StringComparison.OrdinalIgnoreCase)));
            if (matchedOption is not null)
            {
                return matchedOption;
            }
        }

        return GetFilterOptionAt(options, fallbackIndex);
    }

    private static DownloadResourceFilterOptionViewModel? GetFilterOptionAt(
        IReadOnlyList<DownloadResourceFilterOptionViewModel> options,
        int index)
    {
        return options.Count == 0 ? null : options[Math.Clamp(index, 0, options.Count - 1)];
    }

    private static IReadOnlyList<DownloadResourceFilterOptionViewModel> MergeFilterOptions(
        IReadOnlyList<DownloadResourceFilterOptionViewModel> baseOptions,
        IEnumerable<DownloadResourceFilterOptionViewModel> additionalOptions,
        string? selectedValue = null)
    {
        var merged = new List<DownloadResourceFilterOptionViewModel>(baseOptions.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var options = baseOptions.Concat(additionalOptions);
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            options = options.Concat([new DownloadResourceFilterOptionViewModel(selectedValue, selectedValue)]);
        }

        foreach (var option in options)
        {
            var key = string.IsNullOrWhiteSpace(option.FilterValue) ? option.Label : option.FilterValue;
            if (seen.Add(key))
            {
                merged.Add(option);
            }
        }

        return merged;
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDownloadResourceSourceOptions(
        FrontendDownloadResourceState runtimeState,
        string? selectedSource = null)
    {
        var primarySource = runtimeState.Entries
            .Select(entry => entry.Source)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "CurseForge";
        IReadOnlyList<DownloadResourceFilterOptionViewModel> baseOptions = runtimeState.SupportsSecondarySource
            ? [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption("CurseForge", "CurseForge"),
                CreateDownloadResourceFilterOption("Modrinth", "Modrinth")
            ]
            : [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption(primarySource, primarySource)
            ];

        return MergeFilterOptions(baseOptions, [], selectedSource);
    }
    private static int ClampFilterIndex(int value, IReadOnlyList<DownloadResourceFilterOptionViewModel> options)
    {
        return options.Count == 0 ? 0 : Math.Clamp(value, 0, options.Count - 1);
    }

    private static string GetSelectedFilterValue(IReadOnlyList<DownloadResourceFilterOptionViewModel> options, int index)
    {
        return index >= 0 && index < options.Count ? options[index].FilterValue : string.Empty;
    }
}

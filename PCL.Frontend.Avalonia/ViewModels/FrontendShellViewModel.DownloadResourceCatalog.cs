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
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const int DownloadResourcePageSize = 40;
    private readonly ActionCommand _resetDownloadResourceFiltersCommand;
    private readonly ActionCommand _installDownloadResourceModPackCommand;
    private readonly ActionCommand _searchDownloadResourceCommand;
    private readonly ActionCommand _firstDownloadResourcePageCommand;
    private readonly ActionCommand _previousDownloadResourcePageCommand;
    private readonly ActionCommand _nextDownloadResourcePageCommand;
    private CancellationTokenSource? _downloadResourceRefreshCts;
    private int _downloadResourceRefreshVersion;
    private string _downloadResourceSearchQuery = string.Empty;
    private string _downloadResourceSurfaceTitle = string.Empty;
    private string _downloadResourceLoadingText = string.Empty;
    private string _downloadResourceEmptyStateText = string.Empty;
    private string _downloadResourceEmptyStateHintText = string.Empty;
    private string _downloadResourceHintText = string.Empty;
    private bool _showDownloadResourceHint;
    private bool _showDownloadResourceInstallModPackAction;
    private int _selectedDownloadResourceSourceIndex;
    private int _selectedDownloadResourceTagIndex;
    private int _selectedDownloadResourceSortIndex;
    private int _selectedDownloadResourceVersionIndex;
    private int _selectedDownloadResourceLoaderIndex;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceSourceOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceTagOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceSortOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceVersionOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceLoaderOption;
    private int _downloadResourcePageIndex;
    private int _downloadResourceTotalPages = 1;
    private int _downloadResourceTotalEntryCount;
    private bool _downloadResourceHasMoreEntries;
    private bool _downloadResourceSupportsModrinth = true;
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceSourceOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceTagOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceSortOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceVersionOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceLoaderOptions = [];
    private IReadOnlyList<DownloadResourceEntryViewModel> _allDownloadResourceEntries = [];

    public ObservableCollection<DownloadResourceEntryViewModel> DownloadResourceEntries { get; } = [];
    public ObservableCollection<DownloadResourcePaginationItemViewModel> DownloadResourcePaginationItems { get; } = [];

    public string DownloadResourceSearchQuery
    {
        get => _downloadResourceSearchQuery;
        set
        {
            SetProperty(ref _downloadResourceSearchQuery, value);
        }
    }

    public string DownloadResourceSurfaceTitle
    {
        get => _downloadResourceSurfaceTitle;
        private set => SetProperty(ref _downloadResourceSurfaceTitle, value);
    }

    public string DownloadResourceSearchWatermark => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.DownloadMod => T("download.resource.search.mod"),
        LauncherFrontendSubpageKey.DownloadPack => T("download.resource.search.pack"),
        LauncherFrontendSubpageKey.DownloadDataPack => T("download.resource.search.data_pack"),
        LauncherFrontendSubpageKey.DownloadResourcePack => T("download.resource.search.resource_pack"),
        LauncherFrontendSubpageKey.DownloadShader => T("download.resource.search.shader"),
        LauncherFrontendSubpageKey.DownloadWorld => T("download.resource.search.world"),
        _ => T("download.resource.search.default")
    };

    public string DownloadResourceCurrentInstanceTitle => _instanceComposition.Selection.HasSelection
        ? _instanceComposition.Selection.InstanceName
        : T("download.resource.current_instance.none_selected");

    public string DownloadResourceCurrentInstanceSummary
    {
        get
        {
            if (!_instanceComposition.Selection.HasSelection)
            {
                return T("download.resource.current_instance.summary_none_selected");
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_instanceComposition.Selection.VanillaVersion))
            {
                parts.Add($"Minecraft {_instanceComposition.Selection.VanillaVersion}");
            }

            var loader = ResolveSelectedInstanceLoaderLabel();
            if (!string.IsNullOrWhiteSpace(loader))
            {
                parts.Add(loader);
            }

            parts.Add(T("download.resource.current_instance.summary_suffix"));
            return string.Join(" • ", parts);
        }
    }

    private string GetLocalizedDownloadResourceSurfaceName(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => T("download.resource.search.mod"),
            LauncherFrontendSubpageKey.DownloadPack => T("download.resource.search.pack"),
            LauncherFrontendSubpageKey.DownloadDataPack => T("download.resource.search.data_pack"),
            LauncherFrontendSubpageKey.DownloadResourcePack => T("download.resource.search.resource_pack"),
            LauncherFrontendSubpageKey.DownloadShader => T("download.resource.search.shader"),
            LauncherFrontendSubpageKey.DownloadWorld => T("download.resource.search.world"),
            _ => T("download.resource.search.default")
        };
    }

    public bool ShowDownloadResourceCurrentInstanceCard => _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack;

    public bool ShowDownloadResourceLoaderFilter => _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadWorld;

    public ActionCommand SelectDownloadResourceInstanceCommand => new(() => _ = SelectCommunityProjectInstanceAsync());

    public string DownloadResourceLoadingText
    {
        get => _downloadResourceLoadingText;
        private set => SetProperty(ref _downloadResourceLoadingText, value);
    }

    public string DownloadResourceEmptyStateText
    {
        get => _downloadResourceEmptyStateText;
        private set => SetProperty(ref _downloadResourceEmptyStateText, value);
    }

    public string DownloadResourceEmptyStateHintText
    {
        get => _downloadResourceEmptyStateHintText;
        private set
        {
            if (SetProperty(ref _downloadResourceEmptyStateHintText, value))
            {
                RaisePropertyChanged(nameof(ShowDownloadResourceEmptyStateHint));
            }
        }
    }

    public bool ShowDownloadResourceEmptyStateHint => !string.IsNullOrWhiteSpace(DownloadResourceEmptyStateHintText);

    public string DownloadResourceHintText
    {
        get => _downloadResourceHintText;
        private set => SetProperty(ref _downloadResourceHintText, value);
    }

    public bool ShowDownloadResourceHint
    {
        get => _showDownloadResourceHint;
        private set => SetProperty(ref _showDownloadResourceHint, value);
    }

    public bool ShowDownloadResourceInstallModPackAction
    {
        get => _showDownloadResourceInstallModPackAction;
        private set => SetProperty(ref _showDownloadResourceInstallModPackAction, value);
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

    private void RefreshDownloadResourceSurface()
    {
        DownloadResourceSurfaceTitle = string.Empty;
        DownloadResourceLoadingText = string.Empty;
        DownloadResourceEmptyStateText = T("download.resource.empty.default");
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = false;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceTotalEntryCount = 0;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions = [];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceVersionOptions = BuildDefaultDownloadResourceVersionOptions();
        _downloadResourceLoaderOptions = BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _allDownloadResourceEntries = [];
        ReplaceItems(DownloadResourceEntries, []);
        SetDownloadResourceLoading(false);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            RaisePropertyChanged(nameof(DownloadResourceSearchWatermark));
            RaisePropertyChanged(nameof(DownloadResourceSourceOptions));
            RaisePropertyChanged(nameof(DownloadResourceTagOptions));
            RaisePropertyChanged(nameof(DownloadResourceLoaderOptions));
            RaisePropertyChanged(nameof(ShowDownloadResourceCurrentInstanceCard));
            RaisePropertyChanged(nameof(ShowDownloadResourceLoaderFilter));
            RaisePropertyChanged(nameof(HasDownloadResourceEntries));
            RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
            RaisePropertyChanged(nameof(ShowDownloadResourceLoadingCard));
            RaisePropertyChanged(nameof(ShowDownloadResourceContent));
            RaisePropertyChanged(nameof(DownloadResourcePageLabel));
            RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
            NotifyDownloadResourcePageCommandState();
            return;
        }

        var (showInstallModPackAction, useShaderLoaderOptions) = GetDownloadResourceSurfaceDescriptor(_currentRoute.Subpage);
        var surfaceTitle = GetLocalizedDownloadResourceSurfaceName(_currentRoute.Subpage);
        DownloadResourceSurfaceTitle = T("download.resource.surface.title", ("surface_name", surfaceTitle));
        DownloadResourceLoadingText = T("download.resource.surface.loading", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateText = T("download.resource.surface.empty", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions =
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("CurseForge", "CurseForge"),
            CreateDownloadResourceFilterOption("Modrinth", "Modrinth")
        ];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceVersionOptions = BuildDefaultDownloadResourceVersionOptions(
            ShouldAutoSyncDownloadResourceFiltersWithInstance()
                ? ResolveSelectedDownloadResourceVersionFilter()
                : null);
        _downloadResourceLoaderOptions = useShaderLoaderOptions
            ? BuildDefaultShaderLoaderOptions()
            : BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _downloadResourceRuntimeStates.Remove(_currentRoute.Subpage);
        ResetDownloadResourceFilterState();
        RaiseDownloadResourceFilterState();
        SetDownloadResourceLoading(true);
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
    }

    private void ConfigureDownloadResourceSurface(
        string surfaceTitle,
        bool supportsModrinth,
        bool showInstallModPackAction,
        bool useShaderLoader,
        IReadOnlyList<DownloadResourceFilterOptionViewModel> tagOptions,
        IReadOnlyList<DownloadResourceEntryViewModel> entries)
    {
        DownloadResourceSurfaceTitle = T("download.resource.surface.title", ("surface_name", surfaceTitle));
        DownloadResourceLoadingText = T("download.resource.surface.loading", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateText = T("download.resource.surface.empty", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = T("download.resource.hints.modrinth_unavailable");
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = supportsModrinth;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceSourceOptions = supportsModrinth
            ? [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption("CurseForge", "CurseForge"),
                CreateDownloadResourceFilterOption("Modrinth", "Modrinth")
            ]
            : [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption("CurseForge", "CurseForge")
            ];
        _downloadResourceTagOptions = tagOptions;
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceLoaderOptions = useShaderLoader
            ? [
                CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty),
                CreateDownloadResourceFilterOption("vanilla_compatible", "vanilla_compatible"),
                CreateDownloadResourceFilterOption("Iris", "Iris"),
                CreateDownloadResourceFilterOption("OptiFine", "OptiFine")
            ]
            : BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _allDownloadResourceEntries = entries;
        ResetDownloadResourceFilterState();
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

    private void InstallDownloadResourceModPack()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource) || _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack)
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.no_surface"));
            return;
        }

        var visibleEntry = DownloadResourceEntries.FirstOrDefault();
        if (visibleEntry is null)
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.no_filtered_result"));
            return;
        }

        if (!_downloadResourceRuntimeStates.TryGetValue(_currentRoute.Subpage, out var resourceState))
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.runtime_state_missing"));
            return;
        }

        var sourceEntry = resourceState.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Title, visibleEntry.Title, StringComparison.Ordinal)
            && string.Equals(entry.Source, visibleEntry.Source, StringComparison.Ordinal));
        if (sourceEntry is null || string.IsNullOrWhiteSpace(sourceEntry.TargetPath) || !Directory.Exists(sourceEntry.TargetPath))
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.target_missing"));
            return;
        }

        try
        {
            var launcherFolder = ResolveDownloadLauncherFolder();
            var versionsDirectory = Path.Combine(launcherFolder, "versions");
            Directory.CreateDirectory(versionsDirectory);

            var targetName = string.IsNullOrWhiteSpace(DownloadInstallName)
                ? visibleEntry.Title
                : DownloadInstallName.Trim();
            var targetDirectory = GetUniqueInstallDirectoryPath(Path.Combine(
                versionsDirectory,
                SanitizeInstallDirectoryName(targetName)));

            CopyDirectory(sourceEntry.TargetPath, targetDirectory);

            var summaryDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "download-installs");
            Directory.CreateDirectory(summaryDirectory);
            var summaryPath = Path.Combine(summaryDirectory, $"{Path.GetFileName(targetDirectory)}.txt");
            File.WriteAllText(
                summaryPath,
                string.Join(Environment.NewLine,
                [
                    $"Time: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                    $"Source modpack: {visibleEntry.Title}",
                    $"Source directory: {sourceEntry.TargetPath}",
                    $"Target directory: {targetDirectory}",
                    $"Current install name: {DownloadInstallName}"
                ]),
                new UTF8Encoding(false));

            ReloadDownloadComposition();
            InitializeDownloadInstallSurface();
            RefreshDownloadResourceSurface();
            RaisePropertyChanged(nameof(DownloadInstallName));
            OpenInstanceTarget(T("download.resource.activities.install_pack"), targetDirectory, T("download.resource.install_pack.open_target_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.resource.activities.install_pack_failed"), ex.Message);
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

        var loaderFilter = GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex);
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
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        RaisePropertyChanged(nameof(DownloadResourcePageLabel));
        RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
        NotifyDownloadResourcePageCommandState();
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

    private void RaiseDownloadResourceFilterState()
    {
        SyncSelectedDownloadResourceOptions();
        RaisePropertyChanged(nameof(DownloadResourceSearchQuery));
        RaisePropertyChanged(nameof(DownloadResourceSearchWatermark));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceTitle));
        RaisePropertyChanged(nameof(DownloadResourceCurrentInstanceSummary));
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

    private void PreviewDownloadResourceFilters(bool resetPage)
    {
        if (_allDownloadResourceEntries.Count == 0)
        {
            return;
        }

        ApplyDownloadResourceFilters(resetPage);
    }

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
        return new FrontendCommunityResourceQuery(
            DownloadResourceSearchQuery.Trim(),
            GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex),
            GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex),
            GetSelectedFilterValue(DownloadResourceSortOptions, SelectedDownloadResourceSortIndex),
            GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex),
            GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex));
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
        var selectedLoader = query.Loader;

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

    private static (bool ShowInstallModPackAction, bool UseShaderLoaderOptions) GetDownloadResourceSurfaceDescriptor(
        LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => (false, false),
            LauncherFrontendSubpageKey.DownloadPack => (true, false),
            LauncherFrontendSubpageKey.DownloadDataPack => (false, false),
            LauncherFrontendSubpageKey.DownloadResourcePack => (false, false),
            LauncherFrontendSubpageKey.DownloadShader => (false, true),
            LauncherFrontendSubpageKey.DownloadWorld => (false, false),
            _ => (false, false)
        };
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
        return ResolveSelectedInstanceLoaderLabel();
    }

    private bool ShouldAutoSyncDownloadResourceFiltersWithInstance()
    {
        return _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack;
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

    private IReadOnlyList<DownloadResourceEntryViewModel> CreateDownloadResourceEntries(IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return entries
            .Select(entry =>
            {
                var icon = LoadCachedBitmapFromPath(entry.IconPath);
                if (icon is null && !string.IsNullOrWhiteSpace(entry.IconName))
                {
                    icon = LoadLauncherBitmap("Images", "Blocks", entry.IconName);
                }

                return new DownloadResourceEntryViewModel(
                    icon,
                    entry.Title,
                    entry.Info,
                    entry.Source,
                    entry.Version,
                    entry.Loader,
                    entry.Tags,
                    entry.Tags.Select(LocalizeDownloadResourceTag).ToArray(),
                    entry.SupportedVersions,
                    entry.SupportedLoaders,
                    entry.DownloadCount,
                    entry.FollowCount,
                    entry.ReleaseRank,
                    entry.UpdateRank,
                    FormatDownloadResourceVersionLabel(entry.Version),
                    FormatDownloadResourceDownloadCountLabel(entry.DownloadCount),
                    FormatDownloadResourceUpdatedLabel(entry.UpdateRank, entry.ReleaseRank),
                    LocalizeDownloadResourceActionText(entry.ActionText),
                    string.IsNullOrWhiteSpace(entry.TargetPath)
                        ? new ActionCommand(() => AddActivity(
                            T("download.resource.activities.entry_action", ("entry_title", entry.Title)),
                            BuildActivityDetail(entry.Info, entry.Source)))
                        : FrontendCommunityProjectService.TryParseCompDetailTarget(entry.TargetPath, out var projectId)
                            ? new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title, entry.Version, entry.Loader, _currentRoute.Subpage))
                            : CreateOpenTargetCommand(
                                T("download.resource.activities.open_page", ("entry_title", entry.Title)),
                                entry.TargetPath,
                                entry.TargetPath),
                    entry.IconUrl);
            })
            .ToArray();
    }

    private void QueueDownloadResourceIconLoad(IEnumerable<DownloadResourceEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            if (!entry.TryBeginIconLoad())
            {
                continue;
            }

            _ = LoadDownloadResourceIconAsync(entry);
        }
    }

    private async Task LoadDownloadResourceIconAsync(DownloadResourceEntryViewModel entry)
    {
        var iconPath = await FrontendCommunityIconCache.EnsureCachedIconAsync(entry.IconUrl);
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        var bitmap = await Task.Run(() => LoadCachedBitmapFromPath(iconPath));
        if (bitmap is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => entry.ApplyIcon(bitmap));
    }

    private static int ClampFilterIndex(int value, IReadOnlyList<DownloadResourceFilterOptionViewModel> options)
    {
        return options.Count == 0 ? 0 : Math.Clamp(value, 0, options.Count - 1);
    }

    private static string GetSelectedFilterValue(IReadOnlyList<DownloadResourceFilterOptionViewModel> options, int index)
    {
        return index >= 0 && index < options.Count ? options[index].FilterValue : string.Empty;
    }

    private DownloadResourceFilterOptionViewModel CreateDownloadResourceFilterOption(string label, string filterValue, bool isHeader = false)
    {
        return new DownloadResourceFilterOptionViewModel(
            isHeader ? LocalizeDownloadResourceTagGroup(label) : LocalizeDownloadResourceTag(label),
            filterValue,
            isHeader);
    }

    private string LocalizeDownloadResourceActionText(string actionText)
    {
        return actionText switch
        {
            "view_details" => T("resource_detail.actions.view_details"),
            _ => actionText
        };
    }

    private string LocalizeDownloadResourceHintText(string hintText)
    {
        if (string.IsNullOrWhiteSpace(hintText))
        {
            return string.Empty;
        }

        var lines = hintText
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                if (line.StartsWith("version_fallback|", StringComparison.Ordinal))
                {
                    var parts = line.Split('|', 3);
                    if (parts.Length == 3)
                    {
                        return T(
                            "download.resource.hints.version_fallback",
                            ("version", parts[1]),
                            ("surface_name", LocalizeDownloadResourceSurfaceName(parts[2])));
                    }
                }

                if (line.StartsWith("partial_results|", StringComparison.Ordinal))
                {
                    return T(
                        "download.resource.hints.partial_results",
                        ("message", line["partial_results|".Length..].Trim()));
                }

                if (line.StartsWith("unavailable|", StringComparison.Ordinal))
                {
                    return T(
                        "download.resource.hints.unavailable",
                        ("message", line["unavailable|".Length..].Trim()));
                }

                return line;
            })
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join(" ", lines);
    }

    private string LocalizeDownloadResourceSurfaceName(string surfaceId)
    {
        return surfaceId switch
        {
            "mod" => T("download.resource.search.mod"),
            "pack" => T("download.resource.search.pack"),
            "resource_pack" => T("download.resource.search.resource_pack"),
            "shader" => T("download.resource.search.shader"),
            "world" => T("download.resource.search.world"),
            _ => surfaceId
        };
    }

    private string FormatDownloadResourceVersionLabel(string version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? T("download.resource.entry.version_unknown")
            : version;
    }

    private string FormatDownloadResourceDownloadCountLabel(int downloadCount)
    {
        if (downloadCount <= 0)
        {
            return T("download.resource.entry.downloads_none");
        }

        return T(
            "download.resource.entry.downloads_with_count",
            ("count", FormatDownloadResourceCompactCount(downloadCount)));
    }

    private string FormatDownloadResourceUpdatedLabel(int updateRank, int releaseRank)
    {
        var rank = updateRank > 0 ? updateRank : releaseRank;
        if (rank <= 0)
        {
            return T("download.resource.entry.updated_unknown");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(rank).LocalDateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }
        catch
        {
            return T("download.resource.entry.updated_unknown");
        }
    }

    private string FormatDownloadResourceCompactCount(int value)
    {
        if (string.Equals(T("download.resource.entry.count_style"), "east_asian", StringComparison.OrdinalIgnoreCase))
        {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}\u4EBF",
            >= 10_000 => $"{value / 10_000d:0.#}\u4E07",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
        }

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000d:0.#}B",
            >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
            >= 1_000 => $"{value / 1_000d:0.#}K",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
    }

    private string LocalizeDownloadResourceTagGroup(string label)
    {
        return label switch
        {
            "style" => T("download.resource.tag_groups.style"),
            "features" => T("download.resource.tag_groups.features"),
            "resolution" => T("download.resource.tag_groups.resolution"),
            "performance" => T("download.resource.tag_groups.performance"),
            _ => label
        };
    }

    private string LocalizeDownloadResourceTag(string tag)
    {
        return tag switch
        {
            "all" => T("common.filters.all"),
            "CurseForge" => "CurseForge",
            "Modrinth" => "Modrinth",
            "Iris" => "Iris",
            "OptiFine" => "OptiFine",
            "FTB" => "FTB",
            "PBR" => "PBR",
            "mod" => T("download.resource.search.mod"),
            "pack" => T("download.resource.search.pack"),
            "resource_pack" => T("download.resource.search.resource_pack"),
            "shader" => T("download.resource.search.shader"),
            "world" => T("download.resource.search.world"),
            "worldgen" => T("download.resource.tags.worldgen"),
            "biomes" => T("download.resource.tags.biomes"),
            "dimensions" => T("download.resource.tags.dimensions"),
            "ores_resources" => T("download.resource.tags.ores_resources"),
            "structures" => T("download.resource.tags.structures"),
            "technology" => T("download.resource.tags.technology"),
            "logistics" => T("download.resource.tags.logistics"),
            "automation" => T("download.resource.tags.automation"),
            "energy" => T("download.resource.tags.energy"),
            "redstone" => T("download.resource.tags.redstone"),
            "food_cooking" => T("download.resource.tags.food_cooking"),
            "farming" => T("download.resource.tags.farming"),
            "game_mechanics" => T("download.resource.tags.game_mechanics"),
            "transportation" => T("download.resource.tags.transportation"),
            "storage" => T("download.resource.tags.storage"),
            "magic" => T("download.resource.tags.magic"),
            "adventure" => T("download.resource.tags.adventure"),
            "decoration" => T("download.resource.tags.decoration"),
            "mobs" => T("download.resource.tags.mobs"),
            "utility" => T("download.resource.tags.utility"),
            "equipment_tools" => T("download.resource.tags.equipment_tools"),
            "creative_mode" => T("download.resource.tags.creative_mode"),
            "performance" => T("download.resource.tags.performance"),
            "information" => T("download.resource.tags.information"),
            "multiplayer" => T("download.resource.tags.multiplayer"),
            "library" => T("download.resource.tags.library"),
            "hardcore" => T("download.resource.tags.hardcore"),
            "combat" => T("download.resource.tags.combat"),
            "quests" => T("download.resource.tags.quests"),
            "kitchen_sink" => T("download.resource.tags.kitchen_sink"),
            "exploration" => T("download.resource.tags.exploration"),
            "minigames" => T("download.resource.tags.minigames"),
            "scifi" => T("download.resource.tags.scifi"),
            "skyblock" => T("download.resource.tags.skyblock"),
            "vanilla_plus" => T("download.resource.tags.vanilla_plus"),
            "map_based" => T("download.resource.tags.map_based"),
            "lightweight" => T("download.resource.tags.lightweight"),
            "large" => T("download.resource.tags.large"),
            "fantasy" => T("download.resource.tags.fantasy"),
            "modded" => T("download.resource.tags.modded"),
            "vanilla_style" => T("download.resource.tags.vanilla_style"),
            "realistic" => T("download.resource.tags.realistic"),
            "modern" => T("download.resource.tags.modern"),
            "medieval" => T("download.resource.tags.medieval"),
            "steampunk" => T("download.resource.tags.steampunk"),
            "themed" => T("download.resource.tags.themed"),
            "simple" => T("download.resource.tags.simple"),
            "tweaks" => T("download.resource.tags.tweaks"),
            "chaotic" => T("download.resource.tags.chaotic"),
            "entities" => T("download.resource.tags.entities"),
            "audio" => T("download.resource.tags.audio"),
            "fonts" => T("download.resource.tags.fonts"),
            "models" => T("download.resource.tags.models"),
            "locale" => T("download.resource.tags.locale"),
            "gui" => T("download.resource.tags.gui"),
            "core_shaders" => T("download.resource.tags.core_shaders"),
            "dynamic_effects" => T("download.resource.tags.dynamic_effects"),
            "mod_compatible" => T("download.resource.tags.mod_compatible"),
            "resolution_8x" => T("download.resource.tags.resolution_8x"),
            "16x" => T("download.resource.tags.resolution_16x"),
            "32x" => T("download.resource.tags.resolution_32x"),
            "48x" => T("download.resource.tags.resolution_48x"),
            "64x" => T("download.resource.tags.resolution_64x"),
            "128x" => T("download.resource.tags.resolution_128x"),
            "256x" => T("download.resource.tags.resolution_256x"),
            "resolution_512x" => T("download.resource.tags.resolution_512x"),
            "fantasy_style" => T("download.resource.tags.fantasy_style"),
            "semi_realistic" => T("download.resource.tags.semi_realistic"),
            "cartoon" => T("download.resource.tags.cartoon"),
            "colored_lighting" => T("download.resource.tags.colored_lighting"),
            "path_tracing" => T("download.resource.tags.path_tracing"),
            "reflections" => T("download.resource.tags.reflections"),
            "performance_very_low" => T("download.resource.tags.performance_very_low"),
            "performance_low" => T("download.resource.tags.performance_low"),
            "performance_medium" => T("download.resource.tags.performance_medium"),
            "performance_high" => T("download.resource.tags.performance_high"),
            "creative" => T("download.resource.tags.creative"),
            "parkour" => T("download.resource.tags.parkour"),
            "puzzle" => T("download.resource.tags.puzzle"),
            "survival" => T("download.resource.tags.survival"),
            "mod_world" => T("download.resource.tags.mod_world"),
            "vanilla_compatible" => T("download.resource.tags.vanilla_compatible"),
            "data_pack" => T("download.resource.tags.data_pack"),
            _ => tag
        };
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildFallbackDownloadResourceTagOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadMod => BuildModTagOptions(),
            LauncherFrontendSubpageKey.DownloadPack => BuildPackTagOptions(),
            LauncherFrontendSubpageKey.DownloadDataPack => BuildDataPackTagOptions(),
            LauncherFrontendSubpageKey.DownloadResourcePack => BuildResourcePackTagOptions(),
            LauncherFrontendSubpageKey.DownloadShader => BuildShaderTagOptions(),
            LauncherFrontendSubpageKey.DownloadWorld => BuildWorldTagOptions(),
            _ => [CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty)]
        };
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildModTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("worldgen", "worldgen"),
            CreateDownloadResourceFilterOption("biomes", "biomes"),
            CreateDownloadResourceFilterOption("dimensions", "dimensions"),
            CreateDownloadResourceFilterOption("ores_resources", "ores_resources"),
            CreateDownloadResourceFilterOption("structures", "structures"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("logistics", "logistics"),
            CreateDownloadResourceFilterOption("automation", "automation"),
            CreateDownloadResourceFilterOption("energy", "energy"),
            CreateDownloadResourceFilterOption("redstone", "redstone"),
            CreateDownloadResourceFilterOption("food_cooking", "food_cooking"),
            CreateDownloadResourceFilterOption("farming", "farming"),
            CreateDownloadResourceFilterOption("game_mechanics", "game_mechanics"),
            CreateDownloadResourceFilterOption("transportation", "transportation"),
            CreateDownloadResourceFilterOption("storage", "storage"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("mobs", "mobs"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("equipment_tools", "equipment_tools"),
            CreateDownloadResourceFilterOption("creative_mode", "creative_mode"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("information", "information"),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("library", "library")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildPackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("hardcore", "hardcore"),
            CreateDownloadResourceFilterOption("combat", "combat"),
            CreateDownloadResourceFilterOption("quests", "quests"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("kitchen_sink", "kitchen_sink"),
            CreateDownloadResourceFilterOption("exploration", "exploration"),
            CreateDownloadResourceFilterOption("minigames", "minigames"),
            CreateDownloadResourceFilterOption("scifi", "scifi"),
            CreateDownloadResourceFilterOption("skyblock", "skyblock"),
            CreateDownloadResourceFilterOption("vanilla_plus", "vanilla_plus"),
            CreateDownloadResourceFilterOption("FTB", "FTB"),
            CreateDownloadResourceFilterOption("map_based", "map_based"),
            CreateDownloadResourceFilterOption("lightweight", "lightweight"),
            CreateDownloadResourceFilterOption("large", "large")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDataPackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("worldgen", "worldgen"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("game_mechanics", "game_mechanics"),
            CreateDownloadResourceFilterOption("transportation", "transportation"),
            CreateDownloadResourceFilterOption("storage", "storage"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("fantasy", "fantasy"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("mobs", "mobs"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("equipment_tools", "equipment_tools"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("library", "library"),
            CreateDownloadResourceFilterOption("modded", "modded")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildResourcePackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("style", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("vanilla_style", "vanilla_style"),
            CreateDownloadResourceFilterOption("realistic", "realistic"),
            CreateDownloadResourceFilterOption("modern", "modern"),
            CreateDownloadResourceFilterOption("medieval", "medieval"),
            CreateDownloadResourceFilterOption("steampunk", "steampunk"),
            CreateDownloadResourceFilterOption("themed", "themed"),
            CreateDownloadResourceFilterOption("simple", "simple"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("combat", "combat"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("tweaks", "tweaks"),
            CreateDownloadResourceFilterOption("chaotic", "chaotic"),
            CreateDownloadResourceFilterOption("features", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("entities", "entities"),
            CreateDownloadResourceFilterOption("audio", "audio"),
            CreateDownloadResourceFilterOption("fonts", "fonts"),
            CreateDownloadResourceFilterOption("models", "models"),
            CreateDownloadResourceFilterOption("locale", "locale"),
            CreateDownloadResourceFilterOption("gui", "gui"),
            CreateDownloadResourceFilterOption("core_shaders", "core_shaders"),
            CreateDownloadResourceFilterOption("dynamic_effects", "dynamic_effects"),
            CreateDownloadResourceFilterOption("mod_compatible", "mod_compatible"),
            CreateDownloadResourceFilterOption("resolution", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("resolution_8x", "resolution_8x"),
            CreateDownloadResourceFilterOption("16x", "16x"),
            CreateDownloadResourceFilterOption("32x", "32x"),
            CreateDownloadResourceFilterOption("48x", "48x"),
            CreateDownloadResourceFilterOption("64x", "64x"),
            CreateDownloadResourceFilterOption("128x", "128x"),
            CreateDownloadResourceFilterOption("256x", "256x"),
            CreateDownloadResourceFilterOption("resolution_512x", "resolution_512x")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildShaderTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("style", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("vanilla_style", "vanilla_style"),
            CreateDownloadResourceFilterOption("fantasy_style", "fantasy_style"),
            CreateDownloadResourceFilterOption("realistic", "realistic"),
            CreateDownloadResourceFilterOption("semi_realistic", "semi_realistic"),
            CreateDownloadResourceFilterOption("cartoon", "cartoon"),
            CreateDownloadResourceFilterOption("features", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("colored_lighting", "colored_lighting"),
            CreateDownloadResourceFilterOption("path_tracing", "path_tracing"),
            CreateDownloadResourceFilterOption("PBR", "PBR"),
            CreateDownloadResourceFilterOption("reflections", "reflections"),
            CreateDownloadResourceFilterOption("performance", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("performance_very_low", "performance_very_low"),
            CreateDownloadResourceFilterOption("performance_low", "performance_low"),
            CreateDownloadResourceFilterOption("performance_medium", "performance_medium"),
            CreateDownloadResourceFilterOption("performance_high", "performance_high")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildWorldTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("creative", "creative"),
            CreateDownloadResourceFilterOption("minigames", "minigames"),
            CreateDownloadResourceFilterOption("parkour", "parkour"),
            CreateDownloadResourceFilterOption("puzzle", "puzzle"),
            CreateDownloadResourceFilterOption("survival", "survival"),
            CreateDownloadResourceFilterOption("mod_world", "mod_world")
        ];
    }

    private DownloadResourceEntryViewModel CreateDownloadResourceEntry(
        string title,
        string info,
        string source,
        string version,
        string loader,
        IReadOnlyList<string> tags,
        string actionText,
        string? iconFileName,
        int downloadCount,
        int followCount,
        int releaseRank,
        int updateRank)
    {
        Bitmap? icon = null;
        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            icon = LoadLauncherBitmap("Images", "Blocks", iconFileName);
        }

        return new DownloadResourceEntryViewModel(
            icon,
            title,
            info,
            source,
            version,
            loader,
            tags,
            tags.Select(LocalizeDownloadResourceTag).ToArray(),
            [],
            [],
            downloadCount,
            followCount,
            releaseRank,
            updateRank,
            FormatDownloadResourceVersionLabel(version),
            FormatDownloadResourceDownloadCountLabel(downloadCount),
            FormatDownloadResourceUpdatedLabel(updateRank, releaseRank),
            LocalizeDownloadResourceActionText(actionText),
            new ActionCommand(() => AddActivity(
                T("download.resource.activities.entry_action", ("entry_title", title)),
                BuildActivityDetail(source, version, string.Join(" / ", tags)))),
            null);
    }

    private string ResolveDownloadLauncherFolder()
    {
        var provider = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
        var rawValue = FrontendLauncherPathService.DefaultLauncherFolderRaw;

        if (provider.Exists("LaunchFolderSelect"))
        {
            try
            {
                rawValue = provider.Get<string>("LaunchFolderSelect");
            }
            catch
            {
                rawValue = FrontendLauncherPathService.DefaultLauncherFolderRaw;
            }
        }

        return FrontendLauncherPathService.ResolveLauncherFolder(rawValue, _shellActionService.RuntimePaths);
    }

    private static string SanitizeInstallDirectoryName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "ImportedModPack" : cleaned;
    }

    private static string GetUniqueInstallDirectoryPath(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return directoryPath;
        }

        for (var suffix = 1; ; suffix++)
        {
            var candidate = $"{directoryPath}-{suffix}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

}

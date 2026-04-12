using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
    private string _downloadResourceEmptyStateText = "没有找到符合条件的资源条目。";
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
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceSortOptions =
    [
        new DownloadResourceFilterOptionViewModel("默认", string.Empty),
        new DownloadResourceFilterOptionViewModel("相关性", "relevance"),
        new DownloadResourceFilterOptionViewModel("下载量", "downloads"),
        new DownloadResourceFilterOptionViewModel("关注量", "follows"),
        new DownloadResourceFilterOptionViewModel("最新发布", "release"),
        new DownloadResourceFilterOptionViewModel("最近更新", "update")
    ];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceVersionOptions =
    [
        new DownloadResourceFilterOptionViewModel("任意", string.Empty),
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
    ];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceLoaderOptions =
    [
        new DownloadResourceFilterOptionViewModel("任意", string.Empty),
        new DownloadResourceFilterOptionViewModel("Forge", "Forge"),
        new DownloadResourceFilterOptionViewModel("NeoForge", "NeoForge"),
        new DownloadResourceFilterOptionViewModel("Fabric", "Fabric"),
        new DownloadResourceFilterOptionViewModel("Quilt", "Quilt")
    ];
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
        LauncherFrontendSubpageKey.DownloadMod => "搜索 Mod",
        LauncherFrontendSubpageKey.DownloadPack => "搜索整合包",
        LauncherFrontendSubpageKey.DownloadDataPack => "搜索数据包",
        LauncherFrontendSubpageKey.DownloadResourcePack => "搜索资源包",
        LauncherFrontendSubpageKey.DownloadShader => "搜索光影包",
        LauncherFrontendSubpageKey.DownloadWorld => "搜索存档",
        _ => "搜索资源"
    };

    public string DownloadResourceCurrentInstanceTitle => _instanceComposition.Selection.HasSelection
        ? _instanceComposition.Selection.InstanceName
        : "未选择实例";

    public string DownloadResourceCurrentInstanceSummary
    {
        get
        {
            if (!_instanceComposition.Selection.HasSelection)
            {
                return "当前下载页还没有选中实例，无法直接按实例兼容性筛选资源。";
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

            parts.Add("切换实例后，下载页会重新匹配兼容版本。");
            return string.Join(" • ", parts);
        }
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
            return $"Showing {shownCount} of {totalText} results";
        }
    }

    private void RefreshDownloadResourceSurface()
    {
        DownloadResourceSurfaceTitle = string.Empty;
        DownloadResourceLoadingText = string.Empty;
        DownloadResourceEmptyStateText = "没有找到符合条件的资源条目。";
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = false;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceTotalEntryCount = 0;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions = [];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceLoaderOptions =
        [
            new DownloadResourceFilterOptionViewModel("任意", string.Empty),
            new DownloadResourceFilterOptionViewModel("Forge", "Forge"),
            new DownloadResourceFilterOptionViewModel("NeoForge", "NeoForge"),
            new DownloadResourceFilterOptionViewModel("Fabric", "Fabric"),
            new DownloadResourceFilterOptionViewModel("Quilt", "Quilt")
        ];
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

        var (surfaceTitle, showInstallModPackAction, useShaderLoaderOptions) = GetDownloadResourceSurfaceDescriptor(_currentRoute.Subpage);
        DownloadResourceSurfaceTitle = $"{surfaceTitle} 列表";
        DownloadResourceLoadingText = $"正在获取{surfaceTitle}列表";
        DownloadResourceEmptyStateText = $"没有找到符合条件的{surfaceTitle}条目。";
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions =
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("CurseForge", "CurseForge"),
            new DownloadResourceFilterOptionViewModel("Modrinth", "Modrinth")
        ];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceVersionOptions = BuildDefaultDownloadResourceVersionOptions(
            ShouldAutoSyncDownloadResourceFiltersWithInstance()
                ? ResolveSelectedDownloadResourceVersionFilter()
                : null);
        _downloadResourceLoaderOptions = useShaderLoaderOptions
            ? BuildDefaultShaderLoaderOptions()
            : BuildDefaultResourceLoaderOptions();
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
        DownloadResourceSurfaceTitle = $"{surfaceTitle} 列表";
        DownloadResourceLoadingText = $"正在获取{surfaceTitle}列表";
        DownloadResourceEmptyStateText = $"没有找到符合条件的{surfaceTitle}条目。";
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = "无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，搜索结果可能不全。请稍后重试，或使用 VPN 以改善网络环境。";
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = supportsModrinth;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceSourceOptions = supportsModrinth
            ? [
                new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                new DownloadResourceFilterOptionViewModel("CurseForge", "CurseForge"),
                new DownloadResourceFilterOptionViewModel("Modrinth", "Modrinth")
            ]
            : [
                new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                new DownloadResourceFilterOptionViewModel("CurseForge", "CurseForge")
            ];
        _downloadResourceTagOptions = tagOptions;
        _downloadResourceLoaderOptions = useShaderLoader
            ? [
                new DownloadResourceFilterOptionViewModel("任意", string.Empty),
                new DownloadResourceFilterOptionViewModel("原版可用", "原版可用"),
                new DownloadResourceFilterOptionViewModel("Iris", "Iris"),
                new DownloadResourceFilterOptionViewModel("OptiFine", "OptiFine")
            ]
            : [
                new DownloadResourceFilterOptionViewModel("任意", string.Empty),
                new DownloadResourceFilterOptionViewModel("Forge", "Forge"),
                new DownloadResourceFilterOptionViewModel("NeoForge", "NeoForge"),
                new DownloadResourceFilterOptionViewModel("Fabric", "Fabric"),
                new DownloadResourceFilterOptionViewModel("Quilt", "Quilt")
            ];
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
        AddActivity("重置资源筛选", $"{DownloadResourceSurfaceTitle} 已恢复到默认筛选条件。");
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
            AddActivity("安装整合包", "当前页面没有可安装的整合包。");
            return;
        }

        var visibleEntry = DownloadResourceEntries.FirstOrDefault();
        if (visibleEntry is null)
        {
            AddActivity("安装整合包", "当前筛选结果中没有可安装的整合包。");
            return;
        }

        if (!_downloadResourceRuntimeStates.TryGetValue(_currentRoute.Subpage, out var resourceState))
        {
            AddActivity("安装整合包", "当前整合包列表尚未完成运行时组合。");
            return;
        }

        var sourceEntry = resourceState.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Title, visibleEntry.Title, StringComparison.Ordinal)
            && string.Equals(entry.Source, visibleEntry.Source, StringComparison.Ordinal));
        if (sourceEntry is null || string.IsNullOrWhiteSpace(sourceEntry.TargetPath) || !Directory.Exists(sourceEntry.TargetPath))
        {
            AddActivity("安装整合包", "当前置顶整合包缺少可复制的本地实例目录。");
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
                    $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                    $"来源整合包: {visibleEntry.Title}",
                    $"源目录: {sourceEntry.TargetPath}",
                    $"目标目录: {targetDirectory}",
                    $"当前下载安装名: {DownloadInstallName}"
                ]),
                new UTF8Encoding(false));

            ReloadDownloadComposition();
            InitializeDownloadInstallSurface();
            RefreshDownloadResourceSurface();
            RaisePropertyChanged(nameof(DownloadInstallName));
            OpenInstanceTarget("安装整合包", targetDirectory, "新安装的整合包目录不存在。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("安装整合包失败", ex.Message);
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

        DownloadResourceLoadingText = $"正在获取{DownloadResourceSurfaceTitle.Replace(" 列表", string.Empty)}列表";
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
                DownloadResourceHintText = $"实时社区搜索失败：{ex.Message}";
                ShowDownloadResourceHint = true;
                DownloadResourceEmptyStateHintText = "请稍后重试，或调整来源筛选。";
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
        DownloadResourceHintText = result.State.HintText;
        // Keep the loading card copy stable until it has fully transitioned out.
        DownloadResourceEmptyStateHintText = string.Empty;
        ShowDownloadResourceHint = !string.IsNullOrWhiteSpace(result.State.HintText);

        _downloadResourceSourceOptions = BuildDownloadResourceSourceOptions(result.State, selectedSource);
        _downloadResourceTagOptions = MergeFilterOptions(
            BuildFallbackDownloadResourceTagOptions(),
            result.State.TagOptions.Select(option => new DownloadResourceFilterOptionViewModel(option.Label, option.FilterValue)),
            selectedTag);
        _downloadResourceVersionOptions =
            MergeFilterOptions(
                [new DownloadResourceFilterOptionViewModel("任意", string.Empty)],
                result.VersionOptions.Select(version => new DownloadResourceFilterOptionViewModel(version, version)),
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

    private static (string SurfaceTitle, bool ShowInstallModPackAction, bool UseShaderLoaderOptions) GetDownloadResourceSurfaceDescriptor(
        LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => ("Mod", false, false),
            LauncherFrontendSubpageKey.DownloadPack => ("整合包", true, false),
            LauncherFrontendSubpageKey.DownloadDataPack => ("数据包", false, false),
            LauncherFrontendSubpageKey.DownloadResourcePack => ("资源包", false, false),
            LauncherFrontendSubpageKey.DownloadShader => ("光影包", false, true),
            LauncherFrontendSubpageKey.DownloadWorld => ("世界", false, false),
            _ => ("资源", false, false)
        };
    }

    private static IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultDownloadResourceVersionOptions(string? preferredVersion = null)
    {
        return MergeFilterOptions(
            [
            new DownloadResourceFilterOptionViewModel("任意", string.Empty),
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

    private static IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultResourceLoaderOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("任意", string.Empty),
            new DownloadResourceFilterOptionViewModel("Forge", "Forge"),
            new DownloadResourceFilterOptionViewModel("NeoForge", "NeoForge"),
            new DownloadResourceFilterOptionViewModel("Fabric", "Fabric"),
            new DownloadResourceFilterOptionViewModel("Quilt", "Quilt")
        ];
    }

    private static IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDefaultShaderLoaderOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("任意", string.Empty),
            new DownloadResourceFilterOptionViewModel("OptiFine", "OptiFine"),
            new DownloadResourceFilterOptionViewModel("Iris", "Iris")
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
                new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                new DownloadResourceFilterOptionViewModel("CurseForge", "CurseForge"),
                new DownloadResourceFilterOptionViewModel("Modrinth", "Modrinth")
            ]
            : [
                new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                new DownloadResourceFilterOptionViewModel(primarySource, primarySource)
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
                    entry.SupportedVersions,
                    entry.SupportedLoaders,
                    entry.DownloadCount,
                    entry.FollowCount,
                    entry.ReleaseRank,
                    entry.UpdateRank,
                    entry.ActionText,
                    string.IsNullOrWhiteSpace(entry.TargetPath)
                        ? new ActionCommand(() => AddActivity($"下载资源操作: {entry.Title}", $"{entry.Info} • {entry.Source}"))
                        : FrontendCommunityProjectService.TryParseCompDetailTarget(entry.TargetPath, out var projectId)
                            ? new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title, entry.Version, entry.Loader, _currentRoute.Subpage))
                            : CreateOpenTargetCommand($"打开资源页面: {entry.Title}", entry.TargetPath, entry.TargetPath),
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
            _ => [new DownloadResourceFilterOptionViewModel("全部", string.Empty)]
        };
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildModTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("世界元素", "世界元素"),
            new DownloadResourceFilterOptionViewModel("生物群系", "生物群系"),
            new DownloadResourceFilterOptionViewModel("维度", "维度"),
            new DownloadResourceFilterOptionViewModel("矿物与资源", "矿物与资源"),
            new DownloadResourceFilterOptionViewModel("天然结构", "天然结构"),
            new DownloadResourceFilterOptionViewModel("科技", "科技"),
            new DownloadResourceFilterOptionViewModel("管道与物流", "管道与物流"),
            new DownloadResourceFilterOptionViewModel("自动化", "自动化"),
            new DownloadResourceFilterOptionViewModel("能源", "能源"),
            new DownloadResourceFilterOptionViewModel("红石", "红石"),
            new DownloadResourceFilterOptionViewModel("食物与烹饪", "食物与烹饪"),
            new DownloadResourceFilterOptionViewModel("农业", "农业"),
            new DownloadResourceFilterOptionViewModel("游戏机制", "游戏机制"),
            new DownloadResourceFilterOptionViewModel("运输", "运输"),
            new DownloadResourceFilterOptionViewModel("仓储", "仓储"),
            new DownloadResourceFilterOptionViewModel("魔法", "魔法"),
            new DownloadResourceFilterOptionViewModel("冒险", "冒险"),
            new DownloadResourceFilterOptionViewModel("装饰", "装饰"),
            new DownloadResourceFilterOptionViewModel("生物", "生物"),
            new DownloadResourceFilterOptionViewModel("实用", "实用"),
            new DownloadResourceFilterOptionViewModel("装备与工具", "装备与工具"),
            new DownloadResourceFilterOptionViewModel("创造模式", "创造模式"),
            new DownloadResourceFilterOptionViewModel("性能优化", "性能优化"),
            new DownloadResourceFilterOptionViewModel("信息显示", "信息显示"),
            new DownloadResourceFilterOptionViewModel("服务器", "服务器"),
            new DownloadResourceFilterOptionViewModel("支持库", "支持库")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildPackTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("多人", "多人"),
            new DownloadResourceFilterOptionViewModel("性能优化", "性能优化"),
            new DownloadResourceFilterOptionViewModel("硬核", "硬核"),
            new DownloadResourceFilterOptionViewModel("战斗", "战斗"),
            new DownloadResourceFilterOptionViewModel("任务", "任务"),
            new DownloadResourceFilterOptionViewModel("科技", "科技"),
            new DownloadResourceFilterOptionViewModel("魔法", "魔法"),
            new DownloadResourceFilterOptionViewModel("冒险", "冒险"),
            new DownloadResourceFilterOptionViewModel("水槽包", "水槽包"),
            new DownloadResourceFilterOptionViewModel("探索", "探索"),
            new DownloadResourceFilterOptionViewModel("小游戏", "小游戏"),
            new DownloadResourceFilterOptionViewModel("科幻", "科幻"),
            new DownloadResourceFilterOptionViewModel("空岛", "空岛"),
            new DownloadResourceFilterOptionViewModel("原版改良", "原版改良"),
            new DownloadResourceFilterOptionViewModel("FTB", "FTB"),
            new DownloadResourceFilterOptionViewModel("基于地图", "基于地图"),
            new DownloadResourceFilterOptionViewModel("轻量整合", "轻量整合"),
            new DownloadResourceFilterOptionViewModel("大型整合", "大型整合")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDataPackTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("世界元素", "世界元素"),
            new DownloadResourceFilterOptionViewModel("科技", "科技"),
            new DownloadResourceFilterOptionViewModel("游戏机制", "游戏机制"),
            new DownloadResourceFilterOptionViewModel("运输", "运输"),
            new DownloadResourceFilterOptionViewModel("仓储", "仓储"),
            new DownloadResourceFilterOptionViewModel("魔法", "魔法"),
            new DownloadResourceFilterOptionViewModel("冒险", "冒险"),
            new DownloadResourceFilterOptionViewModel("幻想", "幻想"),
            new DownloadResourceFilterOptionViewModel("装饰", "装饰"),
            new DownloadResourceFilterOptionViewModel("生物", "生物"),
            new DownloadResourceFilterOptionViewModel("实用", "实用"),
            new DownloadResourceFilterOptionViewModel("装备与工具", "装备与工具"),
            new DownloadResourceFilterOptionViewModel("性能优化", "性能优化"),
            new DownloadResourceFilterOptionViewModel("服务器", "服务器"),
            new DownloadResourceFilterOptionViewModel("支持库", "支持库"),
            new DownloadResourceFilterOptionViewModel("Mod 相关", "Mod 相关")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildResourcePackTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("风格", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("原版风", "原版风"),
            new DownloadResourceFilterOptionViewModel("写实风", "写实风"),
            new DownloadResourceFilterOptionViewModel("现代风", "现代风"),
            new DownloadResourceFilterOptionViewModel("中世纪", "中世纪"),
            new DownloadResourceFilterOptionViewModel("蒸汽朋克", "蒸汽朋克"),
            new DownloadResourceFilterOptionViewModel("主题化", "主题化"),
            new DownloadResourceFilterOptionViewModel("简洁", "简洁"),
            new DownloadResourceFilterOptionViewModel("装饰", "装饰"),
            new DownloadResourceFilterOptionViewModel("战斗", "战斗"),
            new DownloadResourceFilterOptionViewModel("实用", "实用"),
            new DownloadResourceFilterOptionViewModel("改良", "改良"),
            new DownloadResourceFilterOptionViewModel("鬼畜", "鬼畜"),
            new DownloadResourceFilterOptionViewModel("特性", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("含实体", "含实体"),
            new DownloadResourceFilterOptionViewModel("含声音", "含声音"),
            new DownloadResourceFilterOptionViewModel("含字体", "含字体"),
            new DownloadResourceFilterOptionViewModel("含模型", "含模型"),
            new DownloadResourceFilterOptionViewModel("含语言", "含语言"),
            new DownloadResourceFilterOptionViewModel("含 UI", "含 UI"),
            new DownloadResourceFilterOptionViewModel("核心着色器", "核心着色器"),
            new DownloadResourceFilterOptionViewModel("动态效果", "动态效果"),
            new DownloadResourceFilterOptionViewModel("兼容 Mod", "兼容 Mod"),
            new DownloadResourceFilterOptionViewModel("分辨率", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("8x 或更低", "8x 或更低"),
            new DownloadResourceFilterOptionViewModel("16x", "16x"),
            new DownloadResourceFilterOptionViewModel("32x", "32x"),
            new DownloadResourceFilterOptionViewModel("48x", "48x"),
            new DownloadResourceFilterOptionViewModel("64x", "64x"),
            new DownloadResourceFilterOptionViewModel("128x", "128x"),
            new DownloadResourceFilterOptionViewModel("256x", "256x"),
            new DownloadResourceFilterOptionViewModel("512x 或更高", "512x 或更高")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildShaderTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("风格", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("原版风", "原版风"),
            new DownloadResourceFilterOptionViewModel("幻想风", "幻想风"),
            new DownloadResourceFilterOptionViewModel("写实风", "写实风"),
            new DownloadResourceFilterOptionViewModel("半写实风", "半写实风"),
            new DownloadResourceFilterOptionViewModel("卡通风", "卡通风"),
            new DownloadResourceFilterOptionViewModel("特性", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("彩色光照", "彩色光照"),
            new DownloadResourceFilterOptionViewModel("路径追踪", "路径追踪"),
            new DownloadResourceFilterOptionViewModel("PBR", "PBR"),
            new DownloadResourceFilterOptionViewModel("反射", "反射"),
            new DownloadResourceFilterOptionViewModel("性能负荷", string.Empty, isHeader: true),
            new DownloadResourceFilterOptionViewModel("极低", "极低"),
            new DownloadResourceFilterOptionViewModel("低", "低"),
            new DownloadResourceFilterOptionViewModel("中", "中"),
            new DownloadResourceFilterOptionViewModel("高", "高")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildWorldTagOptions()
    {
        return
        [
            new DownloadResourceFilterOptionViewModel("全部", string.Empty),
            new DownloadResourceFilterOptionViewModel("冒险", "冒险"),
            new DownloadResourceFilterOptionViewModel("创造", "创造"),
            new DownloadResourceFilterOptionViewModel("小游戏", "小游戏"),
            new DownloadResourceFilterOptionViewModel("跑酷", "跑酷"),
            new DownloadResourceFilterOptionViewModel("解谜", "解谜"),
            new DownloadResourceFilterOptionViewModel("生存", "生存"),
            new DownloadResourceFilterOptionViewModel("Mod 世界", "Mod 世界")
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
            [],
            [],
            downloadCount,
            followCount,
            releaseRank,
            updateRank,
            actionText,
            new ActionCommand(() => AddActivity($"下载资源操作: {title}", $"{source} • {version} • {string.Join(" / ", tags)}")),
            null);
    }

    private string ResolveDownloadLauncherFolder()
    {
        var provider = new YamlFileProvider(_shellActionService.RuntimePaths.LocalConfigPath);
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

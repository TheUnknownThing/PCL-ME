using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const int DownloadResourcePageSize = 3;
    private readonly ActionCommand _resetDownloadResourceFiltersCommand;
    private readonly ActionCommand _installDownloadResourceModPackCommand;
    private readonly ActionCommand _firstDownloadResourcePageCommand;
    private readonly ActionCommand _previousDownloadResourcePageCommand;
    private readonly ActionCommand _nextDownloadResourcePageCommand;
    private string _downloadResourceSearchQuery = string.Empty;
    private string _downloadResourceSurfaceTitle = string.Empty;
    private string _downloadResourceLoadingText = string.Empty;
    private string _downloadResourceEmptyStateText = "没有找到符合条件的资源条目。";
    private string _downloadResourceHintText = string.Empty;
    private bool _showDownloadResourceHint;
    private bool _showDownloadResourceInstallModPackAction;
    private int _selectedDownloadResourceSourceIndex;
    private int _selectedDownloadResourceTagIndex;
    private int _selectedDownloadResourceSortIndex;
    private int _selectedDownloadResourceVersionIndex;
    private int _selectedDownloadResourceLoaderIndex;
    private int _downloadResourcePageIndex;
    private int _downloadResourceTotalPages = 1;
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

    public string DownloadResourceSearchQuery
    {
        get => _downloadResourceSearchQuery;
        set
        {
            if (SetProperty(ref _downloadResourceSearchQuery, value) && IsDownloadResourceSurface)
            {
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public string DownloadResourceSurfaceTitle
    {
        get => _downloadResourceSurfaceTitle;
        private set => SetProperty(ref _downloadResourceSurfaceTitle, value);
    }

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

    public int SelectedDownloadResourceSourceIndex
    {
        get => _selectedDownloadResourceSourceIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceSourceOptions);
            if (SetProperty(ref _selectedDownloadResourceSourceIndex, nextValue) && IsDownloadResourceSurface)
            {
                UpdateDownloadResourceHint();
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public int SelectedDownloadResourceTagIndex
    {
        get => _selectedDownloadResourceTagIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceTagOptions);
            if (SetProperty(ref _selectedDownloadResourceTagIndex, nextValue) && IsDownloadResourceSurface)
            {
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public int SelectedDownloadResourceSortIndex
    {
        get => _selectedDownloadResourceSortIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceSortOptions);
            if (SetProperty(ref _selectedDownloadResourceSortIndex, nextValue) && IsDownloadResourceSurface)
            {
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public int SelectedDownloadResourceVersionIndex
    {
        get => _selectedDownloadResourceVersionIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceVersionOptions);
            if (SetProperty(ref _selectedDownloadResourceVersionIndex, nextValue) && IsDownloadResourceSurface)
            {
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public int SelectedDownloadResourceLoaderIndex
    {
        get => _selectedDownloadResourceLoaderIndex;
        set
        {
            var nextValue = ClampFilterIndex(value, DownloadResourceLoaderOptions);
            if (SetProperty(ref _selectedDownloadResourceLoaderIndex, nextValue) && IsDownloadResourceSurface)
            {
                ApplyDownloadResourceFilters(resetPage: true);
            }
        }
    }

    public bool HasDownloadResourceEntries => DownloadResourceEntries.Count > 0;

    public bool HasNoDownloadResourceEntries => !HasDownloadResourceEntries;

    public string DownloadResourcePageLabel => _downloadResourceTotalPages <= 0
        ? "0"
        : $"{_downloadResourcePageIndex + 1} / {_downloadResourceTotalPages}";

    public ActionCommand ResetDownloadResourceFiltersCommand => _resetDownloadResourceFiltersCommand;

    public ActionCommand InstallDownloadResourceModPackCommand => _installDownloadResourceModPackCommand;

    public ActionCommand FirstDownloadResourcePageCommand => _firstDownloadResourcePageCommand;

    public ActionCommand PreviousDownloadResourcePageCommand => _previousDownloadResourcePageCommand;

    public ActionCommand NextDownloadResourcePageCommand => _nextDownloadResourcePageCommand;

    private void RefreshDownloadResourceSurface()
    {
        DownloadResourceSurfaceTitle = string.Empty;
        DownloadResourceLoadingText = string.Empty;
        DownloadResourceEmptyStateText = "没有找到符合条件的资源条目。";
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = false;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions = [];
        _downloadResourceTagOptions = [];
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

        if (!IsDownloadResourceSurface)
        {
            RaisePropertyChanged(nameof(DownloadResourceSourceOptions));
            RaisePropertyChanged(nameof(DownloadResourceTagOptions));
            RaisePropertyChanged(nameof(DownloadResourceLoaderOptions));
            RaisePropertyChanged(nameof(HasDownloadResourceEntries));
            RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
            RaisePropertyChanged(nameof(DownloadResourcePageLabel));
            NotifyDownloadResourcePageCommandState();
            return;
        }

        if (_downloadComposition.ResourceStates.TryGetValue(_currentRoute.Subpage, out var runtimeState))
        {
            DownloadResourceSurfaceTitle = runtimeState.SurfaceTitle;
            DownloadResourceLoadingText = runtimeState.HintText;
            DownloadResourceEmptyStateText = $"没有找到符合条件的{runtimeState.SurfaceTitle.Replace(" 列表", string.Empty)}条目。";
            DownloadResourceHintText = runtimeState.HintText;
            ShowDownloadResourceInstallModPackAction = runtimeState.ShowInstallModPackAction;
            _downloadResourceSupportsModrinth = runtimeState.SupportsSecondarySource;
            _downloadResourceSourceOptions = runtimeState.SupportsSecondarySource
                ? [
                    new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                    new DownloadResourceFilterOptionViewModel("当前实例", "当前实例"),
                    new DownloadResourceFilterOptionViewModel("当前启动器", "当前启动器")
                ]
                : [
                    new DownloadResourceFilterOptionViewModel("全部", string.Empty),
                    new DownloadResourceFilterOptionViewModel("当前实例", "当前实例"),
                    new DownloadResourceFilterOptionViewModel("当前启动器", "当前启动器")
                ];
            _downloadResourceTagOptions = runtimeState.TagOptions
                .Select(option => new DownloadResourceFilterOptionViewModel(option.Label, option.FilterValue))
                .ToArray();
            _downloadResourceLoaderOptions = runtimeState.UseShaderLoaderOptions
                ? [
                    new DownloadResourceFilterOptionViewModel("任意加载器", string.Empty),
                    new DownloadResourceFilterOptionViewModel("OptiFine", "OptiFine"),
                    new DownloadResourceFilterOptionViewModel("Iris", "Iris")
                ]
                : [
                    new DownloadResourceFilterOptionViewModel("任意", string.Empty),
                    new DownloadResourceFilterOptionViewModel("Forge", "Forge"),
                    new DownloadResourceFilterOptionViewModel("NeoForge", "NeoForge"),
                    new DownloadResourceFilterOptionViewModel("Fabric", "Fabric"),
                    new DownloadResourceFilterOptionViewModel("Quilt", "Quilt")
                ];
            _allDownloadResourceEntries = runtimeState.Entries
                .Select(entry =>
                {
                    Bitmap? icon = null;
                    if (!string.IsNullOrWhiteSpace(entry.IconName))
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
                        entry.DownloadCount,
                        entry.FollowCount,
                        entry.ReleaseRank,
                        entry.UpdateRank,
                        entry.ActionText,
                        string.IsNullOrWhiteSpace(entry.TargetPath)
                            ? new ActionCommand(() => AddActivity($"下载资源操作: {entry.Title}", $"{entry.Info} • {entry.Source}"))
                            : new ActionCommand(() => OpenInstanceTarget($"查看资源: {entry.Title}", entry.TargetPath, "目标文件不存在。")));
                })
                .ToArray();
            ResetDownloadResourceFilterState();
            RaiseDownloadResourceFilterState();
            ApplyDownloadResourceFilters(resetPage: true);
            return;
        }

        RaiseDownloadResourceFilterState();
        ApplyDownloadResourceFilters(resetPage: true);
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
        DownloadResourceHintText = "无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，搜索结果可能不全。请稍后重试，或使用 VPN 以改善网络环境。";
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = supportsModrinth;
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
                new DownloadResourceFilterOptionViewModel("任意加载器", string.Empty),
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
        _selectedDownloadResourceVersionIndex = 0;
        _selectedDownloadResourceLoaderIndex = 0;
        _downloadResourcePageIndex = 0;
        _downloadResourceTotalPages = 1;
        UpdateDownloadResourceHint();
    }

    private void ResetDownloadResourceFilters()
    {
        ResetDownloadResourceFilterState();
        RaiseDownloadResourceFilterState();
        ApplyDownloadResourceFilters(resetPage: true);
        AddActivity("重置资源筛选", $"{DownloadResourceSurfaceTitle} 已恢复到默认筛选条件。");
    }

    private void InstallDownloadResourceModPack()
    {
        if (!IsDownloadResourceSurface || _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack)
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

        if (!_downloadComposition.ResourceStates.TryGetValue(_currentRoute.Subpage, out var resourceState))
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
            AddActivity("安装整合包失败", ex.Message);
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
        if (_downloadResourcePageIndex >= _downloadResourceTotalPages - 1)
        {
            return;
        }

        _downloadResourcePageIndex++;
        ApplyDownloadResourceFilters(resetPage: false);
    }

    private void ApplyDownloadResourceFilters(bool resetPage)
    {
        if (!IsDownloadResourceSurface)
        {
            return;
        }

        if (resetPage)
        {
            _downloadResourcePageIndex = 0;
        }

        IEnumerable<DownloadResourceEntryViewModel> entries = _allDownloadResourceEntries;

        if (!string.IsNullOrWhiteSpace(DownloadResourceSearchQuery))
        {
            entries = entries.Where(entry => entry.SearchText.Contains(DownloadResourceSearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        var sourceFilter = GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex);
        if (!string.IsNullOrWhiteSpace(sourceFilter))
        {
            entries = ShowDownloadResourceHint && string.Equals(sourceFilter, "Modrinth", StringComparison.OrdinalIgnoreCase)
                ? []
                : entries.Where(entry => string.Equals(entry.Source, sourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        var tagFilter = GetSelectedFilterValue(DownloadResourceTagOptions, SelectedDownloadResourceTagIndex);
        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            entries = entries.Where(entry => entry.Tags.Any(tag => string.Equals(tag, tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var versionFilter = GetSelectedFilterValue(DownloadResourceVersionOptions, SelectedDownloadResourceVersionIndex);
        if (!string.IsNullOrWhiteSpace(versionFilter))
        {
            entries = entries.Where(entry => string.Equals(entry.Version, versionFilter, StringComparison.OrdinalIgnoreCase));
        }

        var loaderFilter = GetSelectedFilterValue(DownloadResourceLoaderOptions, SelectedDownloadResourceLoaderIndex);
        if (!string.IsNullOrWhiteSpace(loaderFilter))
        {
            entries = entries.Where(entry => string.Equals(entry.Loader, loaderFilter, StringComparison.OrdinalIgnoreCase));
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
        _downloadResourceTotalPages = Math.Max(1, (int)Math.Ceiling(filteredEntries.Length / (double)DownloadResourcePageSize));
        _downloadResourcePageIndex = Math.Clamp(_downloadResourcePageIndex, 0, _downloadResourceTotalPages - 1);

        var pagedEntries = filteredEntries
            .Skip(_downloadResourcePageIndex * DownloadResourcePageSize)
            .Take(DownloadResourcePageSize)
            .ToArray();

        ReplaceItems(DownloadResourceEntries, pagedEntries);
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        RaisePropertyChanged(nameof(DownloadResourcePageLabel));
        NotifyDownloadResourcePageCommandState();
    }

    private void NotifyDownloadResourcePageCommandState()
    {
        _firstDownloadResourcePageCommand.NotifyCanExecuteChanged();
        _previousDownloadResourcePageCommand.NotifyCanExecuteChanged();
        _nextDownloadResourcePageCommand.NotifyCanExecuteChanged();
    }

    private void RaiseDownloadResourceFilterState()
    {
        RaisePropertyChanged(nameof(DownloadResourceSearchQuery));
        RaisePropertyChanged(nameof(DownloadResourceSourceOptions));
        RaisePropertyChanged(nameof(DownloadResourceTagOptions));
        RaisePropertyChanged(nameof(DownloadResourceSortOptions));
        RaisePropertyChanged(nameof(DownloadResourceVersionOptions));
        RaisePropertyChanged(nameof(DownloadResourceLoaderOptions));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSourceIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceTagIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceSortIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceVersionIndex));
        RaisePropertyChanged(nameof(SelectedDownloadResourceLoaderIndex));
        RaisePropertyChanged(nameof(ShowDownloadResourceInstallModPackAction));
        RaisePropertyChanged(nameof(DownloadResourcePageLabel));
    }

    private void UpdateDownloadResourceHint()
    {
        var selectedSource = GetSelectedFilterValue(DownloadResourceSourceOptions, SelectedDownloadResourceSourceIndex);
        ShowDownloadResourceHint = _downloadResourceSupportsModrinth
            && string.Equals(selectedSource, "Modrinth", StringComparison.OrdinalIgnoreCase);
    }

    private static int ClampFilterIndex(int value, IReadOnlyList<DownloadResourceFilterOptionViewModel> options)
    {
        return options.Count == 0 ? 0 : Math.Clamp(value, 0, options.Count - 1);
    }

    private static string GetSelectedFilterValue(IReadOnlyList<DownloadResourceFilterOptionViewModel> options, int index)
    {
        return index >= 0 && index < options.Count ? options[index].FilterValue : string.Empty;
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
            downloadCount,
            followCount,
            releaseRank,
            updateRank,
            actionText,
            new ActionCommand(() => AddActivity($"下载资源操作: {title}", $"{source} • {version} • {string.Join(" / ", tags)}")));
    }

    private string ResolveDownloadLauncherFolder()
    {
        var provider = new YamlFileProvider(_shellActionService.RuntimePaths.LocalConfigPath);
        var rawValue = "$.minecraft\\";

        if (provider.Exists("LaunchFolderSelect"))
        {
            try
            {
                rawValue = provider.Get<string>("LaunchFolderSelect");
            }
            catch
            {
                rawValue = "$.minecraft\\";
            }
        }

        var normalized = string.IsNullOrWhiteSpace(rawValue)
            ? "$.minecraft\\"
            : rawValue.Trim();
        normalized = normalized.Replace(
            "$",
            EnsureTrailingSeparator(_shellActionService.RuntimePaths.ExecutableDirectory),
            StringComparison.Ordinal);
        return Path.GetFullPath(normalized);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
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

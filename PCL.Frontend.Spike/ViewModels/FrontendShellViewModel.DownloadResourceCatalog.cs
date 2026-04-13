using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
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

        switch (_currentRoute.Subpage)
        {
            case LauncherFrontendSubpageKey.DownloadMod:
                ConfigureDownloadResourceSurface(
                    "Mod",
                    supportsModrinth: true,
                    showInstallModPackAction: false,
                    useShaderLoader: false,
                    BuildModTagOptions(),
                    [
                        CreateDownloadResourceEntry("Sodium", "现代性能优化模组，专注于渲染效率和稳定帧率。", "CurseForge", "1.21.1", "Fabric", ["性能优化", "实用"], "查看详情", "Fabric.png", 982, 224, 8, 10),
                        CreateDownloadResourceEntry("Create", "大型机械与物流科技模组，保留原版风味的自动化体验。", "Modrinth", "1.20.1", "Forge", ["科技", "自动化", "管道与物流"], "查看详情", "Anvil.png", 875, 212, 7, 9),
                        CreateDownloadResourceEntry("Biomes O' Plenty", "提供大量额外生物群系与地形生成内容。", "CurseForge", "1.20.1", "NeoForge", ["生物群系", "世界元素"], "查看详情", "Grass.png", 620, 168, 6, 7),
                        CreateDownloadResourceEntry("Waystones", "增加可共享的传送路标，适合多人联机与生存整合。", "CurseForge", "1.21.1", "Fabric", ["运输", "实用"], "查看详情", "CobbleStone.png", 515, 146, 5, 8),
                        CreateDownloadResourceEntry("Alex's Mobs", "加入生态更丰富的生物与冒险掉落内容。", "Modrinth", "1.20.6", "Forge", ["生物", "冒险"], "查看详情", "Egg.png", 430, 126, 4, 6)
                    ]);
                break;
            case LauncherFrontendSubpageKey.DownloadPack:
                ConfigureDownloadResourceSurface(
                    "整合包",
                    supportsModrinth: true,
                    showInstallModPackAction: true,
                    useShaderLoader: false,
                    BuildPackTagOptions(),
                    [
                        CreateDownloadResourceEntry("All The Mods 9", "大型现代整合包，适合长期生存与自动化路线。", "CurseForge", "1.20.1", "Forge", ["大型整合", "科技"], "查看详情", "CommandBlock.png", 910, 255, 9, 10),
                        CreateDownloadResourceEntry("Better MC", "偏探索与原版改良的综合整合包。", "Modrinth", "1.20.1", "Fabric", ["探索", "原版改良"], "查看详情", "Grass.png", 804, 233, 8, 9),
                        CreateDownloadResourceEntry("FTB Skies Expert", "空岛主题的任务型整合包，强调推进路线。", "CurseForge", "1.19.2", "Forge", ["空岛", "任务", "FTB"], "查看详情", "GoldBlock.png", 688, 194, 7, 8),
                        CreateDownloadResourceEntry("RLCraft", "硬核生存与冒险路线的经典整合包。", "CurseForge", "1.12.2", "Forge", ["硬核", "冒险"], "查看详情", "RedstoneBlock.png", 661, 188, 6, 7),
                        CreateDownloadResourceEntry("Fabulously Optimized", "轻量化性能整合包，面向新版 Fabric 环境。", "Modrinth", "1.21.1", "Fabric", ["轻量整合", "性能优化"], "查看详情", "Fabric.png", 580, 171, 5, 6)
                    ]);
                break;
            case LauncherFrontendSubpageKey.DownloadDataPack:
                ConfigureDownloadResourceSurface(
                    "数据包",
                    supportsModrinth: true,
                    showInstallModPackAction: false,
                    useShaderLoader: false,
                    BuildDataPackTagOptions(),
                    [
                        CreateDownloadResourceEntry("Incendium", "重做下界结构与挑战内容的大型冒险数据包。", "Modrinth", "1.21.1", string.Empty, ["冒险", "世界元素"], "查看详情", "RedstoneLampOn.png", 574, 180, 8, 10),
                        CreateDownloadResourceEntry("BlazeandCave's Advancements Pack", "为原版加入大量进度与挑战路线。", "CurseForge", "1.20.6", string.Empty, ["游戏机制", "冒险"], "查看详情", "GoldBlock.png", 441, 143, 7, 9),
                        CreateDownloadResourceEntry("Mob Captains", "强化生物遭遇与掉落循环的玩法增强数据包。", "Modrinth", "1.20.1", string.Empty, ["生物", "装备与工具"], "查看详情", "Egg.png", 396, 112, 6, 8),
                        CreateDownloadResourceEntry("Terralith Compatibility Bundle", "为地形与结构包提供联动支持。", "CurseForge", "1.21.1", string.Empty, ["支持库", "世界元素"], "查看详情", "Grass.png", 321, 101, 5, 7)
                    ]);
                break;
            case LauncherFrontendSubpageKey.DownloadResourcePack:
                ConfigureDownloadResourceSurface(
                    "资源包",
                    supportsModrinth: true,
                    showInstallModPackAction: false,
                    useShaderLoader: false,
                    BuildResourcePackTagOptions(),
                    [
                        CreateDownloadResourceEntry("Faithful 32x", "经典写实增强资源包，保持原版辨识度。", "CurseForge", "1.21.1", "Fabric", ["原版风", "32x"], "查看详情", "Grass.png", 923, 281, 9, 10),
                        CreateDownloadResourceEntry("ModernArch", "现代写实风资源包，适合建筑展示。", "Modrinth", "1.20.1", "OptiFine", ["现代风", "128x"], "查看详情", "GoldBlock.png", 612, 202, 8, 9),
                        CreateDownloadResourceEntry("Vanilla Tweaks", "轻量化原版改良合集，支持按模块组合下载。", "CurseForge", "1.21.1", string.Empty, ["原版风", "改良"], "查看详情", "CobbleStone.png", 570, 178, 7, 8),
                        CreateDownloadResourceEntry("Fresh Animations", "为原版生物提供更细致的动作表现。", "Modrinth", "1.21.1", "Fabric", ["含实体", "改良"], "查看详情", "Egg.png", 488, 165, 6, 7)
                    ]);
                break;
            case LauncherFrontendSubpageKey.DownloadShader:
                ConfigureDownloadResourceSurface(
                    "光影包",
                    supportsModrinth: true,
                    showInstallModPackAction: false,
                    useShaderLoader: true,
                    BuildShaderTagOptions(),
                    [
                        CreateDownloadResourceEntry("Complementary Reimagined", "平衡性能与效果的热门综合光影。", "CurseForge", "1.21.1", "Iris", ["原版风", "中"], "查看详情", "RedstoneLampOn.png", 1012, 296, 9, 10),
                        CreateDownloadResourceEntry("BSL Shaders", "偏暖色氛围的经典光影方案。", "CurseForge", "1.20.1", "OptiFine", ["幻想风", "中"], "查看详情", "RedstoneBlock.png", 874, 241, 8, 9),
                        CreateDownloadResourceEntry("Sildur's Vibrant Shaders", "兼顾低配与高画质档位的常见选择。", "Modrinth", "1.20.6", "OptiFine", ["彩色光照", "低"], "查看详情", "RedstoneLampOff.png", 761, 214, 7, 8),
                        CreateDownloadResourceEntry("Photon Shader", "注重清晰体积光和反射表现的现代光影。", "Modrinth", "1.21.1", "Iris", ["反射", "高"], "查看详情", "CommandBlock.png", 504, 149, 6, 7)
                    ]);
                break;
            case LauncherFrontendSubpageKey.DownloadWorld:
                ConfigureDownloadResourceSurface(
                    "世界",
                    supportsModrinth: false,
                    showInstallModPackAction: false,
                    useShaderLoader: false,
                    BuildWorldTagOptions(),
                    [
                        CreateDownloadResourceEntry("Terra Swoop Force", "高完成度多人竞速地图。", "CurseForge", "1.21.1", string.Empty, ["小游戏", "跑酷"], "查看详情", "GrassPath.png", 608, 190, 8, 10),
                        CreateDownloadResourceEntry("Diversity 3", "包含解谜、跑酷、战斗等多章节挑战。", "CurseForge", "1.20.1", string.Empty, ["解谜", "冒险"], "查看详情", "CommandBlock.png", 552, 177, 7, 9),
                        CreateDownloadResourceEntry("OneBlock Survival", "经典的渐进式空岛生存地图。", "CurseForge", "1.21.1", string.Empty, ["生存", "创造"], "查看详情", "CobbleStone.png", 481, 151, 6, 8),
                        CreateDownloadResourceEntry("Modded Sky Realm", "面向整合包环境的世界预设。", "CurseForge", "1.12.2", "Forge", ["Mod 世界"], "查看详情", "Egg.png", 302, 96, 5, 6)
                    ]);
                break;
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
        AddActivity("安装整合包", "Would install the selected modpack into the current Minecraft folder.");
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
}

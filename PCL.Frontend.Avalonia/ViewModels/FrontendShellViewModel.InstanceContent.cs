using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private enum InstanceResourceFilter
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        Duplicate = 3
    }

    private enum InstanceWorldSortMethod
    {
        FileName = 0,
        CreateTime = 1,
        ModifyTime = 2
    }

    private enum InstanceResourceSortMethod
    {
        FileName = 0,
        ResourceName = 1,
        CreateTime = 2,
        FileSize = 3
    }

    private string _instanceWorldSearchQuery = string.Empty;
    private string _instanceServerSearchQuery = string.Empty;
    private string _instanceResourceSearchQuery = string.Empty;
    private string _instanceResourceSurfaceTitle = "Mod";
    private InstanceResourceFilter _instanceResourceFilter = InstanceResourceFilter.All;
    private int _instanceResourceTotalCount;
    private int _instanceResourceEnabledCount;
    private int _instanceResourceDisabledCount;
    private int _instanceResourceDuplicateCount;
    private bool _instanceResourceIsSearching;
    private InstanceWorldSortMethod _instanceWorldSortMethod = InstanceWorldSortMethod.FileName;
    private InstanceResourceSortMethod _instanceResourceSortMethod = InstanceResourceSortMethod.ResourceName;
    private readonly HashSet<string> _instanceResourceSelectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressInstanceResourceSelectionChanged;

    public string InstanceWorldSearchQuery
    {
        get => _instanceWorldSearchQuery;
        set
        {
            if (SetProperty(ref _instanceWorldSearchQuery, value))
            {
                RefreshInstanceWorldEntries();
            }
        }
    }

    public string InstanceServerSearchQuery
    {
        get => _instanceServerSearchQuery;
        set
        {
            if (SetProperty(ref _instanceServerSearchQuery, value))
            {
                RefreshInstanceServerEntries();
            }
        }
    }

    public string InstanceResourceSearchQuery
    {
        get => _instanceResourceSearchQuery;
        set
        {
            if (SetProperty(ref _instanceResourceSearchQuery, value))
            {
                RefreshInstanceResourceEntries();
            }
        }
    }

    public string InstanceResourceSurfaceTitle
    {
        get => _instanceResourceSurfaceTitle;
        private set => SetProperty(ref _instanceResourceSurfaceTitle, value);
    }

    public bool HasInstanceWorldEntries => InstanceWorldEntries.Count > 0;

    public bool HasNoInstanceWorldEntries => !HasInstanceWorldEntries;

    public bool ShowInstanceWorldContent => _instanceComposition.World.Entries.Count > 0;

    public bool ShowInstanceWorldEmptyState => !ShowInstanceWorldContent;

    public string InstanceWorldSortText => $"排序：{GetInstanceWorldSortName(_instanceWorldSortMethod)}";

    public bool HasInstanceScreenshotEntries => InstanceScreenshotEntries.Count > 0;

    public bool HasNoInstanceScreenshotEntries => !HasInstanceScreenshotEntries;

    public bool ShowInstanceScreenshotContent => _instanceComposition.Screenshot.Entries.Count > 0;

    public bool ShowInstanceScreenshotEmptyState => !ShowInstanceScreenshotContent;

    public bool HasInstanceServerEntries => InstanceServerEntries.Count > 0;

    public bool HasNoInstanceServerEntries => !HasInstanceServerEntries;

    public bool ShowInstanceServerContent => _instanceComposition.Server.Entries.Count > 0;

    public bool ShowInstanceServerEmptyState => !ShowInstanceServerContent;

    public bool HasInstanceResourceEntries => InstanceResourceEntries.Count > 0;

    public bool HasNoInstanceResourceEntries => !HasInstanceResourceEntries;

    public bool ShowInstanceResourceUnsupportedState => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod => !_instanceComposition.Selection.IsModable,
        LauncherFrontendSubpageKey.VersionSchematic => GetCurrentInstanceResourceSourceEntries().Count == 0,
        _ => false
    };

    public bool ShowInstanceResourceContent => !ShowInstanceResourceUnsupportedState
        && GetCurrentInstanceResourceSourceEntries().Count > 0;

    public bool ShowInstanceResourceEmptyState => ShowInstanceResourceUnsupportedState
        || GetCurrentInstanceResourceSourceEntries().Count == 0;

    public bool ShowInstanceResourceFilterBar => _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
        && _instanceComposition.Selection.IsModable;

    public string InstanceResourceFilterAllText => $"{(_instanceResourceIsSearching ? "搜索结果" : "全部")} ({_instanceResourceTotalCount})";

    public string InstanceResourceSortText => $"排序：{GetInstanceResourceSortName(_instanceResourceSortMethod)}";

    public bool IsInstanceResourceFilterAllSelected => _instanceResourceFilter == InstanceResourceFilter.All;

    public string InstanceResourceFilterEnabledText => $"启用 ({_instanceResourceEnabledCount})";

    public bool ShowInstanceResourceEnabledFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Enabled
            || (_instanceResourceEnabledCount > 0 && _instanceResourceEnabledCount < _instanceResourceTotalCount));

    public bool IsInstanceResourceFilterEnabledSelected => _instanceResourceFilter == InstanceResourceFilter.Enabled;

    public string InstanceResourceFilterDisabledText => $"禁用 ({_instanceResourceDisabledCount})";

    public bool ShowInstanceResourceDisabledFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Disabled || _instanceResourceDisabledCount > 0);

    public bool IsInstanceResourceFilterDisabledSelected => _instanceResourceFilter == InstanceResourceFilter.Disabled;

    public string InstanceResourceFilterDuplicateText => $"重复 ({_instanceResourceDuplicateCount})";

    public bool ShowInstanceResourceDuplicateFilter => ShowInstanceResourceFilterBar
        && (_instanceResourceFilter == InstanceResourceFilter.Duplicate || _instanceResourceDuplicateCount > 0);

    public bool IsInstanceResourceFilterDuplicateSelected => _instanceResourceFilter == InstanceResourceFilter.Duplicate;

    public bool ShowInstanceResourceEmptyInstallActions => !ShowInstanceResourceUnsupportedState;

    public bool ShowInstanceResourceInstanceSelectAction => ShowInstanceResourceUnsupportedState;

    public string InstanceResourceSearchWatermark => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => "搜索资源 名称 / 描述 / 标签",
        LauncherFrontendSubpageKey.VersionShader => "搜索光影 名称 / 描述 / 标签",
        LauncherFrontendSubpageKey.VersionSchematic => "搜索投影 名称 / 描述 / 标签",
        _ => "搜索资源 名称 / 描述 / 标签"
    };

    public string InstanceResourceDownloadButtonText => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionResourcePack => "下载新资源",
        LauncherFrontendSubpageKey.VersionShader => "下载新资源",
        LauncherFrontendSubpageKey.VersionSchematic => "下载投影 Mod",
        _ => "下载新资源"
    };

    public string InstanceResourceEmptyTitle => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable => "该实例不可使用 Mod",
        LauncherFrontendSubpageKey.VersionMod => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionResourcePack => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionShader => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionSchematic => "该实例不可用投影原理图",
        _ => "尚未安装资源"
    };

    public string InstanceResourceEmptyDescription => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable =>
            "你需要先安装 Forge、Fabric 等 Mod 加载器才能使用 Mod，请在下载页面安装这些实例。如果你已经安装过了 Mod 加载器，那么你很可能选择了错误的实例，请点击实例选择按钮切换实例。",
        LauncherFrontendSubpageKey.VersionSchematic => "你可能需要先安装投影 Mod，如果已经安装过了投影 Mod 请先启动一次游戏。也可能是你选择错了实例，请点击实例选择按钮切换实例。",
        _ => "你可以下载新的资源，也可以从已经下载好的文件安装资源。如果你已经安装了资源，可能是版本隔离设置有误，请在设置中调整版本隔离选项。"
    };

    public string InstanceResourceEmptyDownloadButtonText => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionMod when !_instanceComposition.Selection.IsModable => "转到下载页面",
        LauncherFrontendSubpageKey.VersionSchematic => "下载投影 Mod",
        _ => InstanceResourceDownloadButtonText
    };

    public bool ShowInstanceResourceCheckButton => _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
        && _instanceComposition.Selection.IsModable;

    public int InstanceResourceSelectedCount => _instanceResourceSelectedPaths.Count;

    public bool HasSelectedInstanceResources => InstanceResourceSelectedCount > 0;

    public string InstanceResourceSelectionText => $"已选择 {InstanceResourceSelectedCount} 项";

    public bool ShowInstanceResourceDefaultActions => !HasSelectedInstanceResources;

    public bool ShowInstanceResourceBatchActions => HasSelectedInstanceResources;

    public bool ShowInstanceResourceToggleActions => IsInstanceResourceToggleSupported();

    public bool CanSelectAllInstanceResources => InstanceResourceEntries.Count > 0
        && InstanceResourceSelectedCount < InstanceResourceEntries.Count;

    public bool CanEnableSelectedInstanceResources => ShowInstanceResourceToggleActions
        && InstanceResourceEntries.Any(entry => entry.IsSelected && !entry.IsEnabledState);

    public bool CanDisableSelectedInstanceResources => ShowInstanceResourceToggleActions
        && InstanceResourceEntries.Any(entry => entry.IsSelected && entry.IsEnabledState);

    public bool CanDeleteSelectedInstanceResources => HasSelectedInstanceResources;

    public ActionCommand OpenInstanceWorldFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            "打开存档文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            "当前实例没有存档目录。"));

    public ActionCommand PasteInstanceWorldClipboardCommand => new(() => _ = PasteInstanceWorldClipboardAsync());

    public ActionCommand OpenInstanceScreenshotFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            "打开截图文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "screenshots") : string.Empty,
            "当前实例没有截图目录。"));

    public ActionCommand RefreshInstanceServerCommand => new(() => _ = RefreshAllInstanceServersAsync());

    public ActionCommand AddInstanceServerCommand => new(() => _ = AddInstanceServerAsync());

    public ActionCommand OpenInstanceResourceFolderCommand => new(() =>
        OpenInstanceDirectoryTarget("打开资源文件夹", GetCurrentInstanceResourceDirectory(), "当前实例没有对应的资源目录。"));

    public ActionCommand InstallInstanceResourceFromFileCommand => new(() => _ = InstallInstanceResourceFromFileAsync());

    public ActionCommand DownloadInstanceResourceCommand => new(DownloadInstanceResource);

    public ActionCommand SelectAllInstanceResourcesCommand => new(SelectAllInstanceResources);

    public ActionCommand ClearInstanceResourceSelectionCommand => new(ClearInstanceResourceSelection);

    public ActionCommand EnableSelectedInstanceResourcesCommand => new(() => _ = SetSelectedInstanceResourcesEnabledAsync(true));

    public ActionCommand DisableSelectedInstanceResourcesCommand => new(() => _ = SetSelectedInstanceResourcesEnabledAsync(false));

    public ActionCommand DeleteSelectedInstanceResourcesCommand => new(() => _ = DeleteSelectedInstanceResourcesAsync());

    public ActionCommand ExportInstanceResourceInfoCommand => new(ExportInstanceResourceInfo);

    public ActionCommand CheckInstanceModsCommand => new(CheckInstanceMods);

    public ActionCommand SetInstanceResourceAllFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.All));

    public ActionCommand SetInstanceResourceEnabledFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Enabled));

    public ActionCommand SetInstanceResourceDisabledFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Disabled));

    public ActionCommand SetInstanceResourceDuplicateFilterCommand => new(() => SetInstanceResourceFilter(InstanceResourceFilter.Duplicate));

    private void InitializeInstanceContentSurfaces()
    {
        _instanceWorldSearchQuery = string.Empty;
        _instanceServerSearchQuery = string.Empty;
        _instanceResourceSearchQuery = string.Empty;
        _instanceResourceSelectedPaths.Clear();
        _instanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        _instanceResourceFilter = InstanceResourceFilter.All;
        _instanceResourceSortMethod = InstanceResourceSortMethod.ResourceName;

        RefreshInstanceWorldEntries();
        RefreshInstanceScreenshotEntries();
        RefreshInstanceServerEntries();
        RefreshInstanceResourceEntries();
    }

    private void RefreshInstanceContentSurfaces()
    {
        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceWorld))
        {
            RaisePropertyChanged(nameof(InstanceWorldSearchQuery));
            RaisePropertyChanged(nameof(InstanceWorldSortText));
            RaisePropertyChanged(nameof(HasInstanceWorldEntries));
            RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
            RaisePropertyChanged(nameof(ShowInstanceWorldContent));
            RaisePropertyChanged(nameof(ShowInstanceWorldEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceScreenshot))
        {
            RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotContent));
            RaisePropertyChanged(nameof(ShowInstanceScreenshotEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceServer))
        {
            RaisePropertyChanged(nameof(InstanceServerSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceServerEntries));
            RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
            RaisePropertyChanged(nameof(ShowInstanceServerContent));
            RaisePropertyChanged(nameof(ShowInstanceServerEmptyState));
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceResource))
        {
            RefreshInstanceResourceEntries();
            RaisePropertyChanged(nameof(InstanceResourceSearchQuery));
            RaisePropertyChanged(nameof(InstanceResourceSurfaceTitle));
            RaisePropertyChanged(nameof(InstanceResourceSearchWatermark));
            RaisePropertyChanged(nameof(InstanceResourceDownloadButtonText));
            RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
            RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
            RaisePropertyChanged(nameof(HasInstanceResourceEntries));
            RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
            RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
            RaisePropertyChanged(nameof(ShowInstanceResourceContent));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
            RaisePropertyChanged(nameof(ShowInstanceResourceFilterBar));
            RaisePropertyChanged(nameof(InstanceResourceFilterAllText));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterAllSelected));
            RaisePropertyChanged(nameof(InstanceResourceSortText));
            RaisePropertyChanged(nameof(InstanceResourceFilterEnabledText));
            RaisePropertyChanged(nameof(ShowInstanceResourceEnabledFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterEnabledSelected));
            RaisePropertyChanged(nameof(InstanceResourceFilterDisabledText));
            RaisePropertyChanged(nameof(ShowInstanceResourceDisabledFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterDisabledSelected));
            RaisePropertyChanged(nameof(InstanceResourceFilterDuplicateText));
            RaisePropertyChanged(nameof(ShowInstanceResourceDuplicateFilter));
            RaisePropertyChanged(nameof(IsInstanceResourceFilterDuplicateSelected));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
            RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
            RaiseInstanceResourceSelectionProperties();
        }
    }

    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, string path, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            new ActionCommand(() => OpenInstanceTarget("打开截图", path, "当前截图文件不存在。")));
    }

    private void RefreshInstanceResourceEntries()
    {
        InstanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        var searchedEntries = GetCurrentInstanceResourceSourceEntries()
            .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Meta, InstanceResourceSearchQuery))
            .ToArray();
        var duplicateTitles = searchedEntries
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _instanceResourceIsSearching = !string.IsNullOrWhiteSpace(InstanceResourceSearchQuery);
        _instanceResourceTotalCount = searchedEntries.Length;
        _instanceResourceEnabledCount = searchedEntries.Count(entry => entry.IsEnabled);
        _instanceResourceDisabledCount = searchedEntries.Count(entry => !entry.IsEnabled);
        _instanceResourceDuplicateCount = searchedEntries.Count(entry => duplicateTitles.Contains(entry.Title));

        var visibleEntries = ApplyInstanceResourceSort(ApplyInstanceResourceFilter(searchedEntries, duplicateTitles))
            .ToArray();
        _instanceResourceSelectedPaths.IntersectWith(visibleEntries.Select(entry => entry.Path));

        ReplaceItems(
            InstanceResourceEntries,
            visibleEntries.Select(CreateInstanceResourceEntry));

        RaisePropertyChanged(nameof(HasInstanceResourceEntries));
        RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
        RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
        RaisePropertyChanged(nameof(ShowInstanceResourceContent));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
        RaisePropertyChanged(nameof(ShowInstanceResourceFilterBar));
        RaisePropertyChanged(nameof(InstanceResourceFilterAllText));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterAllSelected));
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RaisePropertyChanged(nameof(InstanceResourceFilterEnabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceEnabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterEnabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDisabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDisabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDisabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDuplicateText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDuplicateFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDuplicateSelected));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
        RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
        RaiseInstanceResourceSelectionProperties();
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceFilter(
        IReadOnlyList<FrontendInstanceResourceEntry> entries,
        ISet<string> duplicateTitles)
    {
        if (!ShowInstanceResourceFilterBar)
        {
            return entries;
        }

        return _instanceResourceFilter switch
        {
            InstanceResourceFilter.Enabled => entries.Where(entry => entry.IsEnabled),
            InstanceResourceFilter.Disabled => entries.Where(entry => !entry.IsEnabled),
            InstanceResourceFilter.Duplicate => entries.Where(entry => duplicateTitles.Contains(entry.Title)),
            _ => entries
        };
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceSort(IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        return _instanceResourceSortMethod switch
        {
            InstanceResourceSortMethod.FileName => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.CreateTime => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => GetPathCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.FileSize => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => IsDirectoryPath(entry.Path) ? 0L : GetFileSize(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private IReadOnlyList<FrontendInstanceResourceEntry> GetCurrentInstanceResourceSourceEntries()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => _instanceComposition.Mods.Entries.Concat(_instanceComposition.DisabledMods.Entries).ToArray(),
            LauncherFrontendSubpageKey.VersionModDisabled => _instanceComposition.DisabledMods.Entries,
            LauncherFrontendSubpageKey.VersionResourcePack => _instanceComposition.ResourcePacks.Entries,
            LauncherFrontendSubpageKey.VersionShader => _instanceComposition.Shaders.Entries,
            LauncherFrontendSubpageKey.VersionSchematic => _instanceComposition.Schematics.Entries,
            _ => _instanceComposition.Mods.Entries
        };
    }

    private void SetInstanceResourceFilter(InstanceResourceFilter filter)
    {
        if (_instanceResourceFilter == filter)
        {
            return;
        }

        _instanceResourceFilter = filter;
        RefreshInstanceResourceEntries();
    }

    internal void SetInstanceResourceFileNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileName);

    internal void SetInstanceResourceNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.ResourceName);

    internal void SetInstanceResourceCreateTimeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.CreateTime);

    internal void SetInstanceResourceFileSizeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileSize);

    private void SetInstanceResourceSortMethod(InstanceResourceSortMethod target)
    {
        if (_instanceResourceSortMethod == target)
        {
            return;
        }

        _instanceResourceSortMethod = target;
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RefreshInstanceResourceEntries();
    }

    private void RaiseInstanceResourceSelectionProperties()
    {
        RaisePropertyChanged(nameof(InstanceResourceSelectedCount));
        RaisePropertyChanged(nameof(HasSelectedInstanceResources));
        RaisePropertyChanged(nameof(InstanceResourceSelectionText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDefaultActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceBatchActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceToggleActions));
        RaisePropertyChanged(nameof(CanSelectAllInstanceResources));
        RaisePropertyChanged(nameof(CanEnableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDisableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDeleteSelectedInstanceResources));
    }

    private bool IsInstanceResourceToggleSupported()
    {
        return _instanceComposition.Selection.IsModable
            && (_currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
                || _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionModDisabled);
    }

    private IReadOnlyList<InstanceResourceEntryViewModel> GetSelectedInstanceResourceEntries()
    {
        return InstanceResourceEntries
            .Where(entry => entry.IsSelected)
            .ToArray();
    }

    private void HandleInstanceResourceSelectionChanged(string path, bool isSelected)
    {
        if (_suppressInstanceResourceSelectionChanged)
        {
            return;
        }

        if (isSelected)
        {
            _instanceResourceSelectedPaths.Add(path);
        }
        else
        {
            _instanceResourceSelectedPaths.Remove(path);
        }

        RaiseInstanceResourceSelectionProperties();
    }

    private void SelectAllInstanceResources()
    {
        if (InstanceResourceEntries.Count == 0)
        {
            AddActivity("全选资源", $"{InstanceResourceSurfaceTitle} 列表为空。");
            return;
        }

        if (!CanSelectAllInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = true;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        foreach (var entry in InstanceResourceEntries)
        {
            _instanceResourceSelectedPaths.Add(entry.Path);
        }

        RaiseInstanceResourceSelectionProperties();
        AddActivity("全选资源", $"{InstanceResourceSurfaceTitle} • 已选中 {InstanceResourceEntries.Count} 项。");
    }

    private void ClearInstanceResourceSelection()
    {
        if (!HasSelectedInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = false;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        RaiseInstanceResourceSelectionProperties();
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(FrontendInstanceResourceEntry entry)
    {
        var detailCommand = new ActionCommand(() => _ = ShowInstanceResourceDetailsAsync(entry));
        var openCommand = new ActionCommand(() =>
            OpenInstanceTarget("打开资源文件位置", entry.Path, $"{InstanceResourceSurfaceTitle} 项目不存在。"));
        var toggleCommand = IsInstanceResourceToggleSupported()
            ? new ActionCommand(() => _ = SetInstanceResourceEntriesEnabledAsync(
                new[] { (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabled) },
                !entry.IsEnabled,
                "当前没有可切换状态的资源。"))
            : null;
        var deleteCommand = new ActionCommand(() => _ = DeleteInstanceResourcesAsync(
            new[] { (Title: entry.Title, Path: entry.Path) },
            "当前没有可删除的资源。"));

        return new InstanceResourceEntryViewModel(
            LoadLauncherBitmap("Images", "Blocks", entry.IconName),
            entry.Title,
            entry.Summary,
            entry.Meta,
            entry.Path,
            openCommand,
            actionToolTip: "打开文件位置",
            isEnabled: entry.IsEnabled,
            showSelection: true,
            isSelected: _instanceResourceSelectedPaths.Contains(entry.Path),
            selectionChanged: isSelected => HandleInstanceResourceSelectionChanged(entry.Path, isSelected),
            infoCommand: detailCommand,
            openCommand: openCommand,
            toggleCommand: toggleCommand,
            deleteCommand: deleteCommand);
    }

    private async Task ShowInstanceResourceDetailsAsync(FrontendInstanceResourceEntry entry)
    {
        var lines = new List<string>
        {
            $"名称: {entry.Title}",
            $"状态: {(entry.IsEnabled ? "启用" : "禁用")}"
        };

        if (!string.IsNullOrWhiteSpace(entry.Meta))
        {
            lines.Add($"类型: {entry.Meta}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            lines.Add($"摘要: {entry.Summary}");
        }

        lines.Add($"路径: {entry.Path}");

        try
        {
            if (Directory.Exists(entry.Path))
            {
                var directoryInfo = new DirectoryInfo(entry.Path);
                lines.Add("项目类型: 文件夹");
                lines.Add($"创建时间: {directoryInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"修改时间: {directoryInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
            else if (File.Exists(entry.Path))
            {
                var fileInfo = new FileInfo(entry.Path);
                lines.Add($"文件大小: {FormatInstanceResourceFileSize(fileInfo.Length)}");
                lines.Add($"创建时间: {fileInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"修改时间: {fileInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            lines.Add($"附加信息读取失败: {ex.Message}");
        }

        var result = await ShowToolboxConfirmationAsync(
            $"查看{InstanceResourceSurfaceTitle}详情",
            string.Join(Environment.NewLine, lines),
            "关闭");
        if (result is not null)
        {
            AddActivity($"查看{InstanceResourceSurfaceTitle}详情", entry.Title);
        }
    }

    private static string FormatInstanceResourceFileSize(long bytes)
    {
        string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.##} {units[unitIndex]}";
    }

    private void RefreshInstanceWorldEntries()
    {
        var filteredEntries = _instanceComposition.World.Entries
            .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Path, InstanceWorldSearchQuery));

        ReplaceItems(
            InstanceWorldEntries,
            ApplyInstanceWorldSort(filteredEntries)
                .Select(entry => new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => OpenVersionSaveDetails(entry.Path)))));
    }

    private IEnumerable<FrontendInstanceDirectoryEntry> ApplyInstanceWorldSort(IEnumerable<FrontendInstanceDirectoryEntry> entries)
    {
        return _instanceWorldSortMethod switch
        {
            InstanceWorldSortMethod.CreateTime => entries
                .OrderByDescending(entry => GetDirectoryCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceWorldSortMethod.ModifyTime => entries
                .OrderByDescending(entry => GetDirectoryLastWriteTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void SetInstanceWorldSortMethod(InstanceWorldSortMethod target)
    {
        if (_instanceWorldSortMethod == target)
        {
            return;
        }

        _instanceWorldSortMethod = target;

        RaisePropertyChanged(nameof(InstanceWorldSortText));
        RefreshInstanceWorldEntries();
    }

    internal void SetInstanceWorldFileNameSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.FileName);

    internal void SetInstanceWorldCreateTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.CreateTime);

    internal void SetInstanceWorldModifyTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.ModifyTime);

    private static string GetInstanceWorldSortName(InstanceWorldSortMethod method)
    {
        return method switch
        {
            InstanceWorldSortMethod.CreateTime => "创建时间",
            InstanceWorldSortMethod.ModifyTime => "修改时间",
            _ => "文件名"
        };
    }

    private static string GetInstanceResourceSortName(InstanceResourceSortMethod method)
    {
        return method switch
        {
            InstanceResourceSortMethod.FileName => "文件名",
            InstanceResourceSortMethod.CreateTime => "加入时间",
            InstanceResourceSortMethod.FileSize => "文件大小",
            _ => "资源名称"
        };
    }

    private static DateTime GetDirectoryCreationTimeUtc(string path)
    {
        try
        {
            return Directory.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetDirectoryLastWriteTimeUtc(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetPathCreationTimeUtc(string path)
    {
        try
        {
            return IsDirectoryPath(path) ? Directory.GetCreationTimeUtc(path) : File.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsDirectoryPath(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void RefreshInstanceScreenshotEntries()
    {
        ReplaceItems(
            InstanceScreenshotEntries,
            _instanceComposition.Screenshot.Entries.Select(entry => CreateInstanceScreenshotEntry(
                entry.Title,
                entry.Summary,
                entry.Path,
                LoadInstanceBitmap(entry.Path, "Images", "Backgrounds", "server_bg.png"))));
    }

    private void RefreshInstanceServerEntries()
    {
        ReplaceItems(
            InstanceServerEntries,
            _instanceComposition.Server.Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Address, entry.Status, InstanceServerSearchQuery))
                .Select(CreateInstanceServerEntry));
    }

    private InstanceServerEntryViewModel CreateInstanceServerEntry(FrontendInstanceServerEntry entry)
    {
        InstanceServerEntryViewModel? viewModel = null;
        viewModel = new InstanceServerEntryViewModel(
            entry.Title,
            entry.Address,
            LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png"),
            LoadLauncherBitmap("Images", "Icons", "DefaultServer.png"),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = RefreshInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = CopyInstanceServerAddressAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = ConnectInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() => ViewInstanceServer(entry)));
        ApplyInstanceServerIdleState(viewModel, entry.Status);
        return viewModel;
    }

    private async Task RefreshAllInstanceServersAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("刷新服务器信息", "当前未选择实例。");
            return;
        }

        ReloadInstanceComposition();
        var entries = InstanceServerEntries.ToArray();
        if (entries.Length == 0)
        {
            AddActivity("刷新服务器信息", "已重新扫描当前实例中的服务器列表，但当前实例没有已保存的服务器。");
            return;
        }

        await Task.WhenAll(entries.Select(entry => RefreshInstanceServerAsync(entry, addActivity: false)));
        AddActivity("刷新服务器信息", $"已刷新 {entries.Length} 个服务器。");
    }

    private async Task RefreshInstanceServerAsync(InstanceServerEntryViewModel entry, bool addActivity = true)
    {
        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            ApplyInstanceServerErrorState(entry, "服务器地址为空。");
            if (addActivity)
            {
                AddActivity("刷新服务器信息失败", $"{entry.Title} • 服务器地址为空。");
            }

            return;
        }

        ApplyInstanceServerLoadingState(entry);

        try
        {
            var reachableAddress = await ResolveMinecraftServerQueryEndpointAsync(address, CancellationToken.None);
            using var queryService = global::PCL.Core.Link.McPing.McPingServiceFactory.CreateService(reachableAddress.Ip, reachableAddress.Port);
            var result = await queryService.PingAsync(CancellationToken.None);
            if (result is null)
            {
                throw new InvalidOperationException("未返回服务器信息");
            }

            ApplyInstanceServerSuccessState(entry, result);
            if (addActivity)
            {
                AddActivity("刷新服务器信息", $"{entry.Title} • {result.Players.Online}/{result.Players.Max} • {result.Latency}ms");
            }
        }
        catch (Exception ex)
        {
            ApplyInstanceServerErrorState(entry, ex.Message);
            if (addActivity)
            {
                AddActivity("刷新服务器信息失败", $"{entry.Title} • {ex.Message}");
            }
        }
    }

    private static void ApplyInstanceServerIdleState(InstanceServerEntryViewModel entry, string status)
    {
        entry.StatusText = string.IsNullOrWhiteSpace(status) ? "已保存服务器" : status;
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = "-/-";
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines("点击刷新查看服务器状态");
    }

    private static void ApplyInstanceServerLoadingState(InstanceServerEntryViewModel entry)
    {
        entry.StatusText = "正在连接...";
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = "正在连接";
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines("正在连接...");
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private static void ApplyInstanceServerSuccessState(InstanceServerEntryViewModel entry, global::PCL.Core.Link.McPing.Model.McPingResult result)
    {
        entry.StatusText = "服务器在线";
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = $"{result.Players.Online}/{result.Players.Max}";
        entry.Latency = $"{result.Latency}ms";
        entry.LatencyBrush = GetMinecraftServerQueryLatencyBrush(result.Latency);
        entry.PlayerTooltip = result.Players.Samples?.Any() == true
            ? string.Join(Environment.NewLine, result.Players.Samples.Select(sample => sample.Name))
            : null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(result.Description);
        entry.Logo = DecodeMinecraftServerQueryLogo(result.Favicon)
            ?? LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private static void ApplyInstanceServerErrorState(InstanceServerEntryViewModel entry, string message)
    {
        entry.StatusText = $"无法连接: {message}";
        entry.StatusBrush = global::Avalonia.Media.Brushes.Red;
        entry.PlayerCount = "离线";
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines("服务器离线");
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private async Task CopyInstanceServerAddressAsync(InstanceServerEntryViewModel entry)
    {
        var address = (entry.Address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity("复制服务器地址", $"{entry.Title} 没有可复制的地址。");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(address);
            AddActivity("复制服务器地址", $"{entry.Title} • {address}");
        }
        catch (Exception ex)
        {
            AddActivity("复制服务器地址失败", ex.Message);
        }
    }

    private async Task ConnectInstanceServerAsync(InstanceServerEntryViewModel entry)
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("连接服务器", "当前未选择实例。");
            return;
        }

        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity("连接服务器", $"{entry.Title} 没有可用的服务器地址。");
            return;
        }

        try
        {
            InstanceServerAutoJoin = address;
            RefreshLaunchState();
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                $"已切换到启动页并准备连接服务器 {entry.Title}。");
            await HandleLaunchRequestedAsync();
            AddActivity("连接服务器", $"{entry.Title} • {address}");
        }
        catch (Exception ex)
        {
            AddActivity("连接服务器失败", ex.Message);
        }
    }

    private async Task PasteInstanceWorldClipboardAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("粘贴剪贴板文件", "当前未选择实例。");
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch (Exception ex)
        {
            AddActivity("粘贴剪贴板文件失败", ex.Message);
            return;
        }

        var sourcePaths = ParseClipboardPaths(clipboardText)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourcePaths.Length == 0)
        {
            AddActivity("粘贴剪贴板文件", "剪贴板中没有可导入的文件或文件夹路径。");
            return;
        }

        var savesDirectory = Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves");
        Directory.CreateDirectory(savesDirectory);

        var importedTargets = new List<string>();
        foreach (var sourcePath in sourcePaths)
        {
            var targetPath = GetUniqueChildPath(savesDirectory, Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, targetPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath, overwrite: false);
            }

            importedTargets.Add(targetPath);
        }

        ReloadInstanceComposition();
        AddActivity("粘贴剪贴板文件", string.Join(Environment.NewLine, importedTargets));
    }

    private async Task AddInstanceServerAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("添加新服务器", "当前未选择实例。");
            return;
        }

        var serverInfo = default((bool Success, string Name, string Address, string? Activity));
        try
        {
            serverInfo = await PromptForNewInstanceServerAsync();
        }
        catch (Exception ex)
        {
            AddActivity("添加新服务器失败", ex.Message);
            return;
        }

        if (!serverInfo.Success)
        {
            if (!string.IsNullOrWhiteSpace(serverInfo.Activity))
            {
                AddActivity("添加新服务器", serverInfo.Activity);
            }
            return;
        }

        var name = serverInfo.Name;
        var address = serverInfo.Address;

        var serversPath = Path.Combine(_instanceComposition.Selection.IndieDirectory, "servers.dat");
        NbtList serverList;
        try
        {
            serverList = File.Exists(serversPath)
                ? LoadInstanceServerList(serversPath) ?? new NbtList("servers", NbtTagType.Compound)
                : new NbtList("servers", NbtTagType.Compound);
        }
        catch (Exception ex)
        {
            AddActivity("添加新服务器失败", $"无法读取当前实例的服务器列表。{Environment.NewLine}{ex.Message}");
            return;
        }

        try
        {
            if (serverList.ListType == NbtTagType.Unknown)
            {
                serverList.ListType = NbtTagType.Compound;
            }

            serverList.Add(new NbtCompound
            {
                new NbtString("name", name),
                new NbtString("ip", address)
            });

        }
        catch (Exception ex)
        {
            AddActivity("添加新服务器失败", $"无法更新当前实例的服务器列表。{Environment.NewLine}{ex.Message}");
            return;
        }

        try
        {
            var clonedServerList = (NbtList)serverList.Clone();
            if (!TryWriteInstanceServerList(serversPath, clonedServerList))
            {
                AddActivity("添加新服务器失败", "无法写入当前实例的服务器列表。");
                return;
            }
        }
        catch
        {
            AddActivity("添加新服务器失败", "无法写入当前实例的服务器列表。");
            return;
        }

        ReloadInstanceComposition();
        AddActivity("添加新服务器", $"{name} • {address}");
    }

    private async Task<(bool Success, string Name, string Address, string? Activity)> PromptForNewInstanceServerAsync()
    {
        var resolvedName = await _shellActionService.PromptForTextAsync(
            "编辑服务器信息",
            "请输入新的服务器名称：",
            "Minecraft服务器");
        if (resolvedName is null)
        {
            return (false, string.Empty, string.Empty, null);
        }

        var resolvedAddress = await _shellActionService.PromptForTextAsync(
            "编辑服务器信息",
            "请输入新的服务器地址：");
        if (string.IsNullOrWhiteSpace(resolvedAddress))
        {
            return resolvedAddress is null
                ? (false, string.Empty, string.Empty, null)
                : (false, string.Empty, string.Empty, "服务器地址不能为空。");
        }

        return (
            true,
            string.IsNullOrWhiteSpace(resolvedName) ? "Minecraft服务器" : resolvedName.Trim(),
            resolvedAddress.Trim(),
            null);
    }

    private static NbtList? LoadInstanceServerList(string serversPath)
    {
        var file = new NbtFile();
        using var stream = File.OpenRead(serversPath);
        file.LoadFromStream(stream, NbtCompression.AutoDetect);
        return file.RootTag.Get<NbtList>("servers");
    }

    private static bool TryWriteInstanceServerList(string serversPath, NbtList serverList)
    {
        var directoryPath = Path.GetDirectoryName(serversPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        Directory.CreateDirectory(directoryPath);

        var rootTag = new NbtCompound { Name = string.Empty };
        rootTag.Add(serverList);

        var file = new NbtFile(rootTag);
        using var stream = File.Create(serversPath);
        file.SaveToStream(stream, NbtCompression.None);
        return true;
    }

    private async Task InstallInstanceResourceFromFileAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("从文件安装资源", "当前未选择实例。");
            return;
        }

        var (typeName, patterns) = ResolveInstanceResourcePickerOptions();

        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync($"选择{InstanceResourceSurfaceTitle}文件", typeName, patterns);
        }
        catch (Exception ex)
        {
            AddActivity("从文件安装资源失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("从文件安装资源", "已取消选择资源文件。");
            return;
        }

        var targetDirectory = GetCurrentInstanceResourceDirectory();
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueChildPath(targetDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, overwrite: false);

        ReloadInstanceComposition();
        AddActivity("从文件安装资源", $"{sourcePath} -> {targetPath}");
    }

    private void DownloadInstanceResource()
    {
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, ResolveInstanceDownloadSubpage()),
            $"{InstanceResourceSurfaceTitle} 页面已跳转到下载页。");
    }

    private async Task SetSelectedInstanceResourcesEnabledAsync(bool isEnabled)
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(isEnabled ? "启用资源" : "禁用资源", "当前未选择实例。");
            return;
        }

        if (!IsInstanceResourceToggleSupported())
        {
            AddActivity(isEnabled ? "启用资源" : "禁用资源", $"{InstanceResourceSurfaceTitle} 当前不支持启用或禁用操作。");
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabledState))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity(isEnabled ? "启用资源" : "禁用资源", "当前没有选中的资源。");
            return;
        }

        await SetInstanceResourceEntriesEnabledAsync(selectedEntries, isEnabled, "当前没有选中的资源。");
    }

    private Task SetInstanceResourceEntriesEnabledAsync(
        IReadOnlyList<(string Title, string Path, bool IsEnabledState)> entries,
        bool isEnabled,
        string emptyMessage)
    {
        if (entries.Count == 0)
        {
            AddActivity(isEnabled ? "启用资源" : "禁用资源", emptyMessage);
            return Task.CompletedTask;
        }

        var candidates = entries
            .Where(entry => entry.IsEnabledState != isEnabled)
            .ToArray();
        if (candidates.Length == 0)
        {
            AddActivity(isEnabled ? "启用资源" : "禁用资源", isEnabled ? "选中的资源已经全部启用。" : "选中的资源已经全部禁用。");
            return Task.CompletedTask;
        }

        var succeededEntries = new List<string>();
        var failedEntries = new List<string>();

        foreach (var entry in candidates)
        {
            try
            {
                SetInstanceResourceEnabled(entry.Path, isEnabled);
                succeededEntries.Add(entry.Title);
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ReloadInstanceComposition();
        if (succeededEntries.Count > 0)
        {
            AddActivity(
                isEnabled ? "启用资源" : "禁用资源",
                failedEntries.Count == 0
                    ? $"{(isEnabled ? "已启用" : "已禁用")} {succeededEntries.Count} 项：{string.Join("、", succeededEntries)}"
                    : $"{(isEnabled ? "已启用" : "已禁用")} {succeededEntries.Count} 项，{failedEntries.Count} 项失败。");
        }

        if (failedEntries.Count > 0)
        {
            AddActivity(isEnabled ? "启用资源失败" : "禁用资源失败", string.Join(Environment.NewLine, failedEntries));
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedInstanceResourcesAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("删除资源", "当前未选择实例。");
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity("删除资源", "当前没有选中的资源。");
            return;
        }

        await DeleteInstanceResourcesAsync(selectedEntries, "当前没有选中的资源。");
    }

    private async Task DeleteInstanceResourcesAsync(
        IReadOnlyList<(string Title, string Path)> entries,
        string emptyMessage)
    {
        if (entries.Count == 0)
        {
            AddActivity("删除资源", emptyMessage);
            return;
        }

        var itemDescription = string.Join(Environment.NewLine, entries.Take(8).Select(entry => $"- {entry.Title}"));
        if (entries.Count > 8)
        {
            itemDescription = $"{itemDescription}{Environment.NewLine}- 以及另外 {entries.Count - 8} 项";
        }

        var confirmed = await ShowToolboxConfirmationAsync(
            "资源删除确认",
            $"确定要将这 {entries.Count} 个{InstanceResourceSurfaceTitle}项目移入回收区吗？{Environment.NewLine}{Environment.NewLine}{itemDescription}",
            "移入回收区",
            isDanger: true);
        if (confirmed != true)
        {
            if (confirmed == false)
            {
                AddActivity("删除资源", "已取消删除。");
            }

            return;
        }

        var trashDirectory = ResolveInstanceResourceTrashDirectory();
        Directory.CreateDirectory(trashDirectory);

        var succeededEntries = new List<string>();
        var failedEntries = new List<string>();
        foreach (var entry in entries)
        {
            try
            {
                MoveInstanceResourceToTrash(entry.Path, trashDirectory);
                succeededEntries.Add(entry.Title);
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ReloadInstanceComposition();
        if (succeededEntries.Count > 0)
        {
            AddActivity("删除资源", $"已移入回收区 {succeededEntries.Count} 项。");
        }

        if (failedEntries.Count > 0)
        {
            AddActivity("删除资源失败", string.Join(Environment.NewLine, failedEntries));
        }
    }

    private void ExportInstanceResourceInfo()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("导出资源信息", "当前未选择实例。");
            return;
        }

        var entries = GetCurrentInstanceResourceState().Entries;
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-resources");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(
            exportDirectory,
            $"{_instanceComposition.Selection.InstanceName}-{ResolveInstanceResourceExportSlug()}-info.txt");
        var lines = entries.Count == 0
            ? [$"{InstanceResourceSurfaceTitle} 列表为空。"]
            : entries.Select(entry => $"{entry.Title} | {entry.Meta} | {entry.Summary} | {entry.Path}").ToArray();
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget("导出资源信息", outputPath, "导出文件不存在。");
    }

    private void SetInstanceResourceEnabled(string path, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("资源路径为空。");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("资源文件不存在。", path);
        }

        if (isEnabled)
        {
            var enabledFileName = Path.GetFileName(path);
            if (enabledFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".disabled".Length];
            }
            else if (enabledFileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".old".Length];
            }

            var enabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, enabledFileName);
            File.Move(path, enabledPath);
            return;
        }

        var disabledFileName = $"{Path.GetFileName(path)}.disabled";
        var disabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, disabledFileName);
        File.Move(path, disabledPath);
    }

    private void CheckInstanceMods() => _ = CheckInstanceModsAsync();

    private async Task CheckInstanceModsAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("检查 Mod", "当前未选择实例。");
            return;
        }

        var enabledMods = _instanceComposition.Mods.Entries;
        var disabledMods = _instanceComposition.DisabledMods.Entries;
        var duplicateGroups = enabledMods
            .Concat(disabledMods)
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string>
        {
            $"实例: {_instanceComposition.Selection.InstanceName}",
            $"启用 Mod: {enabledMods.Count}",
            $"已禁用 Mod: {disabledMods.Count}",
            $"重复名称: {duplicateGroups.Length}",
            string.Empty
        };

        if (duplicateGroups.Length == 0)
        {
            lines.Add("未检测到重复名称的 Mod 文件。");
        }
        else
        {
            lines.Add("重复名称的 Mod:");
            foreach (var group in duplicateGroups)
            {
                lines.Add($"- {group.Key}");
                foreach (var entry in group)
                {
                    lines.Add($"  {entry.Meta} | {entry.Summary} | {entry.Path}");
                }
            }
        }

        var result = await ShowToolboxConfirmationAsync("检查 Mod", string.Join(Environment.NewLine, lines));
        if (result is null)
        {
            return;
        }

        AddActivity(
            "检查 Mod",
            $"已检查 {_instanceComposition.Selection.InstanceName}：启用 {enabledMods.Count} 个，禁用 {disabledMods.Count} 个，重复 {duplicateGroups.Length} 组。");
    }

    private void ViewInstanceServer(FrontendInstanceServerEntry entry)
    {
        OpenMinecraftServerInspector(entry.Address);
    }

    private FrontendInstanceResourceState GetCurrentInstanceResourceState()
    {
        return new FrontendInstanceResourceState(GetCurrentInstanceResourceSourceEntries());
    }

    private string ResolveInstanceResourceSurfaceTitle()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "Mod",
            LauncherFrontendSubpageKey.VersionResourcePack => "资源包",
            LauncherFrontendSubpageKey.VersionShader => "光影包",
            LauncherFrontendSubpageKey.VersionSchematic => "投影原理图",
            _ => "资源"
        };
    }

    private string GetCurrentInstanceResourceDirectory()
    {
        var folderName = _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "mods"
        };

        return ResolveCurrentInstanceResourceDirectory(folderName);
    }

    private (string TypeName, string[] Patterns) ResolveInstanceResourcePickerOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => ("资源包文件", ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionShader => ("光影文件", ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionSchematic => ("投影原理图文件", ["*.litematic", "*.schem", "*.schematic", "*.nbt"]),
            _ => ("Mod 文件", ["*.jar", "*.disabled", "*.old"])
        };
    }

    private LauncherFrontendSubpageKey ResolveInstanceDownloadSubpage()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => LauncherFrontendSubpageKey.DownloadResourcePack,
            LauncherFrontendSubpageKey.VersionShader => LauncherFrontendSubpageKey.DownloadShader,
            LauncherFrontendSubpageKey.VersionSchematic => LauncherFrontendSubpageKey.DownloadMod,
            _ => LauncherFrontendSubpageKey.DownloadMod
        };
    }

    private string ResolveInstanceResourceExportSlug()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "mods",
            LauncherFrontendSubpageKey.VersionModDisabled => "mods",
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "resources"
        };
    }

    private string ResolveInstanceResourceTrashDirectory()
    {
        return Path.Combine(
            _instanceComposition.Selection.LauncherDirectory,
            ".pcl-trash",
            "resources",
            ResolveInstanceResourceExportSlug());
    }

    private static void MoveInstanceResourceToTrash(string sourcePath, string trashDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("资源路径为空。");
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("资源项目不存在。", sourcePath);
        }

        Directory.CreateDirectory(trashDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetPath = GetUniqueChildPath(
            trashDirectory,
            $"{timestamp}-{Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}");

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
            return;
        }

        File.Move(sourcePath, targetPath);
    }

    private static IEnumerable<string> ParseClipboardPaths(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return [];
        }

        return clipboardText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('"'))
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string GetUniqueChildPath(string directory, string fileOrFolderName)
    {
        var candidate = Path.Combine(directory, fileOrFolderName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileOrFolderName);
        var extension = Path.GetExtension(fileOrFolderName);
        var suffix = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetPath, overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static bool MatchesSearch(params string[] values)
    {
        if (values.Length == 0)
        {
            return true;
        }

        var query = values[^1];
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return values
            .Take(values.Length - 1)
            .Any(value => !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}

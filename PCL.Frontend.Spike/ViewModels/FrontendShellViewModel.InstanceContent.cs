using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceWorldSearchQuery = string.Empty;
    private string _instanceServerSearchQuery = string.Empty;
    private string _instanceResourceSearchQuery = string.Empty;
    private string _instanceResourceSurfaceTitle = "Mod";

    public string InstanceWorldSearchQuery
    {
        get => _instanceWorldSearchQuery;
        set => SetProperty(ref _instanceWorldSearchQuery, value);
    }

    public string InstanceServerSearchQuery
    {
        get => _instanceServerSearchQuery;
        set => SetProperty(ref _instanceServerSearchQuery, value);
    }

    public string InstanceResourceSearchQuery
    {
        get => _instanceResourceSearchQuery;
        set => SetProperty(ref _instanceResourceSearchQuery, value);
    }

    public string InstanceResourceSurfaceTitle
    {
        get => _instanceResourceSurfaceTitle;
        private set => SetProperty(ref _instanceResourceSurfaceTitle, value);
    }

    public bool HasInstanceWorldEntries => InstanceWorldEntries.Count > 0;

    public bool HasNoInstanceWorldEntries => !HasInstanceWorldEntries;

    public bool HasInstanceScreenshotEntries => InstanceScreenshotEntries.Count > 0;

    public bool HasNoInstanceScreenshotEntries => !HasInstanceScreenshotEntries;

    public bool HasInstanceServerEntries => InstanceServerEntries.Count > 0;

    public bool HasNoInstanceServerEntries => !HasInstanceServerEntries;

    public bool HasInstanceResourceEntries => InstanceResourceEntries.Count > 0;

    public bool HasNoInstanceResourceEntries => !HasInstanceResourceEntries;

    public bool ShowInstanceResourceUnsupportedState => IsInstanceResourceSurface
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionSchematic
        && HasNoInstanceResourceEntries;

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
        LauncherFrontendSubpageKey.VersionMod => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionModDisabled => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionResourcePack => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionShader => "尚未安装资源",
        LauncherFrontendSubpageKey.VersionSchematic => "该实例不可用投影原理图",
        _ => "尚未安装资源"
    };

    public string InstanceResourceEmptyDescription => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.VersionSchematic => "你可能需要先安装投影 Mod，如果已经安装过了投影 Mod 请先启动一次游戏。也可能是你选择错了实例，请点击实例选择按钮切换实例。",
        _ => "你可以从已经下载好的文件安装资源。如果你已经安装了资源，可能是版本隔离设置有误，请在设置中调整版本隔离选项。"
    };

    public bool ShowInstanceResourceCheckButton => _currentRoute.Subpage is LauncherFrontendSubpageKey.VersionMod
        or LauncherFrontendSubpageKey.VersionModDisabled;

    public ActionCommand OpenInstanceWorldFolderCommand => new(() =>
        AddActivity("打开存档文件夹", "/Users/demo/.pcl/instances/Modern Fabric Demo/saves"));

    public ActionCommand PasteInstanceWorldClipboardCommand => new(() =>
        AddActivity("粘贴剪贴板文件", "Would import save files from the clipboard into the current instance."));

    public ActionCommand OpenInstanceScreenshotFolderCommand => new(() =>
        AddActivity("打开截图文件夹", "/Users/demo/.pcl/instances/Modern Fabric Demo/screenshots"));

    public ActionCommand RefreshInstanceServerCommand => new(() =>
        AddActivity("刷新服务器信息", "Would reload the instance server list from servers.dat."));

    public ActionCommand AddInstanceServerCommand => new(() =>
        AddActivity("添加新服务器", "Would open the add-server dialog for the current instance."));

    public ActionCommand OpenInstanceResourceFolderCommand => new(() =>
        AddActivity("打开资源文件夹", $"/Users/demo/.pcl/instances/Modern Fabric Demo/{GetInstanceResourceFolderName()}"));

    public ActionCommand InstallInstanceResourceFromFileCommand => new(() =>
        AddActivity("从文件安装资源", $"{InstanceResourceSurfaceTitle} • Would open a local file picker."));

    public ActionCommand DownloadInstanceResourceCommand => new(() =>
        AddActivity("下载新资源", $"{InstanceResourceSurfaceTitle} • Would jump to the download surface.")); 

    public ActionCommand SelectAllInstanceResourcesCommand => new(() =>
        AddActivity("全选资源", $"{InstanceResourceSurfaceTitle} • Would toggle select-all for the current list."));

    public ActionCommand ExportInstanceResourceInfoCommand => new(() =>
        AddActivity("导出资源信息", $"{InstanceResourceSurfaceTitle} • Would export detailed item metadata."));

    public ActionCommand CheckInstanceModsCommand => new(() =>
        AddActivity("检查 Mod", "Would run duplicate, dependency, and compatibility checks for installed mods."));

    private void InitializeInstanceContentSurfaces()
    {
        _instanceWorldSearchQuery = string.Empty;
        _instanceServerSearchQuery = string.Empty;
        _instanceResourceSearchQuery = string.Empty;
        _instanceResourceSurfaceTitle = "Mod";

        ReplaceItems(InstanceWorldEntries,
        [
            new SimpleListEntryViewModel("Demo Survival World", "主世界存档 • 最近打开于 2026-04-04 11:32", new ActionCommand(() => AddActivity("查看存档", "Demo Survival World"))),
            new SimpleListEntryViewModel("Creative Test Flat", "创造测试地图 • 含建筑样板与命令方块", new ActionCommand(() => AddActivity("查看存档", "Creative Test Flat"))),
            new SimpleListEntryViewModel("Redstone Benchmark", "红石性能测试存档 • 仅本地保留", new ActionCommand(() => AddActivity("查看存档", "Redstone Benchmark")))
        ]);

        ReplaceItems(InstanceScreenshotEntries,
        [
            CreateInstanceScreenshotEntry("2026-04-04_11.42.18.png", "主界面广场截图 • 1920×1080", LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png")),
            CreateInstanceScreenshotEntry("2026-04-02_21.15.04.png", "仓库区夜景截图 • 1920×1080", LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png")),
            CreateInstanceScreenshotEntry("2026-03-29_18.07.33.png", "工业区俯视截图 • 1920×1080", LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png"))
        ]);

        ReplaceItems(InstanceServerEntries,
        [
            new InstanceServerEntryViewModel("PCL CE Demo", "play.pclce.example:25565", "在线 • 23ms", new ActionCommand(() => AddActivity("查看服务器", "PCL CE Demo"))),
            new InstanceServerEntryViewModel("Fabric Snapshot Test", "snapshot.fabric.example:25570", "维护中 • 上次在线 2 小时前", new ActionCommand(() => AddActivity("查看服务器", "Fabric Snapshot Test"))),
            new InstanceServerEntryViewModel("LittleSkin Auth Arena", "arena.littleskin.example:25575", "需第三方验证", new ActionCommand(() => AddActivity("查看服务器", "LittleSkin Auth Arena")))
        ]);

        RefreshInstanceResourceEntries();
    }

    private void RefreshInstanceContentSurfaces()
    {
        if (IsInstanceWorldSurface)
        {
            RaisePropertyChanged(nameof(InstanceWorldSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceWorldEntries));
            RaisePropertyChanged(nameof(HasNoInstanceWorldEntries));
        }

        if (IsInstanceScreenshotSurface)
        {
            RaisePropertyChanged(nameof(HasInstanceScreenshotEntries));
            RaisePropertyChanged(nameof(HasNoInstanceScreenshotEntries));
        }

        if (IsInstanceServerSurface)
        {
            RaisePropertyChanged(nameof(InstanceServerSearchQuery));
            RaisePropertyChanged(nameof(HasInstanceServerEntries));
            RaisePropertyChanged(nameof(HasNoInstanceServerEntries));
        }

        if (IsInstanceResourceSurface)
        {
            RefreshInstanceResourceEntries();
            RaisePropertyChanged(nameof(InstanceResourceSearchQuery));
            RaisePropertyChanged(nameof(InstanceResourceSurfaceTitle));
            RaisePropertyChanged(nameof(InstanceResourceSearchWatermark));
            RaisePropertyChanged(nameof(InstanceResourceDownloadButtonText));
            RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
            RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
            RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
            RaisePropertyChanged(nameof(HasInstanceResourceEntries));
            RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
            RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
            RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
            RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
        }
    }

    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            new ActionCommand(() => AddActivity("打开截图", title)));
    }

    private void RefreshInstanceResourceEntries()
    {
        if (!IsInstanceResourceSurface)
        {
            return;
        }

        InstanceResourceSurfaceTitle = _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "已禁用 Mod",
            LauncherFrontendSubpageKey.VersionResourcePack => "资源包",
            LauncherFrontendSubpageKey.VersionShader => "光影包",
            LauncherFrontendSubpageKey.VersionSchematic => "投影原理图",
            _ => "资源"
        };

        _instanceResourceSearchQuery = string.Empty;

        ReplaceItems(InstanceResourceEntries, _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod =>
            [
                CreateInstanceResourceEntry("Sodium", "现代性能优化模组，主打渲染加速与更稳定的帧率。", "已启用 • Fabric • 1.21.1", "Fabric.png"),
                CreateInstanceResourceEntry("Create", "大型机械与物流科技模组，保留原版风味的自动化体验。", "已启用 • Forge 兼容桥接", "Anvil.png"),
                CreateInstanceResourceEntry("JourneyMap", "地图与路标模组，提供地表和洞穴实时浏览。", "可更新 • 发现新版本", "Grass.png")
            ],
            LauncherFrontendSubpageKey.VersionModDisabled =>
            [
                CreateInstanceResourceEntry("OptiFine", "当前被停用的图形增强模组。", "已禁用 • 与当前模组集兼容性较差", "GrassPath.png"),
                CreateInstanceResourceEntry("Not Enough Crashes", "错误捕获模组，当前手动禁用。", "已禁用 • 可随时恢复", "RedstoneBlock.png")
            ],
            LauncherFrontendSubpageKey.VersionResourcePack =>
            [
                CreateInstanceResourceEntry("Faithful 32x", "经典高清材质包，保留原版风格。", "已启用 • 本地资源包", "Grass.png"),
                CreateInstanceResourceEntry("Fresh Animations", "实体动作增强资源包，需要实体模型支持。", "未启用 • 可切换", "Egg.png")
            ],
            LauncherFrontendSubpageKey.VersionShader =>
            [
                CreateInstanceResourceEntry("Complementary Reimagined", "当前实例使用中的主光影方案。", "已启用 • 兼容 Iris", "Quilt.png"),
                CreateInstanceResourceEntry("BSL Shaders", "额外收藏的备用光影。", "未启用 • 可切换", "RedstoneLampOn.png")
            ],
            LauncherFrontendSubpageKey.VersionSchematic => [],
            _ => []
        });
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(string title, string info, string meta, string iconName)
    {
        return new InstanceResourceEntryViewModel(
            LoadLauncherBitmap("Images", "Blocks", iconName),
            title,
            info,
            meta,
            new ActionCommand(() => AddActivity("查看资源", $"{InstanceResourceSurfaceTitle} • {title}")));
    }

    private string GetInstanceResourceFolderName()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "mods"
        };
    }
}

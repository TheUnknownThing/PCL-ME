using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;

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
        OpenInstanceTarget(
            "打开存档文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            "当前实例没有存档目录。"));

    public ActionCommand PasteInstanceWorldClipboardCommand => new(() =>
        AddActivity("粘贴剪贴板文件", "Would import save files from the clipboard into the current instance."));

    public ActionCommand OpenInstanceScreenshotFolderCommand => new(() =>
        OpenInstanceTarget(
            "打开截图文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "screenshots") : string.Empty,
            "当前实例没有截图目录。"));

    public ActionCommand RefreshInstanceServerCommand => new(() =>
    {
        ReloadInstanceComposition();
        AddActivity("刷新服务器信息", "已重新扫描当前实例中的服务器列表。");
    });

    public ActionCommand AddInstanceServerCommand => new(() =>
        AddActivity("添加新服务器", "Would open the add-server dialog for the current instance."));

    public ActionCommand OpenInstanceResourceFolderCommand => new(() =>
        OpenInstanceTarget("打开资源文件夹", GetCurrentInstanceResourceDirectory(), "当前实例没有对应的资源目录。"));

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
        _instanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();

        RefreshInstanceWorldEntries();
        RefreshInstanceScreenshotEntries();
        RefreshInstanceServerEntries();
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
        ReplaceItems(
            InstanceResourceEntries,
            GetCurrentInstanceResourceState().Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Meta, InstanceResourceSearchQuery))
                .Select(entry => CreateInstanceResourceEntry(entry.Title, entry.Summary, entry.Meta, entry.IconName, entry.Path)));
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(string title, string info, string meta, string iconName, string path)
    {
        return new InstanceResourceEntryViewModel(
            LoadLauncherBitmap("Images", "Blocks", iconName),
            title,
            info,
            meta,
            new ActionCommand(() => OpenInstanceTarget("查看资源", path, $"{InstanceResourceSurfaceTitle} 项目不存在。")));
    }

    private void RefreshInstanceWorldEntries()
    {
        ReplaceItems(
            InstanceWorldEntries,
            _instanceComposition.World.Entries
                .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Path, InstanceWorldSearchQuery))
                .Select(entry => new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => OpenInstanceTarget("查看存档", entry.Path, "当前存档目录不存在。")))));
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
                .Select(entry => new InstanceServerEntryViewModel(
                    entry.Title,
                    entry.Address,
                    entry.Status,
                    new ActionCommand(() => AddActivity("查看服务器", $"{entry.Title} • {entry.Address}")))));
    }

    private FrontendInstanceResourceState GetCurrentInstanceResourceState()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => _instanceComposition.Mods,
            LauncherFrontendSubpageKey.VersionModDisabled => _instanceComposition.DisabledMods,
            LauncherFrontendSubpageKey.VersionResourcePack => _instanceComposition.ResourcePacks,
            LauncherFrontendSubpageKey.VersionShader => _instanceComposition.Shaders,
            LauncherFrontendSubpageKey.VersionSchematic => _instanceComposition.Schematics,
            _ => _instanceComposition.Mods
        };
    }

    private string ResolveInstanceResourceSurfaceTitle()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "已禁用 Mod",
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

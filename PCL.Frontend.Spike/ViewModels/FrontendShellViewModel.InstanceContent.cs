using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceWorldSearchQuery = string.Empty;
    private string _instanceServerSearchQuery = string.Empty;

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

    public bool HasInstanceWorldEntries => InstanceWorldEntries.Count > 0;

    public bool HasNoInstanceWorldEntries => !HasInstanceWorldEntries;

    public bool HasInstanceScreenshotEntries => InstanceScreenshotEntries.Count > 0;

    public bool HasNoInstanceScreenshotEntries => !HasInstanceScreenshotEntries;

    public bool HasInstanceServerEntries => InstanceServerEntries.Count > 0;

    public bool HasNoInstanceServerEntries => !HasInstanceServerEntries;

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

    private void InitializeInstanceContentSurfaces()
    {
        _instanceWorldSearchQuery = string.Empty;
        _instanceServerSearchQuery = string.Empty;

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
    }

    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            new ActionCommand(() => AddActivity("打开截图", title)));
    }
}

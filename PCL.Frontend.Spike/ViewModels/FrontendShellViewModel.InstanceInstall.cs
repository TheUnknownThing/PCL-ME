using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceInstallSelectionTitle = "Modern Fabric Demo";
    private string _instanceInstallSelectionSummary = "Minecraft 1.21.1 / Fabric 0.16.9 / 独立实例";
    private string _instanceInstallMinecraftVersion = "Minecraft 1.21.1";
    private Bitmap? _instanceInstallMinecraftIcon;

    public string InstanceInstallSelectionTitle
    {
        get => _instanceInstallSelectionTitle;
        private set => SetProperty(ref _instanceInstallSelectionTitle, value);
    }

    public string InstanceInstallSelectionSummary
    {
        get => _instanceInstallSelectionSummary;
        private set => SetProperty(ref _instanceInstallSelectionSummary, value);
    }

    public string InstanceInstallMinecraftVersion
    {
        get => _instanceInstallMinecraftVersion;
        private set => SetProperty(ref _instanceInstallMinecraftVersion, value);
    }

    public Bitmap? InstanceInstallMinecraftIcon
    {
        get => _instanceInstallMinecraftIcon;
        private set => SetProperty(ref _instanceInstallMinecraftIcon, value);
    }

    public ActionCommand EditInstanceInstallSelectionCommand => new(() =>
        AddActivity("修改实例安装目标", $"{InstanceInstallSelectionTitle} • {InstanceInstallSelectionSummary}"));

    public ActionCommand EditInstanceInstallMinecraftCommand => new(() =>
        AddActivity("修改 Minecraft 版本", InstanceInstallMinecraftVersion));

    private void InitializeInstanceInstallSurface()
    {
        InstanceInstallSelectionTitle = "Modern Fabric Demo";
        InstanceInstallSelectionSummary = "Minecraft 1.21.1 / Fabric 0.16.9 / 独立实例";
        InstanceInstallMinecraftVersion = "Minecraft 1.21.1";
        InstanceInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", "Grass.png");

        ReplaceItems(InstanceInstallHints,
        [
            CreateNoticeStrip("安装结束后，请在 Mod 下载中搜索 LegacyOptiFabric 并下载，否则 OptiFine 会无法使用！", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("如果不安装 Legacy Fabric API，大多数 Mod 都会无法使用！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("你尚未选择安装 Fabric API，这会导致大多数 Mod 无法使用！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("你尚未选择安装 QFAPI / QSL，这会导致大多数 Mod 无法使用！如果 QFAPI / QSL 无可用版本，你可以选择安装 Fabric API。", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("你选择了在 Quilt 中安装 Fabric API，而当前存在适配的 QFAPI / QSL 可供安装。请优先考虑安装 QFAPI / QSL。", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("你尚未选择安装 OptiFabric，这会导致 OptiFine 无法使用！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("安装结束后，请在 Mod 下载中搜索 OptiFabric Origins 并下载，否则 OptiFine 会无法使用！", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("OptiFine 与一部分 Mod 的兼容性不佳，请谨慎安装。", "#FFF8DD", "#F2D777", "#6E5800")
        ]);

        ReplaceItems(InstanceInstallOptions,
        [
            CreateDownloadInstallOption("Forge", "1.20.1 recommended", LoadLauncherBitmap("Images", "Blocks", "Anvil.png")),
            CreateDownloadInstallOption("Cleanroom", "1.12.2 experimental", LoadLauncherBitmap("Images", "Blocks", "Cleanroom.png")),
            CreateDownloadInstallOption("NeoForge", "21.1.2", LoadLauncherBitmap("Images", "Blocks", "NeoForge.png")),
            CreateDownloadInstallOption("Fabric", "0.16.9", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Legacy Fabric", "1.8.9 backport", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Fabric API", "0.118.0+1.21.1", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Legacy Fabric API", "1.7.4+1.8.9", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Quilt", "0.27.1", LoadLauncherBitmap("Images", "Blocks", "Quilt.png")),
            CreateDownloadInstallOption("QFAPI / QSL", "11.0.0-beta", LoadLauncherBitmap("Images", "Blocks", "Quilt.png")),
            CreateDownloadInstallOption("LabyMod", "4.1.23", LoadLauncherBitmap("Images", "Blocks", "LabyMod.png")),
            CreateDownloadInstallOption("OptiFine", "HD_U_I6", LoadLauncherBitmap("Images", "Blocks", "GrassPath.png")),
            CreateDownloadInstallOption("OptiFabric", "1.14.3", LoadLauncherBitmap("Images", "Blocks", "OptiFabric.png")),
            CreateDownloadInstallOption("LiteLoader", "1.12.2-SNAPSHOT", LoadLauncherBitmap("Images", "Blocks", "LiteLoader.png"))
        ]);
    }

    private void RefreshInstanceInstallSurface()
    {
        if (!IsInstanceInstallSurface)
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceInstallSelectionTitle));
        RaisePropertyChanged(nameof(InstanceInstallSelectionSummary));
        RaisePropertyChanged(nameof(InstanceInstallMinecraftVersion));
        RaisePropertyChanged(nameof(InstanceInstallMinecraftIcon));
    }
}

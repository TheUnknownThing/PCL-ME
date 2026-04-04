using System.Collections.ObjectModel;
using System.Linq;
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
        var installState = _instanceComposition.Install;
        InstanceInstallSelectionTitle = installState.SelectionTitle;
        InstanceInstallSelectionSummary = installState.SelectionSummary;
        InstanceInstallMinecraftVersion = installState.MinecraftVersion;
        InstanceInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", installState.MinecraftIconName);

        ReplaceItems(
            InstanceInstallHints,
            installState.Hints.Select(hint => CreateNoticeStrip(hint, "#FFF1EA", "#F1C8B6", "#A94F2B")));

        ReplaceItems(
            InstanceInstallOptions,
            installState.Options.Select(option => CreateDownloadInstallOption(
                option.Title,
                option.Selection,
                LoadLauncherBitmap("Images", "Blocks", option.IconName))));
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

using Avalonia.Controls;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadFavoritesShellRightPaneView : UserControl
{
    public DownloadFavoritesShellRightPaneView()
    {
        InitializeComponent();
        ManageTargetButton.IconData = FrontendIconCatalog.SettingsFilledData;
        ManageTargetButton.IconScale = FrontendIconCatalog.SettingsFilledScale;
    }
}

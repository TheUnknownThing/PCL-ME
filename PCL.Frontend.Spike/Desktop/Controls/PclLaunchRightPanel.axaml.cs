using Avalonia.Controls;
using PCL.Frontend.Spike.Icons;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclLaunchRightPanel : UserControl
{
    public PclLaunchRightPanel()
    {
        InitializeComponent();
        // LaunchBannerIcon.Text = FrontendIconCatalog.LaunchBanner.Data;
        DismissLaunchCommunityHintButton.IconData = FrontendIconCatalog.Close.Data;
    }
}

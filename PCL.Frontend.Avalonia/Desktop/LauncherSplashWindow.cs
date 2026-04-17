using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class LauncherSplashWindow : Window
{
    public LauncherSplashWindow(Workflows.FrontendStartupSplashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        FrontendWindowIdentity.Apply(this);
        Content = BuildContent(request);
    }

    private static Control BuildContent(Workflows.FrontendStartupSplashRequest request)
    {
        return new Border
        {
            Padding = new Thickness(2),
            Child = new Image
            {
                Width = 48,
                Height = 48,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new Bitmap(AssetLoader.Open(request.SplashImageAssetUri))
            }
        };
    }
}

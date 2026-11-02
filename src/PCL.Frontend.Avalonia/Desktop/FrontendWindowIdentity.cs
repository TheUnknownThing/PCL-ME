using Avalonia.Controls;
using Avalonia.Platform;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class FrontendWindowIdentity
{
    private static readonly Uri WindowsIconAssetUri = new("avares://PCL.Frontend.Avalonia/Assets/icon.ico");
    private static readonly Uri CrossPlatformIconAssetUri = new("avares://PCL.Frontend.Avalonia/Assets/icon.png");

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Icon = CreateWindowIcon();

        if (OperatingSystem.IsLinux())
        {
            X11Properties.SetWmClass(window, FrontendApplicationIdentity.LinuxWindowClass);
        }
    }

    private static WindowIcon CreateWindowIcon()
    {
        var iconAssetUri = OperatingSystem.IsWindows()
            ? WindowsIconAssetUri
            : CrossPlatformIconAssetUri;
        return new WindowIcon(AssetLoader.Open(iconAssetUri));
    }
}

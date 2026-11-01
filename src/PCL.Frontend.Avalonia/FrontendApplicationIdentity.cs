namespace PCL.Frontend.Avalonia;

internal static class FrontendApplicationIdentity
{
    internal const string DisplayName = "PCL-ME";
    internal const string LinuxDesktopFileId = "org.pcl.me.frontend";
    internal const string LinuxWindowClass = LinuxDesktopFileId;
    internal const string LinuxPackageIconFileName = "icon.png";
    internal const string EmbeddedIconResourceName = "PCL.Frontend.Avalonia.Assets.icon.png";

    internal static readonly string LinuxIconRelativePath = Path.Combine("LauncherAssets", "Images", "icon.png");
    internal static readonly string LinuxDesktopIconRelativePath = Path.Combine(
        "icons",
        "hicolor",
        "512x512",
        "apps",
        $"{LinuxDesktopFileId}.png");
}

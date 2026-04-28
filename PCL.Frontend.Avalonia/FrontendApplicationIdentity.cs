namespace PCL.Frontend.Avalonia;

internal static class FrontendApplicationIdentity
{
    internal const string DisplayName = "PCL-ME";
    internal const string LinuxDesktopFileId = "org.pcl.me.frontend";
    internal const string LinuxWindowClass = LinuxDesktopFileId;
    internal const string LinuxPackageIconFileName = "icon.png";

    internal static readonly string LinuxIconRelativePath = Path.Combine("LauncherAssets", "Images", "icon.png");
}

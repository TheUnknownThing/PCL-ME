namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendLauncherAssetLocator
{
    private static readonly string SourceRootDirectory = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "Plain Craft Launcher 2"));

    private static readonly string PackagedRootDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "LauncherAssets");

    public static string RootDirectory => Directory.Exists(PackagedRootDirectory)
        ? PackagedRootDirectory
        : SourceRootDirectory;

    public static string GetPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine([RootDirectory, .. segments]));
    }
}

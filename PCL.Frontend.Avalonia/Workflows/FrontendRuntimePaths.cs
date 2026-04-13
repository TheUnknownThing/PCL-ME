using PCL.Core.App;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendRuntimePaths(
    string ExecutableDirectory,
    string TempDirectory,
    string DataDirectory,
    string SharedConfigDirectory,
    string SharedConfigPath,
    string LocalConfigPath,
    string LauncherAppDataDirectory)
{
    public string FrontendArtifactDirectory => Path.Combine(DataDirectory, "frontend-artifacts");

    public string FrontendTempDirectory => Path.Combine(TempDirectory, "frontend-artifacts");

    public string LauncherProfileDirectory => SharedConfigDirectory;

    public static FrontendRuntimePaths Resolve(FrontendPlatformAdapter platformAdapter)
    {
        ArgumentNullException.ThrowIfNull(platformAdapter);

        var layout = CreateLayout();
        var localConfigPath = Path.Combine(layout.Data, "config.v1.yml");
        TryMigrateLegacyLocalConfig(layout, localConfigPath);
        return new FrontendRuntimePaths(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory,
            layout.Temp,
            layout.Data,
            layout.SharedData,
            Path.Combine(layout.SharedData, "config.v1.json"),
            localConfigPath,
            platformAdapter.GetLauncherAppDataDirectory());
    }

    private static AppPathLayout CreateLayout()
    {
#if DEBUG
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE_Debug", ".PCLCEDebug", enableDebugOverrides: true);
#else
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE", ".PCLCE", enableDebugOverrides: false);
#endif
    }

    private static void TryMigrateLegacyLocalConfig(AppPathLayout layout, string targetLocalConfigPath)
    {
        var legacyPortableConfigPath = Path.Combine(layout.PortableData, "config.v1.yml");
        if (PathsEqual(targetLocalConfigPath, legacyPortableConfigPath) ||
            File.Exists(targetLocalConfigPath) ||
            !File.Exists(legacyPortableConfigPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetLocalConfigPath)!);
            File.Copy(legacyPortableConfigPath, targetLocalConfigPath);
        }
        catch
        {
            // Best effort only. The frontend will continue with defaults if migration fails.
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }
}

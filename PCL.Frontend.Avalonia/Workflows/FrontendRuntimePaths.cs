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
        return new FrontendRuntimePaths(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory,
            layout.Temp,
            layout.Data,
            layout.SharedData,
            Path.Combine(layout.SharedData, "config.v1.json"),
            Path.Combine(layout.Data, "config.v1.yml"),
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
}

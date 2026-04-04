using PCL.Core.App;

namespace PCL.Frontend.Spike.Workflows;

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

    public static FrontendRuntimePaths Resolve()
    {
        var layout = CreateLayout();
        return new FrontendRuntimePaths(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory,
            layout.Temp,
            layout.Data,
            layout.SharedData,
            Path.Combine(layout.SharedData, "config.v1.json"),
            Path.Combine(layout.Data, "config.v1.yml"),
            GetLauncherAppDataDirectory());
    }

    private static AppPathLayout CreateLayout()
    {
#if DEBUG
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE_Debug", ".PCLCEDebug", enableDebugOverrides: true);
#else
        return new AppPathLayout(SystemAppEnvironment.Current, "PCLCE", ".PCLCE", enableDebugOverrides: false);
#endif
    }

    private static string GetLauncherAppDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "PCL");
    }
}

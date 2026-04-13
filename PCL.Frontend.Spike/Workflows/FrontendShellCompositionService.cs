using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows.Inspection;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendShellCompositionService
{
    public static FrontendShellComposition Compose(SpikeCommandOptions options)
    {
        var replayComposition = FrontendInspectionShellCompositionService.TryComposeReplay(options);
        if (replayComposition is not null)
        {
            return replayComposition;
        }

        var paths = FrontendRuntimePaths.Resolve();
        var sharedConfig = new JsonFileProvider(paths.SharedConfigPath);
        var localConfig = new YamlFileProvider(paths.LocalConfigPath);

        var startupWorkflowRequest = BuildStartupWorkflowRequest(paths, localConfig);
        var mainWindowRequest = BuildMainWindowRequest(paths, sharedConfig, localConfig);
        var mainWindowPlan = LauncherMainWindowStartupWorkflowService.BuildPlan(mainWindowRequest);
        ApplyVersionIsolationMigration(localConfig, mainWindowPlan.VersionIsolationMigration);

        return new FrontendShellComposition(
            startupWorkflowRequest,
            new LauncherStartupConsentRequest(
                mainWindowRequest.SpecialBuildKind,
                mainWindowRequest.IsSpecialBuildHintDisabled,
                mainWindowRequest.HasAcceptedEula,
                mainWindowRequest.IsTelemetryDefault),
            mainWindowPlan.Consent,
            BuildNavigationRequest(paths),
            "Runtime-composed shell inputs",
            $"Config: {paths.SharedConfigPath}");
    }

    private static LauncherStartupWorkflowRequest BuildStartupWorkflowRequest(
        FrontendRuntimePaths paths,
        YamlFileProvider localConfig)
    {
        return new LauncherStartupWorkflowRequest(
            CommandLineArguments: Environment.GetCommandLineArgs()[1..],
            ExecutableDirectory: EnsureTrailingSeparator(paths.ExecutableDirectory),
            TempDirectory: EnsureTrailingSeparator(paths.TempDirectory),
            AppDataDirectory: EnsureTrailingSeparator(paths.LauncherAppDataDirectory),
            IsBetaVersion: false,
            DetectedWindowsVersion: GetHostVersionForStartupChecks(),
            Is64BitOperatingSystem: Environment.Is64BitOperatingSystem,
            ShowStartupLogo: ReadValue(localConfig, "UiLauncherLogo", true));
    }

    private static LauncherMainWindowStartupWorkflowRequest BuildMainWindowRequest(
        FrontendRuntimePaths paths,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        return new LauncherMainWindowStartupWorkflowRequest(
            HasVersionIsolationV2Setting: localConfig.Exists("LaunchArgumentIndieV2"),
            HasLegacyVersionIsolationSetting: localConfig.Exists("LaunchArgumentIndie"),
            LegacyVersionIsolationValue: ReadValue(localConfig, "LaunchArgumentIndie", 0),
            HasWindowHeightSetting: localConfig.Exists("WindowHeight"),
            LegacyVersionIsolationDefaultValue: -1,
            VersionIsolationV2DefaultValue: 0,
            SpecialBuildKind: GetStartupSpecialBuildKind(),
            IsSpecialBuildHintDisabled: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT")),
            HasAcceptedEula: ReadValue(sharedConfig, "SystemEula", false),
            IsTelemetryDefault: !sharedConfig.Exists("SystemTelemetry"),
            CurrentStartupCount: LauncherFrontendRuntimeStateService.ReadStartupCount(
                paths.SharedConfigDirectory,
                paths.SharedConfigPath));
    }

    private static LauncherFrontendNavigationViewRequest BuildNavigationRequest(FrontendRuntimePaths paths)
    {
        var hasGameLogs = File.Exists(Path.Combine(paths.ExecutableDirectory, "PCL", "LatestLaunch.bat")) ||
                          Directory.Exists(Path.Combine(paths.LauncherAppDataDirectory, "Log"));

        return new LauncherFrontendNavigationViewRequest(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            HasRunningTasks: LauncherFrontendRuntimeStateService.HasRunningTasks(),
            HasGameLogs: hasGameLogs);
    }

    private static void ApplyVersionIsolationMigration(
        YamlFileProvider localConfig,
        LauncherStartupVersionIsolationMigrationResult migration)
    {
        if (!migration.ShouldStoreVersionIsolationV2)
        {
            return;
        }

        localConfig.Set("LaunchArgumentIndieV2", migration.VersionIsolationV2Value);
        localConfig.Sync();
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static Version GetHostVersionForStartupChecks()
    {
        var version = Environment.OSVersion.Version;
        if (OperatingSystem.IsWindows())
        {
            return version;
        }

        return new Version(Math.Max(version.Major, 10), Math.Max(version.Minor, 0), Math.Max(version.Build, 17763));
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static LauncherStartupSpecialBuildKind GetStartupSpecialBuildKind()
    {
#if DEBUG
        return LauncherStartupSpecialBuildKind.Debug;
#else
        return LauncherStartupSpecialBuildKind.None;
#endif
    }
}

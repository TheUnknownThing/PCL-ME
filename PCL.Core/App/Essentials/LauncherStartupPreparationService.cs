using System;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupPreparationService
{
    public static LauncherStartupBootstrapResult Prepare(LauncherStartupPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = LauncherStartupEnvironmentWarningService.GetWarnings(
            new LauncherStartupEnvironmentWarningRequest(
                request.ExecutableDirectory,
                request.DetectedWindowsVersion,
                request.Is64BitOperatingSystem));

        return LauncherStartupBootstrapService.Build(
            new LauncherStartupBootstrapRequest(
                request.ExecutableDirectory,
                request.TempDirectory,
                request.AppDataDirectory,
                request.IsBetaVersion,
                warnings));
    }
}

public sealed record LauncherStartupPreparationRequest(
    string ExecutableDirectory,
    string TempDirectory,
    string AppDataDirectory,
    bool IsBetaVersion,
    Version DetectedWindowsVersion,
    bool Is64BitOperatingSystem);

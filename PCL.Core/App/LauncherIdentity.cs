using System;
using System.IO;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class LauncherIdentity
{
    private const string LogModule = "LauncherIdentity";
    private const string LauncherIdFileName = "launcher-id.txt";

    public static string LauncherId { get; } = ResolveLauncherId();

    private static string ResolveLauncherId()
    {
        var persistedPath = GetPersistedLauncherIdPath();
        var plan = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: EnvironmentInterop.GetSecret("LAUNCHER_ID", readEnvDebugOnly: true),
            PersistedLauncherId: TryReadPersistedLauncherId(persistedPath),
            DeviceLauncherId: TryGetWindowsLauncherId(),
            GeneratedLauncherId: Guid.NewGuid().ToString("N")));

        if (plan.ShouldPersist)
        {
            PersistLauncherId(persistedPath, plan.LauncherId);
        }

        return plan.LauncherId;
    }

    private static string? TryGetWindowsLauncherId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return LauncherIdentityResolutionService.NormalizeLauncherId(Identify.LauncherId);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "读取 Windows 设备识别码失败，将使用持久化兜底值");
            return null;
        }
    }

    private static string? TryReadPersistedLauncherId(string persistedPath)
    {
        try
        {
            if (!File.Exists(persistedPath))
            {
                return null;
            }

            return LauncherIdentityResolutionService.NormalizeLauncherId(File.ReadAllText(persistedPath));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, $"读取持久化识别码失败：{persistedPath}");
            return null;
        }
    }

    private static void PersistLauncherId(string persistedPath, string launcherId)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(persistedPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(persistedPath, launcherId);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, $"持久化识别码失败：{persistedPath}");
        }
    }

    private static string GetPersistedLauncherIdPath()
    {
        return Path.Combine(Paths.SharedData, LauncherIdFileName);
    }
}

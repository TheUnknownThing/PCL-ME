using System;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class LauncherIdentity
{
    private const string LogModule = "LauncherIdentity";

    public static string LauncherId { get; } = ResolveLauncherId();

    private static string ResolveLauncherId()
    {
        var persistedPath = LauncherIdentityStorageService.GetPersistedLauncherIdPath(Paths.SharedData);
        var plan = LauncherIdentityRuntimeService.Resolve(new LauncherIdentityRuntimeRequest(
            ExplicitLauncherId: EnvironmentInterop.GetSecret("LAUNCHER_ID", readEnvDebugOnly: true),
            PersistedLauncherId: TryReadPersistedLauncherId(persistedPath),
            ReadDeviceLauncherId: TryGetWindowsLauncherId,
            GenerateLauncherId: () => Guid.NewGuid().ToString("N"),
            PersistLauncherId: launcherId => PersistLauncherId(persistedPath, launcherId)));

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
            return LauncherIdentityResolutionService.NormalizeLauncherId(WindowsDeviceIdentityProvider.GetLauncherId());
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
            return LauncherIdentityResolutionService.NormalizeLauncherId(
                LauncherIdentityStorageService.TryReadPersistedLauncherId(persistedPath));
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
            LauncherIdentityStorageService.PersistLauncherId(persistedPath, launcherId);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, $"持久化识别码失败：{persistedPath}");
        }
    }
}

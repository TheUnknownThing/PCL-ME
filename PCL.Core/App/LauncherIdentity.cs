using System;
using System.IO;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;
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
        var explicitLauncherId = NormalizeLauncherId(EnvironmentInterop.GetSecret("LAUNCHER_ID", readEnvDebugOnly: true));
        if (explicitLauncherId is not null)
        {
            return explicitLauncherId;
        }

        var persistedPath = GetPersistedLauncherIdPath();
        var persistedLauncherId = TryReadPersistedLauncherId(persistedPath);
        if (persistedLauncherId is not null)
        {
            return persistedLauncherId;
        }

        var launcherId = TryGetWindowsLauncherId() ?? CreateGeneratedLauncherId();
        PersistLauncherId(persistedPath, launcherId);
        return launcherId;
    }

    private static string? TryGetWindowsLauncherId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return NormalizeLauncherId(Identify.LauncherId);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "读取 Windows 设备识别码失败，将使用持久化兜底值");
            return null;
        }
    }

    private static string CreateGeneratedLauncherId()
    {
        var generated = NormalizeLauncherId(Guid.NewGuid().ToString("N"));
        return generated ?? "PCL2-CECE-GOOD-2025";
    }

    private static string? TryReadPersistedLauncherId(string persistedPath)
    {
        try
        {
            if (!File.Exists(persistedPath))
            {
                return null;
            }

            return NormalizeLauncherId(File.ReadAllText(persistedPath));
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

    private static string? NormalizeLauncherId(string? launcherId)
    {
        if (string.IsNullOrWhiteSpace(launcherId))
        {
            return null;
        }

        var trimmed = launcherId.Trim().ToUpperInvariant();
        if (trimmed.Length == 19 && trimmed[4] == '-' && trimmed[9] == '-' && trimmed[14] == '-')
        {
            return trimmed;
        }

        var sample = SHA512Provider.Instance.ComputeHash($"PCL-CE|{trimmed}|LauncherId").ToHexString();
        var token = sample.Substring(64, 16).ToUpperInvariant();
        return token
            .Insert(4, "-")
            .Insert(9, "-")
            .Insert(14, "-");
    }
}

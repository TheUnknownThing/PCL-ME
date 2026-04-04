using System;
using System.IO;

namespace PCL.Core.App.Essentials;

public static class LauncherIdentityStorageService
{
    public const string DefaultFileName = "launcher-id.txt";

    public static string GetPersistedLauncherIdPath(string sharedDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        return Path.Combine(sharedDataPath, DefaultFileName);
    }

    public static string? TryReadPersistedLauncherId(string persistedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);

        if (!File.Exists(persistedPath))
        {
            return null;
        }

        return File.ReadAllText(persistedPath);
    }

    public static void PersistLauncherId(string persistedPath, string launcherId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherId);

        var directoryPath = Path.GetDirectoryName(persistedPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(persistedPath, launcherId);
    }
}

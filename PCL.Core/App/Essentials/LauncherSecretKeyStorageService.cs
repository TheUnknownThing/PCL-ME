using System;
using System.IO;
using PCL.Core.Utils;

namespace PCL.Core.App.Essentials;

public static class LauncherSecretKeyStorageService
{
    public const string DefaultFileName = "UserKey.bin";

    public static string GetPersistedKeyPath(string sharedDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        return Path.Combine(sharedDataPath, DefaultFileName);
    }

    public static byte[]? TryReadPersistedKeyEnvelope(string persistedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);

        if (!File.Exists(persistedPath))
        {
            return null;
        }

        return File.ReadAllBytes(persistedPath);
    }

    public static void PersistKeyEnvelope(string persistedPath, byte[] persistedKeyEnvelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);
        ArgumentNullException.ThrowIfNull(persistedKeyEnvelope);

        var directoryPath = Path.GetDirectoryName(persistedPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tmpFile = $"{persistedPath}.tmp{RandomUtils.NextInt(10000, 99999)}";
        using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            fs.Write(persistedKeyEnvelope);
            fs.Flush(true);
        }

        TryApplyPortableFilePermissions(tmpFile);

        File.Move(tmpFile, persistedPath, true);
        TryApplyPortableFilePermissions(persistedPath);
    }

    private static void TryApplyPortableFilePermissions(string filePath)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Ignore best-effort permission hardening failures on non-Windows hosts.
        }
    }
}

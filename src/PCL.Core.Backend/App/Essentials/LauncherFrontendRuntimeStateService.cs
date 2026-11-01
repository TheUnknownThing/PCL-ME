using System;
using System.IO;
using System.Linq;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Tasks;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Essentials;

public static class LauncherFrontendRuntimeStateService
{
    public static bool HasRunningTasks()
    {
        return TaskCenter.Tasks.Any(task => task.State is TaskState.Waiting or TaskState.Running);
    }

    public static int ReadStartupCount(string sharedDataPath, string sharedConfigPath, int fallback = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedConfigPath);

        try
        {
            return ReadProtectedInt(sharedDataPath, sharedConfigPath, "SystemCount", fallback);
        }
        catch
        {
            return fallback;
        }
    }

    public static int ReadStartupCount(string sharedDataPath, JsonFileProvider sharedConfig, int fallback = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        ArgumentNullException.ThrowIfNull(sharedConfig);

        try
        {
            return ReadProtectedInt(sharedDataPath, sharedConfig, "SystemCount", fallback);
        }
        catch
        {
            return fallback;
        }
    }

    public static int ReadProtectedInt(
        string sharedDataPath,
        string sharedConfigPath,
        string key,
        int fallback = 0)
    {
        var plainText = TryReadProtectedString(sharedDataPath, sharedConfigPath, key);
        return int.TryParse(plainText, out var value)
            ? value
            : fallback;
    }

    public static int ReadProtectedInt(
        string sharedDataPath,
        JsonFileProvider sharedConfig,
        string key,
        int fallback = 0)
    {
        var plainText = TryReadProtectedString(sharedDataPath, sharedConfig, key);
        return int.TryParse(plainText, out var value)
            ? value
            : fallback;
    }

    public static string? TryReadProtectedString(
        string sharedDataPath,
        string sharedConfigPath,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedConfigPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var provider = new JsonFileProvider(sharedConfigPath);
            if (!provider.Exists(key))
            {
                return null;
            }

            var encryptedValue = provider.Get<string>(key);
            return TryUnprotectString(sharedDataPath, encryptedValue);
        }
        catch
        {
            return null;
        }
    }

    public static string? TryReadProtectedString(
        string sharedDataPath,
        JsonFileProvider sharedConfig,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);
        ArgumentNullException.ThrowIfNull(sharedConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            if (!sharedConfig.Exists(key))
            {
                return null;
            }

            var encryptedValue = sharedConfig.Get<string>(key);
            return TryUnprotectString(sharedDataPath, encryptedValue);
        }
        catch
        {
            return null;
        }
    }

    public static string? TryUnprotectString(string sharedDataPath, string? encryptedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);

        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            return null;
        }

        try
        {
            var encryptionKey = TryResolveEncryptionKey(sharedDataPath);
            if (encryptionKey is null)
            {
                return null;
            }

            return LauncherDataProtectionService.Unprotect(
                encryptedValue,
                encryptionKey,
                TryResolveLegacyDecryptKey());
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryResolveEncryptionKey(string sharedDataPath)
    {
        var explicitKey = LauncherSecretKeyResolutionService.ParseExplicitKeyOverride(
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
        if (explicitKey is not null)
        {
            return explicitKey;
        }

        var persistedKeyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(sharedDataPath);
        var persistedEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(persistedKeyPath);
        if (persistedEnvelope is null)
        {
            return null;
        }

        var storedKey = LauncherVersionedDataService.Parse(persistedEnvelope);
        return ReadStoredKey(storedKey, persistedKeyPath);
    }

    private static byte[] ReadStoredKey(LauncherVersionedData data, string persistedKeyPath)
    {
        return LauncherStoredKeyEnvelopeService.ReadKey(data, persistedKeyPath);
    }

    private static string? TryResolveLegacyDecryptKey()
    {
        var explicitKey = Environment.GetEnvironmentVariable("PCL_LEGACY_ENCRYPTION_KEY");
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey.Trim();
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return LauncherLegacyIdentityService.DeriveEncryptionKey(WindowsLegacyCpuIdProvider.GetCpuId());
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.App.Essentials;

public static class LauncherStoredKeyEnvelopeService
{
    private static readonly byte[] IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");
    private const string InsecureFileStorageOverrideEnvironmentKey = "PCL_ALLOW_INSECURE_FILE_SECRET_STORAGE";

    public static byte[] ReadKey(LauncherVersionedData data, string persistedPath)
    {
        return ReadKey(data, persistedPath, secretStore: null);
    }

    internal static byte[] ReadKey(
        LauncherVersionedData data,
        string persistedPath,
        ILauncherPlatformSecretStore? secretStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);

        return data.Version switch
        {
            1 => ReadWindowsProtectedKey(data.Data),
            2 => data.Data,
            3 => ReadPlatformStoredKey(data.Data, secretStore),
            _ => throw new NotSupportedException("Unsupported launcher key version.")
        };
    }

    public static LauncherVersionedData CreateStoredKeyEnvelope(byte[] randomKey, string persistedPath)
    {
        return CreateStoredKeyEnvelope(randomKey, persistedPath, secretStore: null);
    }

    internal static LauncherVersionedData CreateStoredKeyEnvelope(
        byte[] randomKey,
        string persistedPath,
        ILauncherPlatformSecretStore? secretStore)
    {
        ArgumentNullException.ThrowIfNull(randomKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);

        if (OperatingSystem.IsWindows())
        {
            return new LauncherVersionedData(
                Version: 1,
                Data: ProtectedData.Protect(randomKey, IdentifyEntropy, DataProtectionScope.CurrentUser));
        }

        var platformSecretStore = secretStore ?? new LauncherProcessPlatformSecretStore();
        if (platformSecretStore.IsSupported)
        {
            try
            {
                var secretId = CreateSecretId(persistedPath);
                platformSecretStore.WriteSecret(secretId, randomKey);
                return new LauncherVersionedData(
                    Version: 3,
                    Data: Encoding.UTF8.GetBytes(secretId));
            }
            catch (InvalidOperationException)
            {
                return new LauncherVersionedData(
                    Version: 2,
                    Data: randomKey);
            }
        }

        if (AllowInsecureFileSecretStorage())
        {
            return new LauncherVersionedData(
                Version: 2,
                Data: randomKey);
        }

        throw new PlatformNotSupportedException(
            "A supported OS-backed secret store is required to persist launcher secrets on this platform. " +
            "Configure PCL_ENCRYPTION_KEY for an explicit key or set PCL_ALLOW_INSECURE_FILE_SECRET_STORAGE=1 " +
            "to opt into the legacy file-based fallback.");
    }

    internal static LauncherVersionedData? TryUpgradeStoredKeyEnvelope(
        LauncherVersionedData data,
        byte[] resolvedKey,
        string persistedPath,
        ILauncherPlatformSecretStore? secretStore = null)
    {
        ArgumentNullException.ThrowIfNull(resolvedKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(persistedPath);

        if (OperatingSystem.IsWindows() || data.Version != 2)
        {
            return null;
        }

        var platformSecretStore = secretStore ?? new LauncherProcessPlatformSecretStore();
        if (!platformSecretStore.IsSupported)
        {
            return null;
        }

        try
        {
            var secretId = CreateSecretId(persistedPath);
            platformSecretStore.WriteSecret(secretId, resolvedKey);
            return new LauncherVersionedData(
                Version: 3,
                Data: Encoding.UTF8.GetBytes(secretId));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static byte[] ReadWindowsProtectedKey(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected launcher keys are only supported on Windows.");
        }

        return ProtectedData.Unprotect(data, IdentifyEntropy, DataProtectionScope.CurrentUser);
    }

    private static byte[] ReadPlatformStoredKey(byte[] data, ILauncherPlatformSecretStore? secretStore)
    {
        var secretId = Encoding.UTF8.GetString(data);
        if (string.IsNullOrWhiteSpace(secretId))
        {
            throw new InvalidOperationException("Stored launcher key reference is empty.");
        }

        var platformSecretStore = secretStore ?? new LauncherProcessPlatformSecretStore();
        return platformSecretStore.ReadSecret(secretId);
    }

    private static string CreateSecretId(string persistedPath)
    {
        return $"pclce:{Convert.ToHexString(PCL.Core.Utils.Hash.SHA256Provider.Instance.ComputeHash(Path.GetFullPath(persistedPath))).ToLowerInvariant()}";
    }

    private static bool AllowInsecureFileSecretStorage()
    {
        var value = Environment.GetEnvironmentVariable(InsecureFileStorageOverrideEnvironmentKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }
}

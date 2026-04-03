using System;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.App.Essentials;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class LauncherDataProtectionRuntime
{
    private static readonly byte[] IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");
    private static readonly Lazy<byte[]> _encryptionKey = new(ResolveEncryptionKey);

    public static (IEncryptionProvider Provider, uint Version) DefaultProvider => LauncherDataProtectionService.DefaultProvider;
    internal static byte[] EncryptionKey => _encryptionKey.Value;

    public static string Protect(string? data)
    {
        return LauncherDataProtectionService.Protect(data, EncryptionKey);
    }

    public static string Unprotect(string? data)
    {
        return LauncherDataProtectionService.Unprotect(data, EncryptionKey, LegacySecretRuntimeService.LegacyDecryptKey);
    }

    private static byte[] ResolveEncryptionKey()
    {
        var keyFile = LauncherSecretKeyStorageService.GetPersistedKeyPath(Paths.SharedData);
        var resolution = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: EnvironmentInterop.GetSecret("ENCRYPTION_KEY", readEnvDebugOnly: true),
            PersistedKeyEnvelope: LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(keyFile),
            ReadPersistedKey: ReadStoredKey,
            ProtectGeneratedKey: CreateStoredKeyEnvelope));

        if (resolution.ShouldPersist && resolution.PersistedKeyEnvelope is not null)
        {
            LauncherSecretKeyStorageService.PersistKeyEnvelope(keyFile, resolution.PersistedKeyEnvelope);
        }

        return resolution.Key;
    }

    private static LauncherVersionedData CreateStoredKeyEnvelope(byte[] randomKey)
    {
        if (OperatingSystem.IsWindows())
        {
            return new LauncherVersionedData(
                Version: 1,
                Data: ProtectedData.Protect(randomKey, IdentifyEntropy, DataProtectionScope.CurrentUser));
        }

        return new LauncherVersionedData(
            Version: 2,
            Data: randomKey);
    }

    private static byte[] ReadStoredKey(LauncherVersionedData data)
    {
        return data.Version switch
        {
            1 => ProtectedData.Unprotect(data.Data, IdentifyEntropy, DataProtectionScope.CurrentUser),
            2 => data.Data,
            _ => throw new NotSupportedException("Unsupported key version")
        };
    }
}

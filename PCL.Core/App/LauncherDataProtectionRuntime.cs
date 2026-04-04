using System;
using PCL.Core.App.Essentials;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class LauncherDataProtectionRuntime
{
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
            ReadPersistedKey: LauncherStoredKeyEnvelopeService.ReadKey,
            ProtectGeneratedKey: LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope));

        if (resolution.ShouldPersist && resolution.PersistedKeyEnvelope is not null)
        {
            LauncherSecretKeyStorageService.PersistKeyEnvelope(keyFile, resolution.PersistedKeyEnvelope);
        }

        return resolution.Key;
    }
}

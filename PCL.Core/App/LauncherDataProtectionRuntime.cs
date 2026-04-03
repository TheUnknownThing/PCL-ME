using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.App.Essentials;
using PCL.Core.Utils;
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
        return LauncherDataProtectionService.Unprotect(data, EncryptionKey, LegacySecretKeyProvider.LegacyDecryptKey);
    }

    private static byte[] ResolveEncryptionKey()
    {
        var keyFile = Path.Combine(Paths.SharedData, "UserKey.bin");
        var resolution = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: EnvironmentInterop.GetSecret("ENCRYPTION_KEY", readEnvDebugOnly: true),
            PersistedKeyEnvelope: File.Exists(keyFile) ? File.ReadAllBytes(keyFile) : null,
            ReadPersistedKey: ReadStoredKey,
            ProtectGeneratedKey: CreateStoredKeyEnvelope));

        if (resolution.ShouldPersist && resolution.PersistedKeyEnvelope is not null)
        {
            var tmpFile = $"{keyFile}.tmp{RandomUtils.NextInt(10000, 99999)}";
            using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Write(resolution.PersistedKeyEnvelope);
                fs.Flush(true);
            }

            TryApplyPortableKeyFilePermissions(tmpFile);

            File.Move(tmpFile, keyFile, true);
            TryApplyPortableKeyFilePermissions(keyFile);
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

    private static void TryApplyPortableKeyFilePermissions(string filePath)
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

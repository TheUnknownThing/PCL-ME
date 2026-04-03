using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.IO;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.Utils.Secret;

public static class EncryptHelper
{
    public static (IEncryptionProvider Provider, uint Version) DefaultProvider => _DefaultProvider.Value;
    private static readonly Lazy<(IEncryptionProvider Provider, uint Version)> _DefaultProvider = new(_SelectBestEncryption);

    private static (IEncryptionProvider Provider, uint Version) _SelectBestEncryption()
    {
        var aesHardwareSupport = System.Runtime.Intrinsics.X86.Aes.IsSupported ||
                                 System.Runtime.Intrinsics.Arm.Aes.IsSupported;
        if (aesHardwareSupport && AesGcmProvider.Instance.IsSupported) return (AesGcmProvider.Instance, 2);
        if (ChaCha20Poly1305Provider.Instance.IsSupported) return (ChaCha20Poly1305Provider.Instance, 1);
        return (ChaCha20SoftwareProvider.Instance, 0);
    }

    public static string SecretEncrypt(string? data)
    {
        if (data.IsNullOrEmpty()) return string.Empty;
        var rawData = Encoding.UTF8.GetBytes(data);

        return Convert.ToBase64String(LauncherVersionedDataService.Serialize(new LauncherVersionedData(
            Version: DefaultProvider.Version,
            Data: DefaultProvider.Provider.Encrypt(rawData, EncryptionKey))));
    }

    public static string SecretDecrypt(string? data)
    {
        if (data.IsNullOrEmpty()) return string.Empty;
        var rawData = Convert.FromBase64String(data);
        Exception? decryptError;
        if (LauncherVersionedDataService.IsValid(rawData))
        {
            try
            {
                var encryptionData = LauncherVersionedDataService.Parse(rawData);
                IEncryptionProvider provider = encryptionData.Version switch
                {
                    0 => ChaCha20SoftwareProvider.Instance,
                    1 => ChaCha20Poly1305Provider.Instance,
                    2 => AesGcmProvider.Instance,
                    _ => throw new NotSupportedException("Unsupported encryption version")
                };
                var decryptedData = provider.Decrypt(encryptionData.Data, EncryptionKey);
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex) { decryptError = ex; }
        }
        else
        {
            decryptError = TryDecryptLegacyData(rawData, out var legacyPlainText);
            if (legacyPlainText is not null)
            {
                return legacyPlainText;
            }
        }

        throw new Exception($"Unknown Encryption data, the data may broken", decryptError);
    }

    #region "密钥存储和获取"

    private static readonly byte[] _IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");
    internal static byte[] EncryptionKey { get => _EncryptionKey.Value; }
    private static readonly Lazy<byte[]> _EncryptionKey = new(_GetKey);

    private static byte[] _GetKey()
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
                Data: ProtectedData.Protect(randomKey, _IdentifyEntropy, DataProtectionScope.CurrentUser));
        }

        return new LauncherVersionedData(
            Version: 2,
            Data: randomKey);
    }

    private static byte[] ReadStoredKey(LauncherVersionedData data)
    {
        return data.Version switch
        {
            1 => ProtectedData.Unprotect(data.Data, _IdentifyEntropy, DataProtectionScope.CurrentUser),
            2 => data.Data,
            _ => throw new NotSupportedException("Unsupported key version")
        };
    }

    private static Exception? TryDecryptLegacyData(byte[] rawData, out string? plainText)
    {
        plainText = null;
        var legacyKey = LegacySecretKeyProvider.LegacyDecryptKey;
        if (legacyKey.IsNullOrEmpty())
        {
            return new InvalidOperationException("旧版密文缺少可用的兼容解密密钥。可通过 PCL_LEGACY_ENCRYPTION_KEY 提供旧版密钥。");
        }

        try
        {
#pragma warning disable CS0612,CS0618 // Type or member is obsolete
            var decryptedData = AesCbcProvider.Instance.Decrypt(rawData, Encoding.UTF8.GetBytes(legacyKey!));
#pragma warning restore CS0612,CS0618 // Type or member is obsolete
            plainText = Encoding.UTF8.GetString(decryptedData);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
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

    #endregion
}

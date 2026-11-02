using System;
using System.Text;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Essentials;

public static class LauncherDataProtectionService
{
    private static readonly Lazy<(IEncryptionProvider Provider, uint Version)> _defaultProvider = new(SelectBestEncryption);

    public static (IEncryptionProvider Provider, uint Version) DefaultProvider => _defaultProvider.Value;

    public static string Protect(string? data, byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        if (data.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var rawData = Encoding.UTF8.GetBytes(data);
        return Convert.ToBase64String(LauncherVersionedDataService.Serialize(new LauncherVersionedData(
            Version: DefaultProvider.Version,
            Data: DefaultProvider.Provider.Encrypt(rawData, encryptionKey))));
    }

    public static string Unprotect(string? data, byte[] encryptionKey, string? legacyDecryptKey = null)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        if (data.IsNullOrEmpty())
        {
            return string.Empty;
        }

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
                var decryptedData = provider.Decrypt(encryptionData.Data, encryptionKey);
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex)
            {
                decryptError = ex;
            }
        }
        else
        {
            decryptError = TryDecryptLegacyData(rawData, legacyDecryptKey, out var legacyPlainText);
            if (legacyPlainText is not null)
            {
                return legacyPlainText;
            }
        }

        throw new Exception("Unknown encryption data; the payload may be corrupted.", decryptError);
    }

    private static (IEncryptionProvider Provider, uint Version) SelectBestEncryption()
    {
        var aesHardwareSupport = System.Runtime.Intrinsics.X86.Aes.IsSupported ||
                                 System.Runtime.Intrinsics.Arm.Aes.IsSupported;
        if (aesHardwareSupport && AesGcmProvider.Instance.IsSupported)
        {
            return (AesGcmProvider.Instance, 2);
        }

        if (ChaCha20Poly1305Provider.Instance.IsSupported)
        {
            return (ChaCha20Poly1305Provider.Instance, 1);
        }

        return (ChaCha20SoftwareProvider.Instance, 0);
    }

    private static Exception? TryDecryptLegacyData(byte[] rawData, string? legacyDecryptKey, out string? plainText)
    {
        plainText = null;
        if (legacyDecryptKey.IsNullOrEmpty())
        {
            return new InvalidOperationException("The legacy ciphertext has no compatible decryption key. Provide the legacy key through PCL_LEGACY_ENCRYPTION_KEY.");
        }

        try
        {
#pragma warning disable CS0612,CS0618
            var decryptedData = AesCbcProvider.Instance.Decrypt(rawData, Encoding.UTF8.GetBytes(legacyDecryptKey!));
#pragma warning restore CS0612,CS0618
            plainText = Encoding.UTF8.GetString(decryptedData);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}

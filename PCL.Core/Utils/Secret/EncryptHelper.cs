using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Utils.Encryption;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;
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

        return Convert.ToBase64String(EncryptionData.ToBytes(new EncryptionData
            { Version = DefaultProvider.Version, Data = DefaultProvider.Provider.Encrypt(rawData, EncryptionKey) }));
    }

    public static string SecretDecrypt(string? data)
    {
        if (data.IsNullOrEmpty()) return string.Empty;
        var rawData = Convert.FromBase64String(data);
        Exception? decryptError;
        if (EncryptionData.IsValid(rawData))
        {
            try
            {
                var encryptionData = EncryptionData.FromBytes(rawData);
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
            try
            {
#pragma warning disable CS0612,CS0618 // Type or member is obsolete
                var decryptedData = AesCbcProvider.Instance.Decrypt(rawData, Encoding.UTF8.GetBytes(IdentifyOld.EncryptKey));
#pragma warning restore CS0612,CS0618 // Type or member is obsolete
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (Exception ex) { decryptError = ex; }
        }

        throw new Exception($"Unknown Encryption data, the data may broken", decryptError);
    }

    #region "加密存储信息数据"


    public struct EncryptionData
    {
        public uint Version;
        public byte[] Data;

        private const uint MagicNumber = 0x454E4321;

        public static EncryptionData FromBase64(string base64)
        {
            return FromBytes(Convert.FromBase64String(base64));
        }

        public static EncryptionData FromBytes(ReadOnlySpan<byte> bytes)
        {
            // 0 - 4  MagicNumber |  4 - 8 version || 8 - 12 bytes rData length | n bytes rData
            if (bytes.Length < 12)
                throw new ArgumentException("No enough data for EncryptionData", nameof(bytes));

            if (BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]) != MagicNumber)
                throw new ArgumentException("Unknown data for EncryptionData", nameof(bytes));

            var dataLength = BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]);
            if (dataLength > bytes.Length - 12)
                throw new ArgumentException("No enough data for EncryptionData", nameof(bytes));
            if (dataLength < 0)
                throw new ArgumentException("Invalid data length for EncryptionData", nameof(bytes));

            var rData = bytes[12..(12 + dataLength)];

            return new EncryptionData
            {
                Version = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..8]),
                Data = rData.ToArray()
            };
        }

        public static byte[] ToBytes(EncryptionData encryptionData)
        {
            var length = 12 + encryptionData.Data.Length;
            var bytes = new byte[length];
            var bytesSpan = bytes.AsSpan();
            BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[..4], MagicNumber);
            BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[4..8], encryptionData.Version);
            BinaryPrimitives.WriteInt32BigEndian(bytesSpan[8..12], encryptionData.Data.Length);
            encryptionData.Data.CopyTo(bytesSpan[12..]);

            return bytes;
        }

        public static bool IsValid(ReadOnlySpan<byte> data)
        {
            try
            {
                return data.Length >= 12 && BinaryPrimitives.ReadUInt32BigEndian(data[..4]) == MagicNumber;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region "密钥存储和获取"

    private static readonly byte[] _IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");
    internal static byte[] EncryptionKey { get => _EncryptionKey.Value; }
    private static readonly Lazy<byte[]> _EncryptionKey = new(_GetKey);

    private static byte[] _GetKey()
    {
        var explicitKey = TryGetEnvironmentKeyOverride();
        if (explicitKey is not null) return explicitKey;

        var keyFile = Path.Combine(Paths.SharedData, "UserKey.bin");
        if (File.Exists(keyFile))
        {
            var buf = File.ReadAllBytes(keyFile);
            var data = EncryptionData.FromBytes(buf);
            return data.Version switch
                {
                1 => ProtectedData.Unprotect(data.Data, _IdentifyEntropy, DataProtectionScope.CurrentUser),
                2 => data.Data,
                _ => throw new NotSupportedException("Unsupported key version")
            };
        }
        else
        {
            var randomKey = new byte[32];
            RandomNumberGenerator.Fill(randomKey);
            var storeData = EncryptionData.ToBytes(CreateStoredKeyData(randomKey));

            var tmpFile = $"{keyFile}.tmp{RandomUtils.NextInt(10000, 99999)}";
            using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Write(storeData);
                fs.Flush(true);
            }

            TryApplyPortableKeyFilePermissions(tmpFile);

            File.Move(tmpFile, keyFile, true);
            TryApplyPortableKeyFilePermissions(keyFile);

            return randomKey;
        }
    }

    private static EncryptionData CreateStoredKeyData(byte[] randomKey)
    {
        if (OperatingSystem.IsWindows())
        {
            return new EncryptionData
            {
                Version = 1,
                Data = ProtectedData.Protect(randomKey, _IdentifyEntropy, DataProtectionScope.CurrentUser)
            };
        }

        return new EncryptionData
        {
            Version = 2,
            Data = randomKey
        };
    }

    private static byte[]? TryGetEnvironmentKeyOverride()
    {
        var rawValue = EnvironmentInterop.GetSecret("ENCRYPTION_KEY", readEnvDebugOnly: true);
        if (rawValue.IsNullOrEmpty()) return null;

        var normalized = rawValue!.Trim();
        try
        {
            if (normalized.Length == 64 && normalized.All(Uri.IsHexDigit))
            {
                return Convert.FromHexString(normalized);
            }

            return SHA256Provider.Instance.ComputeHash(normalized);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("环境变量 PCL_ENCRYPTION_KEY 无法转换为有效密钥。", ex);
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

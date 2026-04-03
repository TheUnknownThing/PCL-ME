using System;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL.Core.Utils.Secret;

internal static class LegacySecretKeyProvider
{
    private const string LogModule = "Secret";

    public static string? LegacyDecryptKey => _LegacyDecryptKey.Value;
    private static readonly Lazy<string?> _LegacyDecryptKey = new(ResolveLegacyDecryptKey);

    private static string? ResolveLegacyDecryptKey()
    {
        var environmentKey = TryReadEnvironmentOverride();
        if (!string.IsNullOrWhiteSpace(environmentKey))
        {
            return environmentKey;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var legacyCpuId = TryReadLegacyDeviceSeed();
            return LauncherLegacyIdentityService.DeriveEncryptionKey(legacyCpuId);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "无法解析旧版密钥，将跳过旧版密文兼容解密。");
            return null;
        }
    }

    private static string? TryReadLegacyDeviceSeed()
    {
        try
        {
#pragma warning disable CS0612,CS0618 // Type or member is obsolete
            return IdentifyOld.CpuId;
#pragma warning restore CS0612,CS0618 // Type or member is obsolete
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "读取旧版设备识别种子失败，将使用兼容默认密钥。");
            return null;
        }
    }

    private static string? TryReadEnvironmentOverride()
    {
        var rawValue = EnvironmentInterop.GetSecret("LEGACY_ENCRYPTION_KEY", readEnvDebugOnly: true);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return rawValue!.Trim();
    }
}

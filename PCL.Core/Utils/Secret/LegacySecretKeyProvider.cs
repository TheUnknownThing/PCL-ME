using System;
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
#pragma warning disable CS0612,CS0618 // Type or member is obsolete
            return IdentifyOld.EncryptKey;
#pragma warning restore CS0612,CS0618 // Type or member is obsolete
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "无法解析旧版密钥，将跳过旧版密文兼容解密。");
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

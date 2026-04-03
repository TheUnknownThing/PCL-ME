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
        try
        {
            var plan = LauncherLegacySecretResolutionService.Resolve(new LauncherLegacySecretResolutionRequest(
                ExplicitLegacyDecryptKey: TryReadEnvironmentOverride(),
                LegacyDeviceSeed: TryReadLegacyDeviceSeed()));
            return plan.DecryptKey;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, LogModule, "无法解析旧版密钥，将跳过旧版密文兼容解密。");
            return null;
        }
    }

    private static string? TryReadLegacyDeviceSeed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return WindowsLegacyCpuIdProvider.GetCpuId();
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

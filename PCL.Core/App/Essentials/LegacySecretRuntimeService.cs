using System;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Essentials;

internal static class LegacySecretRuntimeService
{
    private const string LogModule = "Secret";
    private static readonly Lazy<string?> _legacyDecryptKey = new(ResolveLegacyDecryptKey);

    public static string? LegacyDecryptKey => _legacyDecryptKey.Value;

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

        return rawValue.Trim();
    }
}

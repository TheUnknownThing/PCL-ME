using System;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;

namespace PCL.Core.Utils.Secret;

[Obsolete("Use PCL.Core.Utils.Secret.Identify instead")]
public static class IdentifyOld
{
    private static readonly Lazy<string?> _LazyCpuId = new(WindowsLegacyCpuIdProvider.GetCpuId);

    private static readonly Lazy<string> _LazyRawCode = new(() => LauncherLegacyIdentityService.DeriveRawCode(CpuId));

    private static readonly Lazy<string> _LaunchId = new(_GetLaunchId);

    private static readonly Lazy<string> _LazyEncryptKey = new(() => LauncherLegacyIdentityService.DeriveEncryptionKey(CpuId));

    public static string GetGuid() => Guid.NewGuid().ToString();
    [Obsolete]
    public static string? CpuId => _LazyCpuId.Value;
    [Obsolete]
    public static string RawCode => _LazyRawCode.Value;
    [Obsolete]
    public static string LaunchId => _LaunchId.Value;
    [Obsolete]
    public static string EncryptKey => _LazyEncryptKey.Value;

    public static string GetMachineId(string randomId)
    {
        return LauncherLegacyIdentityService.DeriveMachineId(randomId, CpuId);
    }

    private static string _GetLaunchId()
    {
        try
        {
            if (string.IsNullOrEmpty(States.System.LaunchUuid)) States.System.LaunchUuid = GetGuid();
            return LauncherLegacyIdentityService.DeriveLauncherId(States.System.LaunchUuid, CpuId);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Identify", "无法获取短识别码");
            return "PCL2-CECE-GOOD-2025";
        }
    }
}

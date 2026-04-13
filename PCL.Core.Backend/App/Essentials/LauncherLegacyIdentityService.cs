using System;
using PCL.Core.Utils.Hash;

namespace PCL.Core.App.Essentials;

public static class LauncherLegacyIdentityService
{
    public const string DefaultRawCode = "B09675A9351CBD1FD568056781FE3966DD936CC9B94E51AB5CF67EEB7E74C075";

    public static string DeriveRawCode(string? deviceSeed)
    {
        var normalizedDeviceSeed = NormalizeDeviceSeed(deviceSeed);
        return normalizedDeviceSeed is null
            ? DefaultRawCode
            : Convert.ToHexString(SHA256Provider.Instance.ComputeHash(normalizedDeviceSeed)).ToUpperInvariant();
    }

    public static string DeriveEncryptionKey(string? deviceSeed)
    {
        return Convert.ToHexString(SHA512Provider.Instance.ComputeHash(DeriveRawCode(deviceSeed)))
            .Substring(4, 32)
            .ToUpperInvariant();
    }

    public static string DeriveMachineId(string randomId, string? deviceSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(randomId);

        var normalizedDeviceSeed = NormalizeDeviceSeed(deviceSeed) ?? string.Empty;
        return Convert.ToHexString(SHA512Provider.Instance.ComputeHash($"{randomId}|{normalizedDeviceSeed}"))
            .ToUpperInvariant();
    }

    public static string DeriveLauncherId(string randomId, string? deviceSeed)
    {
        var machineId = DeriveMachineId(randomId, deviceSeed);
        return machineId
            .Substring(6, 16)
            .Insert(4, "-")
            .Insert(9, "-")
            .Insert(14, "-");
    }

    private static string? NormalizeDeviceSeed(string? deviceSeed)
    {
        return string.IsNullOrWhiteSpace(deviceSeed) ? null : deviceSeed.Trim();
    }
}

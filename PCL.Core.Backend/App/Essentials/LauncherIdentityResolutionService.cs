using System;
using PCL.Core.Utils.Hash;

namespace PCL.Core.App.Essentials;

public static class LauncherIdentityResolutionService
{
    private const string DefaultLauncherId = "PCL-MEDE-FAULT-2026";

    public static LauncherIdentityResolutionPlan Resolve(LauncherIdentityResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var explicitLauncherId = NormalizeLauncherId(request.ExplicitLauncherId);
        if (explicitLauncherId is not null)
        {
            return new LauncherIdentityResolutionPlan(
                explicitLauncherId,
                LauncherIdentityResolutionSource.EnvironmentOverride,
                ShouldPersist: false);
        }

        var persistedLauncherId = NormalizeLauncherId(request.PersistedLauncherId);
        if (persistedLauncherId is not null)
        {
            return new LauncherIdentityResolutionPlan(
                persistedLauncherId,
                LauncherIdentityResolutionSource.PersistedFile,
                ShouldPersist: false);
        }

        var deviceLauncherId = NormalizeLauncherId(request.DeviceLauncherId);
        if (deviceLauncherId is not null)
        {
            return new LauncherIdentityResolutionPlan(
                deviceLauncherId,
                LauncherIdentityResolutionSource.DeviceIdentity,
                ShouldPersist: true);
        }

        var generatedLauncherId = NormalizeLauncherId(request.GeneratedLauncherId);
        return new LauncherIdentityResolutionPlan(
            generatedLauncherId ?? DefaultLauncherId,
            LauncherIdentityResolutionSource.GeneratedFallback,
            ShouldPersist: true);
    }

    public static string? NormalizeLauncherId(string? launcherId)
    {
        if (string.IsNullOrWhiteSpace(launcherId))
        {
            return null;
        }

        var trimmed = launcherId.Trim().ToUpperInvariant();
        if (trimmed.Length == 19 && trimmed[4] == '-' && trimmed[9] == '-' && trimmed[14] == '-')
        {
            return trimmed;
        }

        var sample = Convert.ToHexString(SHA512Provider.Instance.ComputeHash($"PCL-ME|{trimmed}|LauncherId")).ToLowerInvariant();
        var token = sample.Substring(64, 16).ToUpperInvariant();
        return token
            .Insert(4, "-")
            .Insert(9, "-")
            .Insert(14, "-");
    }
}

public sealed record LauncherIdentityResolutionRequest(
    string? ExplicitLauncherId,
    string? PersistedLauncherId,
    string? DeviceLauncherId,
    string? GeneratedLauncherId);

public sealed record LauncherIdentityResolutionPlan(
    string LauncherId,
    LauncherIdentityResolutionSource Source,
    bool ShouldPersist);

public enum LauncherIdentityResolutionSource
{
    EnvironmentOverride = 0,
    PersistedFile = 1,
    DeviceIdentity = 2,
    GeneratedFallback = 3
}

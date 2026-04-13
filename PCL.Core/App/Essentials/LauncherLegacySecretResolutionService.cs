using System;

namespace PCL.Core.App.Essentials;

public static class LauncherLegacySecretResolutionService
{
    public static LauncherLegacySecretResolutionPlan Resolve(LauncherLegacySecretResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.ExplicitLegacyDecryptKey))
        {
            return new LauncherLegacySecretResolutionPlan(
                request.ExplicitLegacyDecryptKey.Trim(),
                LauncherLegacySecretSource.EnvironmentOverride);
        }

        if (string.IsNullOrWhiteSpace(request.LegacyDeviceSeed))
        {
            return new LauncherLegacySecretResolutionPlan(
                null,
                LauncherLegacySecretSource.Unavailable);
        }

        return new LauncherLegacySecretResolutionPlan(
            LauncherLegacyIdentityService.DeriveEncryptionKey(request.LegacyDeviceSeed),
            LauncherLegacySecretSource.DeviceSeedDerived);
    }
}

public sealed record LauncherLegacySecretResolutionRequest(
    string? ExplicitLegacyDecryptKey,
    string? LegacyDeviceSeed);

public sealed record LauncherLegacySecretResolutionPlan(
    string? DecryptKey,
    LauncherLegacySecretSource Source);

public enum LauncherLegacySecretSource
{
    Unavailable = 0,
    EnvironmentOverride = 1,
    DeviceSeedDerived = 2
}

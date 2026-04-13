using System;

namespace PCL.Core.App.Essentials;

public static class LauncherIdentityRuntimeService
{
    public static LauncherIdentityRuntimePlan Resolve(LauncherIdentityRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var explicitLauncherId = LauncherIdentityResolutionService.NormalizeLauncherId(request.ExplicitLauncherId);
        if (explicitLauncherId is not null)
        {
            return new LauncherIdentityRuntimePlan(
                explicitLauncherId,
                LauncherIdentityResolutionSource.EnvironmentOverride,
                PersistenceRequested: false);
        }

        var persistedLauncherId = LauncherIdentityResolutionService.NormalizeLauncherId(request.PersistedLauncherId);
        if (persistedLauncherId is not null)
        {
            return new LauncherIdentityRuntimePlan(
                persistedLauncherId,
                LauncherIdentityResolutionSource.PersistedFile,
                PersistenceRequested: false);
        }

        var deviceLauncherId = LauncherIdentityResolutionService.NormalizeLauncherId(request.ReadDeviceLauncherId?.Invoke());
        if (deviceLauncherId is not null)
        {
            request.PersistLauncherId?.Invoke(deviceLauncherId);
            return new LauncherIdentityRuntimePlan(
                deviceLauncherId,
                LauncherIdentityResolutionSource.DeviceIdentity,
                PersistenceRequested: true);
        }

        var generatedPlan = LauncherIdentityResolutionService.Resolve(new LauncherIdentityResolutionRequest(
            ExplicitLauncherId: null,
            PersistedLauncherId: null,
            DeviceLauncherId: null,
            GeneratedLauncherId: request.GenerateLauncherId?.Invoke()));

        if (generatedPlan.ShouldPersist)
        {
            request.PersistLauncherId?.Invoke(generatedPlan.LauncherId);
        }

        return new LauncherIdentityRuntimePlan(
            generatedPlan.LauncherId,
            generatedPlan.Source,
            PersistenceRequested: generatedPlan.ShouldPersist);
    }
}

public sealed record LauncherIdentityRuntimeRequest(
    string? ExplicitLauncherId,
    string? PersistedLauncherId,
    Func<string?>? ReadDeviceLauncherId,
    Func<string?>? GenerateLauncherId,
    Action<string>? PersistLauncherId);

public sealed record LauncherIdentityRuntimePlan(
    string LauncherId,
    LauncherIdentityResolutionSource Source,
    bool PersistenceRequested);

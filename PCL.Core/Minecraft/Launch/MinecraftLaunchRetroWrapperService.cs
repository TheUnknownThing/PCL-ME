using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchRetroWrapperService
{
    private static readonly DateTime RetroWrapperReleaseThreshold = new(2013, 6, 25);

    public static bool ShouldUse(MinecraftLaunchRetroWrapperRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request.ReleaseTime >= RetroWrapperReleaseThreshold && request.GameVersionDrop == 99) ||
               (request.GameVersionDrop < 60 && request.GameVersionDrop != 99) &&
               !request.DisableGlobalRetroWrapper &&
               !request.DisableInstanceRetroWrapper;
    }
}

public sealed record MinecraftLaunchRetroWrapperRequest(
    DateTime ReleaseTime,
    int GameVersionDrop,
    bool DisableGlobalRetroWrapper,
    bool DisableInstanceRetroWrapper);

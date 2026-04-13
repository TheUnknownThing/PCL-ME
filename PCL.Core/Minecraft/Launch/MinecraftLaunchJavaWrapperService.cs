using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaWrapperService
{
    private const int JavaWrapperFixIncludedMajorVersion = 19;

    public static bool ShouldUse(MinecraftLaunchJavaWrapperRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.IsRequested &&
               request.IsWindowsPlatform &&
               request.JavaMajorVersion < JavaWrapperFixIncludedMajorVersion &&
               !string.IsNullOrWhiteSpace(request.JavaWrapperTempDirectory) &&
               !string.IsNullOrWhiteSpace(request.JavaWrapperPath);
    }
}

public sealed record MinecraftLaunchJavaWrapperRequest(
    bool IsRequested,
    bool IsWindowsPlatform,
    int JavaMajorVersion,
    string? JavaWrapperTempDirectory,
    string? JavaWrapperPath);

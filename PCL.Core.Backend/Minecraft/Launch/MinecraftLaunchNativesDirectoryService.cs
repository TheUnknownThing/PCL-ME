using System;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchNativesDirectoryService
{
    public static string ResolvePath(MinecraftLaunchNativesDirectoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PreferInstanceDirectory || IsAscii(request.PreferredInstanceDirectory))
        {
            return request.PreferredInstanceDirectory;
        }

        if (IsAscii(request.AppDataNativesDirectory))
        {
            return request.AppDataNativesDirectory;
        }

        return request.FinalFallbackDirectory;
    }

    private static bool IsAscii(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.All(character => character <= sbyte.MaxValue);
    }
}

public sealed record MinecraftLaunchNativesDirectoryRequest(
    string PreferredInstanceDirectory,
    bool PreferInstanceDirectory,
    string AppDataNativesDirectory,
    string FinalFallbackDirectory);

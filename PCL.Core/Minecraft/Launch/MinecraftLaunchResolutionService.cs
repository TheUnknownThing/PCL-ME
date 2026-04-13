using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchResolutionService
{
    private const int DefaultWidth = 854;
    private const int DefaultHeight = 480;

    public static MinecraftLaunchResolutionPlan BuildPlan(MinecraftLaunchResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dpiScale = request.DpiScale > 0 ? request.DpiScale : 1D;
        var width = DefaultWidth;
        var height = DefaultHeight;

        switch (request.WindowMode)
        {
            case 2:
                width = (int)Math.Round(request.LauncherWindowWidth ?? DefaultWidth);
                height = (int)Math.Round((request.LauncherWindowHeight ?? DefaultHeight) - request.LauncherTitleBarHeight);
                break;
            case 3:
                width = Math.Max(100, request.CustomWidth);
                height = Math.Max(100, request.CustomHeight);
                break;
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var shouldApplyLegacyDpiFix =
            request.GameVersionDrop <= 120 &&
            request.JavaMajorVersion <= 8 &&
            request.JavaRevision >= 200 &&
            request.JavaRevision <= 321 &&
            !request.HasOptiFine &&
            !request.HasForge;

        if (shouldApplyLegacyDpiFix)
        {
            width = Math.Max(1, (int)Math.Round(width / dpiScale));
            height = Math.Max(1, (int)Math.Round(height / dpiScale));
        }

        return new MinecraftLaunchResolutionPlan(
            width,
            height,
            shouldApplyLegacyDpiFix,
            shouldApplyLegacyDpiFix ? $"已应用窗口大小过大修复（{request.JavaRevision}）" : null);
    }
}

public sealed record MinecraftLaunchResolutionRequest(
    int WindowMode,
    double? LauncherWindowWidth,
    double? LauncherWindowHeight,
    double LauncherTitleBarHeight,
    int CustomWidth,
    int CustomHeight,
    int GameVersionDrop,
    int JavaMajorVersion,
    int JavaRevision,
    bool HasOptiFine,
    bool HasForge,
    double DpiScale);

public sealed record MinecraftLaunchResolutionPlan(
    int Width,
    int Height,
    bool AppliedLegacyJavaDpiFix,
    string? LogMessage);

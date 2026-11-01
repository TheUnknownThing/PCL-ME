using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchPrerunWorkflowService
{
    public static MinecraftLaunchPrerunWorkflowPlan BuildPlan(MinecraftLaunchPrerunWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PrimaryOptionsFilePath))
        {
            throw new ArgumentException("The primary options.txt path cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.YosbrOptionsFilePath))
        {
            throw new ArgumentException("The Yosbr options.txt path cannot be empty.", nameof(request));
        }

        if (request.IsMicrosoftLogin && string.IsNullOrWhiteSpace(request.LauncherProfilesPath))
        {
            throw new ArgumentException("The launcher_profiles.json path cannot be empty when using Microsoft sign-in.", nameof(request));
        }

        var launcherProfilesWorkflow = MinecraftLaunchLauncherProfilesWorkflowService.BuildPlan(
            new MinecraftLaunchLauncherProfilesWorkflowRequest(
                request.IsMicrosoftLogin,
                request.ExistingLauncherProfilesJson,
                request.UserName ?? string.Empty,
                request.ClientToken,
                request.LauncherProfilesDefaultTimestamp));
        var optionsPlan = MinecraftLaunchOptionsFileService.BuildPlan(
            new MinecraftLaunchOptionsSyncRequest(
                request.AutoChangeLanguage,
                request.PrimaryOptionsFileExists,
                request.PrimaryCurrentLanguage,
                request.YosbrOptionsFileExists,
                request.HasExistingSaves,
                request.ReleaseTime,
                request.LaunchWindowType));

        var targetOptionsFilePath = optionsPlan.TargetKind == MinecraftLaunchOptionsFileTargetKind.Yosbr
            ? request.YosbrOptionsFilePath
            : request.PrimaryOptionsFilePath;

        return new MinecraftLaunchPrerunWorkflowPlan(
            new MinecraftLaunchLauncherProfilesPrerunPlan(
                request.IsMicrosoftLogin,
                request.LauncherProfilesPath,
                launcherProfilesWorkflow),
            new MinecraftLaunchOptionsPrerunPlan(
                targetOptionsFilePath,
                optionsPlan));
    }

    public static MinecraftLaunchGpuPreferenceFailurePlan BuildGpuPreferenceFailurePlan(MinecraftLaunchGpuPreferenceFailureRequest request)
    {
        return MinecraftLaunchGpuPreferenceWorkflowService.BuildFailurePlan(request);
    }
}

public sealed record MinecraftLaunchPrerunWorkflowRequest(
    string? LauncherProfilesPath,
    bool IsMicrosoftLogin,
    string? ExistingLauncherProfilesJson,
    string? UserName,
    string? ClientToken,
    DateTime LauncherProfilesDefaultTimestamp,
    string PrimaryOptionsFilePath,
    bool PrimaryOptionsFileExists,
    string? PrimaryCurrentLanguage,
    string YosbrOptionsFilePath,
    bool YosbrOptionsFileExists,
    bool HasExistingSaves,
    DateTime ReleaseTime,
    int LaunchWindowType,
    bool AutoChangeLanguage);

public sealed record MinecraftLaunchPrerunWorkflowPlan(
    MinecraftLaunchLauncherProfilesPrerunPlan LauncherProfiles,
    MinecraftLaunchOptionsPrerunPlan Options);

public sealed record MinecraftLaunchLauncherProfilesPrerunPlan(
    bool ShouldEnsureFileExists,
    string? Path,
    MinecraftLaunchLauncherProfilesWorkflowPlan Workflow);

public sealed record MinecraftLaunchOptionsPrerunPlan(
    string TargetFilePath,
    MinecraftLaunchOptionsSyncPlan SyncPlan);

using System;
using PCL.Core.Minecraft;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchLauncherProfilesWorkflowService
{
    public static MinecraftLaunchLauncherProfilesWorkflowPlan BuildPlan(MinecraftLaunchLauncherProfilesWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsMicrosoftLogin)
        {
            return MinecraftLaunchLauncherProfilesWorkflowPlan.None;
        }

        var initialAttemptPlan = MinecraftLaunchLauncherProfilesService.BuildUpdatePlan(
            new MinecraftLaunchLauncherProfilesUpdateRequest(
                request.IsMicrosoftLogin,
                request.ExistingProfilesJson,
                request.UserName,
                request.ClientToken));

        if (!initialAttemptPlan.ShouldWrite)
        {
            return MinecraftLaunchLauncherProfilesWorkflowPlan.None;
        }

        var retrySeedJson = MinecraftLauncherProfilesFileService.CreateDefaultProfilesJson(request.DefaultProfileTimestamp);
        var retryAttemptPlan = MinecraftLaunchLauncherProfilesService.BuildUpdatePlan(
            new MinecraftLaunchLauncherProfilesUpdateRequest(
                request.IsMicrosoftLogin,
                retrySeedJson,
                request.UserName,
                request.ClientToken));

        return new MinecraftLaunchLauncherProfilesWorkflowPlan(
            true,
            new MinecraftLaunchLauncherProfilesWriteAttempt(
                initialAttemptPlan.UpdatedProfilesJson!,
                "Updated launcher_profiles.json"),
            new MinecraftLaunchLauncherProfilesWriteAttempt(
                retryAttemptPlan.UpdatedProfilesJson!,
                "Updated launcher_profiles.json after deletion"),
            "Failed to update launcher_profiles.json; will retry after deleting the file",
            "Failed to update launcher_profiles.json");
    }
}

public sealed record MinecraftLaunchLauncherProfilesWorkflowRequest(
    bool IsMicrosoftLogin,
    string? ExistingProfilesJson,
    string UserName,
    string? ClientToken,
    DateTime DefaultProfileTimestamp);

public sealed record MinecraftLaunchLauncherProfilesWorkflowPlan(
    bool ShouldWrite,
    MinecraftLaunchLauncherProfilesWriteAttempt? InitialAttempt,
    MinecraftLaunchLauncherProfilesWriteAttempt? RetryAttempt,
    string RetryLogMessage,
    string FailureLogMessage)
{
    public static MinecraftLaunchLauncherProfilesWorkflowPlan None { get; } = new(
        false,
        null,
        null,
        string.Empty,
        string.Empty);
}

public sealed record MinecraftLaunchLauncherProfilesWriteAttempt(
    string UpdatedProfilesJson,
    string SuccessLogMessage);

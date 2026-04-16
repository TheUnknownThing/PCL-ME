using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchWatcherWorkflowService
{
    public static MinecraftLaunchWatcherWorkflowPlan BuildPlan(MinecraftLaunchWatcherWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SessionLogRequest);
        ArgumentNullException.ThrowIfNull(request.WatcherRequest);

        var startupSummary = MinecraftLaunchSessionLogService.BuildStartupSummary(request.SessionLogRequest);
        var watcherPlan = MinecraftLaunchRuntimeService.BuildWatcherPlan(request.WatcherRequest);

        return new MinecraftLaunchWatcherWorkflowPlan(
            startupSummary.LogLines,
            watcherPlan.RawWindowTitleTemplate,
            watcherPlan.JstackExecutablePath,
            request.OutputRealTimeLog,
            request.OutputRealTimeLog ? "Game real-time logs are being shown" : null);
    }
}

public sealed record MinecraftLaunchWatcherWorkflowRequest(
    MinecraftLaunchSessionLogRequest SessionLogRequest,
    MinecraftLaunchWatcherRequest WatcherRequest,
    bool OutputRealTimeLog);

public sealed record MinecraftLaunchWatcherWorkflowPlan(
    IReadOnlyList<string> StartupSummaryLogLines,
    string RawWindowTitleTemplate,
    string JstackExecutablePath,
    bool ShouldAttachRealtimeLog,
    string? RealtimeLogAttachedMessage);

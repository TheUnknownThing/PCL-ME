using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchGpuPreferenceWorkflowService
{
    public static MinecraftLaunchGpuPreferenceFailurePlan BuildFailurePlan(MinecraftLaunchGpuPreferenceFailureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsRunningAsAdmin || !request.WantHighPerformance)
        {
            return new MinecraftLaunchGpuPreferenceFailurePlan(
                MinecraftLaunchGpuPreferenceFailureActionKind.LogDirectFailure,
                AdminRetryArguments: null,
                RetryLogMessage: null,
                RetryFailureHintMessage: null);
        }

        return new MinecraftLaunchGpuPreferenceFailurePlan(
            MinecraftLaunchGpuPreferenceFailureActionKind.RetryAsAdmin,
            $"--gpu \"{request.JavaExecutablePath}\"",
            "Direct GPU preference changes failed; PCL will restart with administrator privileges and try again",
            "GPU preference changes failed; Minecraft may run on the default GPU");
    }
}

public sealed record MinecraftLaunchGpuPreferenceFailureRequest(
    string JavaExecutablePath,
    bool WantHighPerformance,
    bool IsRunningAsAdmin);

public sealed record MinecraftLaunchGpuPreferenceFailurePlan(
    MinecraftLaunchGpuPreferenceFailureActionKind ActionKind,
    string? AdminRetryArguments,
    string? RetryLogMessage,
    string? RetryFailureHintMessage);

public enum MinecraftLaunchGpuPreferenceFailureActionKind
{
    LogDirectFailure = 0,
    RetryAsAdmin = 1
}

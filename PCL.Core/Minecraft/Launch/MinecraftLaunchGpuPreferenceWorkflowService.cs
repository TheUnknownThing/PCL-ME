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
            "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试",
            "调整显卡设置失败，Minecraft 可能会使用默认显卡运行");
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

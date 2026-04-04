using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

/// <summary>
/// 启动前预检查的结果。
/// </summary>
public sealed record MinecraftLaunchPrecheckResult(
    string? FailureMessage,
    IReadOnlyList<MinecraftLaunchPrompt> Prompts)
{
    public bool IsSuccess => string.IsNullOrEmpty(FailureMessage);
}

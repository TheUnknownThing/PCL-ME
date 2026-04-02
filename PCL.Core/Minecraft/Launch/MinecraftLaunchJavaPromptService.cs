using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchJavaPromptService
{
    public static MinecraftLaunchJavaPrompt BuildMissingJavaPrompt(MinecraftLaunchJavaPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MinimumVersion >= new Version(1, 9))
        {
            var downloadMajorVersion = GetDisplayJavaMajorVersion(request.MinimumVersion).ToString();
            return CreateAutomaticDownloadPrompt(
                $"Java {downloadMajorVersion}",
                request.RecommendedComponent ?? downloadMajorVersion);
        }

        if (request.MaximumVersion < new Version(1, 8))
        {
            return new MinecraftLaunchJavaPrompt(
                request.HasForge
                    ? $"你需要先安装 LegacyJavaFixer Mod，或安装 Java 7 才能启动该版本。{Environment.NewLine}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。"
                    : $"你需要安装 Java 7 才能启动该版本。{Environment.NewLine}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                "未找到 Java",
                [
                    new MinecraftLaunchJavaPromptOption("确定", MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        if (request.MinimumVersion > new Version(1, 8, 0, 140) &&
            request.MaximumVersion < new Version(1, 8, 0, 321))
        {
            return new MinecraftLaunchJavaPrompt(
                $"你需要安装 Java 8u141 ~ 8u320 才能启动该版本。{Environment.NewLine}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                "未找到 Java",
                [
                    new MinecraftLaunchJavaPromptOption("确定", MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        if (request.MinimumVersion > new Version(1, 8, 0, 140))
        {
            return new MinecraftLaunchJavaPrompt(
                $"你需要安装 Java 8u141 或更高版本的 Java 8 才能启动该版本。{Environment.NewLine}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                "未找到 Java",
                [
                    new MinecraftLaunchJavaPromptOption("确定", MinecraftLaunchJavaPromptDecision.Abort)
                ]);
        }

        return CreateAutomaticDownloadPrompt("Java 8", "8");
    }

    private static MinecraftLaunchJavaPrompt CreateAutomaticDownloadPrompt(string versionDescription, string downloadTarget)
    {
        return new MinecraftLaunchJavaPrompt(
            $"PCL 未找到 {versionDescription}，是否需要 PCL 自动下载？{Environment.NewLine}如果你已经安装了 {versionDescription}，可以在 设置 → 启动选项 → 游戏 Java 中手动导入。",
            "自动下载 Java？",
            [
                new MinecraftLaunchJavaPromptOption("自动下载", MinecraftLaunchJavaPromptDecision.Download),
                new MinecraftLaunchJavaPromptOption("取消", MinecraftLaunchJavaPromptDecision.Abort)
            ],
            downloadTarget);
    }

    private static int GetDisplayJavaMajorVersion(Version version)
    {
        return version.Major <= 1 ? version.Minor : version.Major;
    }
}

public sealed record MinecraftLaunchJavaPromptRequest(
    Version MinimumVersion,
    Version MaximumVersion,
    bool HasForge,
    string? RecommendedComponent);

public sealed record MinecraftLaunchJavaPrompt(
    string Message,
    string Title,
    IReadOnlyList<MinecraftLaunchJavaPromptOption> Options,
    string? DownloadTarget = null);

public sealed record MinecraftLaunchJavaPromptOption(
    string Label,
    MinecraftLaunchJavaPromptDecision Decision);

public enum MinecraftLaunchJavaPromptDecision
{
    Download = 0,
    Abort = 1
}

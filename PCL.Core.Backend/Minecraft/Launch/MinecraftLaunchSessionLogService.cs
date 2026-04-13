using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchSessionLogService
{
    public static MinecraftLaunchSessionLogPlan BuildStartupSummary(MinecraftLaunchSessionLogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lines = new List<string>
        {
            string.Empty,
            "~ 基础参数 ~",
            $"PCL 版本：{request.LauncherVersionName} ({request.LauncherVersionCode})",
            $"游戏版本：{request.GameVersionDisplayName}（{request.GameVersionRaw}，Drop {request.GameVersionDrop}{(request.IsGameVersionReliable ? "" : "，无法完全确定")}）",
            $"资源版本：{request.AssetsIndexName}",
            $"实例继承：{(string.IsNullOrEmpty(request.InheritedInstanceName) ? "无" : request.InheritedInstanceName)}",
            $"分配的内存：{request.AllocatedMemoryInGigabytes} GB（{Math.Round(request.AllocatedMemoryInGigabytes * 1024)} MB）",
            $"MC 文件夹：{request.MinecraftFolder}",
            $"实例文件夹：{request.InstanceFolder}",
            $"版本隔离：{request.IsVersionIsolated}",
            $"HMCL 格式：{request.IsHmclFormatJson}",
            $"Java 信息：{(string.IsNullOrWhiteSpace(request.JavaDescription) ? "无可用 Java" : request.JavaDescription)}",
            $"Natives 文件夹：{request.NativesFolder}",
            $"Natives 压缩包：{request.NativeArchiveCount}",
            string.Empty,
            "~ 档案参数 ~",
            $"玩家用户名：{request.PlayerName}",
            $"AccessToken：{request.AccessToken}",
            $"ClientToken：{request.ClientToken}",
            $"UUID：{request.Uuid}",
            $"验证方式：{request.LoginType}",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(request.NativeExtractionDirectory) &&
            !string.Equals(request.NativeExtractionDirectory, request.NativesFolder, StringComparison.OrdinalIgnoreCase))
        {
            lines.Insert(14, $"Natives 解压目录：{request.NativeExtractionDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(request.NativeSearchPath) &&
            !string.Equals(request.NativeSearchPath, request.NativesFolder, StringComparison.Ordinal))
        {
            lines.Insert(14, $"Natives 搜索路径：{request.NativeSearchPath}");
        }

        if (!string.IsNullOrWhiteSpace(request.NativeAliasDirectory))
        {
            lines.Insert(14, $"Natives ASCII 别名：{request.NativeAliasDirectory}");
        }

        return new MinecraftLaunchSessionLogPlan(lines);
    }
}

public sealed record MinecraftLaunchSessionLogRequest(
    string LauncherVersionName,
    int LauncherVersionCode,
    string GameVersionDisplayName,
    string GameVersionRaw,
    int GameVersionDrop,
    bool IsGameVersionReliable,
    string AssetsIndexName,
    string? InheritedInstanceName,
    double AllocatedMemoryInGigabytes,
    string MinecraftFolder,
    string InstanceFolder,
    bool IsVersionIsolated,
    bool IsHmclFormatJson,
    string? JavaDescription,
    string NativesFolder,
    string PlayerName,
    string AccessToken,
    string ClientToken,
    string Uuid,
    string LoginType,
    string? NativeSearchPath = null,
    string? NativeExtractionDirectory = null,
    string? NativeAliasDirectory = null,
    int NativeArchiveCount = 0);

public sealed record MinecraftLaunchSessionLogPlan(
    IReadOnlyList<string> LogLines);

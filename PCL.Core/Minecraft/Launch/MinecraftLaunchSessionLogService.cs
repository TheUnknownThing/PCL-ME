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
            string.Empty,
            "~ 档案参数 ~",
            $"玩家用户名：{request.PlayerName}",
            $"AccessToken：{request.AccessToken}",
            $"ClientToken：{request.ClientToken}",
            $"UUID：{request.Uuid}",
            $"验证方式：{request.LoginType}",
            string.Empty
        };

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
    string LoginType);

public sealed record MinecraftLaunchSessionLogPlan(
    IReadOnlyList<string> LogLines);

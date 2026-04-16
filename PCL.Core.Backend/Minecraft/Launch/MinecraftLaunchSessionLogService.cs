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
            "~ Base Parameters ~",
            $"PCL version: {request.LauncherVersionName} ({request.LauncherVersionCode})",
            $"Game version: {request.GameVersionDisplayName} ({request.GameVersionRaw}, Drop {request.GameVersionDrop}{(request.IsGameVersionReliable ? "" : ", not fully confirmed")})",
            $"Asset version: {request.AssetsIndexName}",
            $"Inherited instance: {(string.IsNullOrEmpty(request.InheritedInstanceName) ? "None" : request.InheritedInstanceName)}",
            $"Allocated memory: {request.AllocatedMemoryInGigabytes} GB ({Math.Round(request.AllocatedMemoryInGigabytes * 1024)} MB)",
            $"Minecraft folder: {request.MinecraftFolder}",
            $"Instance folder: {request.InstanceFolder}",
            $"Version isolation: {request.IsVersionIsolated}",
            $"HMCL format: {request.IsHmclFormatJson}",
            $"Java info: {(string.IsNullOrWhiteSpace(request.JavaDescription) ? "No Java available" : request.JavaDescription)}",
            $"Natives folder: {request.NativesFolder}",
            $"Native archives: {request.NativeArchiveCount}",
            string.Empty,
            "~ Profile Parameters ~",
            $"Player name: {request.PlayerName}",
            $"Access token: {request.AccessToken}",
            $"Client token: {request.ClientToken}",
            $"UUID: {request.Uuid}",
            $"Login type: {request.LoginType}",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(request.NativeExtractionDirectory) &&
            !string.Equals(request.NativeExtractionDirectory, request.NativesFolder, StringComparison.OrdinalIgnoreCase))
        {
            lines.Insert(14, $"Native extraction directory: {request.NativeExtractionDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(request.NativeSearchPath) &&
            !string.Equals(request.NativeSearchPath, request.NativesFolder, StringComparison.Ordinal))
        {
            lines.Insert(14, $"Native search path: {request.NativeSearchPath}");
        }

        if (!string.IsNullOrWhiteSpace(request.NativeAliasDirectory))
        {
            lines.Insert(14, $"Native ASCII alias: {request.NativeAliasDirectory}");
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

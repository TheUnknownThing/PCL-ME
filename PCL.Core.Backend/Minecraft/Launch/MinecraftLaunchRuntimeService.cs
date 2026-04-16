using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchRuntimeService
{
    public static MinecraftLaunchProcessPlan BuildProcessPlan(MinecraftLaunchProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.JavaExecutablePath))
        {
            throw new ArgumentException("The Java executable path cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.JavaFolder))
        {
            throw new ArgumentException("The Java folder path cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("The working directory cannot be empty.", nameof(request));
        }

        var hasJavaw = !string.IsNullOrWhiteSpace(request.JavawExecutablePath);
        var shouldUseConsoleJava = request.PreferConsoleJava && hasJavaw;
        var executablePath = shouldUseConsoleJava
            ? request.JavaExecutablePath
            : request.JavawExecutablePath ?? request.JavaExecutablePath;

        var pathEntries = string.IsNullOrWhiteSpace(request.CurrentPathEnvironmentValue)
            ? []
            : request.CurrentPathEnvironmentValue
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        if (!pathEntries.Contains(request.JavaFolder, StringComparer.OrdinalIgnoreCase))
        {
            pathEntries.Add(request.JavaFolder);
        }

        return new MinecraftLaunchProcessPlan(
            executablePath,
            request.WorkingDirectory,
            shouldUseConsoleJava || !hasJavaw,
            request.LaunchArguments,
            string.Join(Path.PathSeparator, pathEntries),
            request.AppDataPath,
            request.EnvironmentVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ResolvePriority(request.PrioritySetting));
    }

    public static MinecraftLaunchWatcherPlan BuildWatcherPlan(MinecraftLaunchWatcherRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawWindowTitle = !string.IsNullOrWhiteSpace(request.VersionSpecificWindowTitleTemplate)
            ? request.VersionSpecificWindowTitleTemplate
            : request.VersionTitleExplicitlyEmpty
                ? string.Empty
                : request.GlobalWindowTitleTemplate ?? string.Empty;

        var usesWindowsPathStyle = request.JavaFolder.Contains('\\') ||
                                   (request.JavaFolder.Length >= 2 &&
                                    char.IsLetter(request.JavaFolder[0]) &&
                                    request.JavaFolder[1] == ':');
        var separator = usesWindowsPathStyle ? '\\' : Path.DirectorySeparatorChar;
        var jstackFileName = usesWindowsPathStyle || OperatingSystem.IsWindows()
            ? "jstack.exe"
            : "jstack";
        var jstackPath = request.JavaFolder.TrimEnd('\\', '/') + separator + jstackFileName;

        return new MinecraftLaunchWatcherPlan(
            rawWindowTitle,
            request.JstackExecutableExists ? jstackPath : string.Empty);
    }

    private static MinecraftLaunchProcessPriorityKind ResolvePriority(int prioritySetting)
    {
        return prioritySetting switch
        {
            0 => MinecraftLaunchProcessPriorityKind.AboveNormal,
            2 => MinecraftLaunchProcessPriorityKind.BelowNormal,
            _ => MinecraftLaunchProcessPriorityKind.Normal
        };
    }
}

public sealed record MinecraftLaunchProcessRequest(
    bool PreferConsoleJava,
    string JavaExecutablePath,
    string? JavawExecutablePath,
    string JavaFolder,
    string CurrentPathEnvironmentValue,
    string AppDataPath,
    string WorkingDirectory,
    string LaunchArguments,
    IReadOnlyDictionary<string, string>? EnvironmentVariables,
    int PrioritySetting);

public sealed record MinecraftLaunchProcessPlan(
    string ExecutablePath,
    string WorkingDirectory,
    bool CreateNoWindow,
    string LaunchArguments,
    string PathEnvironmentValue,
    string AppDataEnvironmentValue,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    MinecraftLaunchProcessPriorityKind PriorityKind);

public sealed record MinecraftLaunchWatcherRequest(
    string? VersionSpecificWindowTitleTemplate,
    bool VersionTitleExplicitlyEmpty,
    string? GlobalWindowTitleTemplate,
    string JavaFolder,
    bool JstackExecutableExists);

public sealed record MinecraftLaunchWatcherPlan(
    string RawWindowTitleTemplate,
    string JstackExecutablePath);

public enum MinecraftLaunchProcessPriorityKind
{
    Normal = 0,
    AboveNormal = 1,
    BelowNormal = 2
}

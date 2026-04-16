using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchExecutionWorkflowService
{
    public static MinecraftLaunchCustomCommandShellPlan BuildCustomCommandShellPlan(MinecraftLaunchCustomCommandShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            throw new ArgumentException("The custom command cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("The custom command working directory cannot be empty.", nameof(request));
        }

        var fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? $"/c \"{request.Command}\""
            : $"-lc \"{request.Command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

        return new MinecraftLaunchCustomCommandShellPlan(
            FileName: fileName,
            Arguments: arguments,
            WorkingDirectory: request.WorkingDirectory,
            UseShellExecute: false,
            CreateNoWindow: true,
            request.WaitForExit,
            request.StartLogMessage,
            request.FailureLogMessage,
            "Launch was canceled, so the custom command CMD process was forcefully terminated");
    }

    public static MinecraftLaunchProcessShellPlan BuildProcessShellPlan(MinecraftLaunchProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtimePlan = MinecraftLaunchRuntimeService.BuildProcessPlan(request);
        var shouldRedirectProcessOutput = OperatingSystem.IsWindows();
        return new MinecraftLaunchProcessShellPlan(
            runtimePlan.ExecutablePath,
            runtimePlan.LaunchArguments,
            runtimePlan.WorkingDirectory,
            runtimePlan.CreateNoWindow,
            UseShellExecute: false,
            RedirectStandardOutput: shouldRedirectProcessOutput,
            RedirectStandardError: shouldRedirectProcessOutput,
            runtimePlan.PathEnvironmentValue,
            runtimePlan.AppDataEnvironmentValue,
            runtimePlan.EnvironmentVariables,
            runtimePlan.PriorityKind,
            "Started game process: " + runtimePlan.ExecutablePath,
            "Launch was canceled, so the game process was forcefully terminated");
    }
}

public sealed record MinecraftLaunchCustomCommandShellRequest(
    string Command,
    bool WaitForExit,
    string WorkingDirectory,
    string StartLogMessage,
    string FailureLogMessage);

public sealed record MinecraftLaunchCustomCommandShellPlan(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    bool UseShellExecute,
    bool CreateNoWindow,
    bool WaitForExit,
    string StartLogMessage,
    string FailureLogMessage,
    string AbortKillLogMessage);

public sealed record MinecraftLaunchProcessShellPlan(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    bool CreateNoWindow,
    bool UseShellExecute,
    bool RedirectStandardOutput,
    bool RedirectStandardError,
    string PathEnvironmentValue,
    string AppDataEnvironmentValue,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    MinecraftLaunchProcessPriorityKind PriorityKind,
    string StartedLogMessage,
    string AbortKillLogMessage);

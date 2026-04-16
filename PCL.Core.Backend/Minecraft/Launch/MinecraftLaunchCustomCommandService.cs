using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchCustomCommandService
{
    public static MinecraftLaunchCustomCommandPlan BuildPlan(MinecraftLaunchCustomCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.InstanceName))
        {
            throw new ArgumentException("The instance name cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("The working directory cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.JavaExecutablePath))
        {
            throw new ArgumentException("The Java executable path cannot be empty.", nameof(request));
        }

        var useUtf8Encoding = request.JavaMajorVersion > 8;
        var scriptLines = new List<string>();
        var lineBreak = OperatingSystem.IsWindows() ? "\r\n" : "\n";

        if (OperatingSystem.IsWindows())
        {
            if (useUtf8Encoding)
            {
                scriptLines.Add("chcp 65001>nul");
            }

            scriptLines.Add("@echo off");
            scriptLines.Add($"title Launch - {request.InstanceName}");
            scriptLines.Add("echo Game is starting, please wait.");
            scriptLines.Add($"cd /D \"{request.WorkingDirectory}\"");
        }
        else
        {
            scriptLines.Add("#!/bin/sh");
            scriptLines.Add("printf '%s\\n' 'Game is starting, please wait.'");
            scriptLines.Add($"cd \"{request.WorkingDirectory}\" || exit 1");
        }

        var commandExecutions = new List<MinecraftLaunchCustomCommandExecution>();
        AddCommand(
            commandExecutions,
            scriptLines,
            request.GlobalCommand,
            request.WaitForGlobalCommand,
            MinecraftLaunchCustomCommandScope.Global);
        AddCommand(
            commandExecutions,
            scriptLines,
            request.InstanceCommand,
            request.WaitForInstanceCommand,
            MinecraftLaunchCustomCommandScope.Instance);

        AppendEnvironmentVariables(scriptLines, request.EnvironmentVariables);
        scriptLines.Add($"\"{request.JavaExecutablePath}\" {request.LaunchArguments}");
        if (OperatingSystem.IsWindows())
        {
            scriptLines.Add("echo Game has exited.");
            scriptLines.Add("pause");
        }
        else
        {
            scriptLines.Add("printf '%s\\n' 'Game has exited.'");
        }

        return new MinecraftLaunchCustomCommandPlan(
            string.Join(lineBreak, scriptLines),
            useUtf8Encoding,
            commandExecutions);
    }

    private static void AddCommand(
        List<MinecraftLaunchCustomCommandExecution> commandExecutions,
        List<string> batchLines,
        string? command,
        bool waitForExit,
        MinecraftLaunchCustomCommandScope scope)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        batchLines.Add(command);
        commandExecutions.Add(new MinecraftLaunchCustomCommandExecution(
            scope,
            command,
            waitForExit,
            scope == MinecraftLaunchCustomCommandScope.Global
                ? $"Running global custom command: {command}"
                : $"Running instance custom command: {command}",
            scope == MinecraftLaunchCustomCommandScope.Global
                ? "Failed to run the global custom command"
                : "Failed to run the instance custom command"));
    }

    private static void AppendEnvironmentVariables(List<string> scriptLines, IReadOnlyDictionary<string, string> environmentVariables)
    {
        foreach (var environmentVariable in environmentVariables)
        {
            if (OperatingSystem.IsWindows())
            {
                scriptLines.Add($"set \"{environmentVariable.Key}={EscapeWindowsEnvironmentValue(environmentVariable.Value)}\"");
                continue;
            }

            scriptLines.Add($"export {environmentVariable.Key}={EscapePosixEnvironmentValue(environmentVariable.Value)}");
        }
    }

    private static string EscapeWindowsEnvironmentValue(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    private static string EscapePosixEnvironmentValue(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}

public sealed record MinecraftLaunchCustomCommandRequest(
    int JavaMajorVersion,
    string InstanceName,
    string WorkingDirectory,
    string JavaExecutablePath,
    string LaunchArguments,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string? GlobalCommand,
    bool WaitForGlobalCommand,
    string? InstanceCommand,
    bool WaitForInstanceCommand);

public sealed record MinecraftLaunchCustomCommandPlan(
    string BatchScriptContent,
    bool UseUtf8Encoding,
    IReadOnlyList<MinecraftLaunchCustomCommandExecution> CommandExecutions);

public sealed record MinecraftLaunchCustomCommandExecution(
    MinecraftLaunchCustomCommandScope Scope,
    string Command,
    bool WaitForExit,
    string StartLogMessage,
    string FailureLogMessage);

public enum MinecraftLaunchCustomCommandScope
{
    Global = 0,
    Instance = 1
}

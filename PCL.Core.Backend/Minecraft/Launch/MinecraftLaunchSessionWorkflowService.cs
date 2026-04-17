using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchSessionWorkflowService
{
    public static MinecraftLaunchSessionStartWorkflowPlan BuildStartPlan(MinecraftLaunchSessionStartWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CustomCommandWorkflow);
        ArgumentNullException.ThrowIfNull(request.ProcessRequest);
        ArgumentNullException.ThrowIfNull(request.WatcherWorkflowRequest);

        if (string.IsNullOrWhiteSpace(request.CustomCommandWorkflow.ShellWorkingDirectory))
        {
            throw new ArgumentException("The custom command working directory cannot be empty.", nameof(request));
        }

        var customCommandPlan = MinecraftLaunchCustomCommandService.BuildPlan(request.CustomCommandWorkflow.CommandRequest);
        var customCommandShellPlans = BuildCustomCommandShellPlans(customCommandPlan.CommandExecutions, request.CustomCommandWorkflow.ShellWorkingDirectory);
        var processShellPlan = MinecraftLaunchExecutionWorkflowService.BuildProcessShellPlan(request.ProcessRequest);
        var watcherWorkflowPlan = MinecraftLaunchWatcherWorkflowService.BuildPlan(request.WatcherWorkflowRequest);
        var launchScriptContent = BuildLaunchScriptContent(
            request.CustomCommandWorkflow.CommandRequest.InstanceName,
            customCommandPlan.UseUtf8Encoding,
            customCommandShellPlans,
            request.CustomCommandWorkflow.ShellWorkingDirectory,
            processShellPlan);

        return new MinecraftLaunchSessionStartWorkflowPlan(
            customCommandPlan with
            {
                BatchScriptContent = launchScriptContent
            },
            customCommandShellPlans,
            processShellPlan,
            watcherWorkflowPlan);
    }

    public static MinecraftGameShellPlan BuildPostLaunchPlan(MinecraftLaunchPostLaunchShellRequest request)
    {
        return MinecraftLaunchShellService.GetPostLaunchShellPlan(request);
    }

    private static IReadOnlyList<MinecraftLaunchCustomCommandShellPlan> BuildCustomCommandShellPlans(
        IReadOnlyList<MinecraftLaunchCustomCommandExecution> commandExecutions,
        string shellWorkingDirectory)
    {
        return commandExecutions
            .Select(commandExecution => MinecraftLaunchExecutionWorkflowService.BuildCustomCommandShellPlan(
                new MinecraftLaunchCustomCommandShellRequest(
                    commandExecution.Command,
                    commandExecution.WaitForExit,
                    shellWorkingDirectory,
                    commandExecution.StartLogMessage,
                    commandExecution.FailureLogMessage)))
            .ToArray();
    }

    private static string BuildLaunchScriptContent(
        string instanceName,
        bool useUtf8Encoding,
        IReadOnlyList<MinecraftLaunchCustomCommandShellPlan> customCommandShellPlans,
        string customCommandWorkingDirectory,
        MinecraftLaunchProcessShellPlan processShellPlan)
    {
        var lineBreak = OperatingSystem.IsWindows() ? "\r\n" : "\n";
        var scriptLines = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            if (useUtf8Encoding)
            {
                scriptLines.Add("chcp 65001>nul");
            }

            scriptLines.Add("@echo off");
            scriptLines.Add($"title Launch - {instanceName}");
            scriptLines.Add("echo Game is starting, please wait.");
        }
        else
        {
            scriptLines.Add("#!/bin/sh");
            scriptLines.Add("printf '%s\\n' 'Game is starting, please wait.'");
        }

        if (customCommandShellPlans.Count > 0)
        {
            AppendChangeDirectory(scriptLines, customCommandWorkingDirectory);
            foreach (var customCommandShellPlan in customCommandShellPlans)
            {
                AppendCustomCommandInvocation(scriptLines, customCommandShellPlan);
            }
        }

        AppendChangeDirectory(scriptLines, processShellPlan.WorkingDirectory);
        AppendEnvironmentVariables(scriptLines, BuildProcessEnvironmentVariables(processShellPlan));
        scriptLines.Add(BuildShellCommand(processShellPlan.FileName, processShellPlan.Arguments));

        if (OperatingSystem.IsWindows())
        {
            scriptLines.Add("set \"launch_exit_code=%errorlevel%\"");
            scriptLines.Add("echo Game has exited.");
            scriptLines.Add("pause");
            scriptLines.Add("exit /b %launch_exit_code%");
        }
        else
        {
            scriptLines.Add("launch_exit_code=$?");
            scriptLines.Add("printf '%s\\n' 'Game has exited.'");
            scriptLines.Add("exit \"$launch_exit_code\"");
        }

        return string.Join(lineBreak, scriptLines);
    }

    private static IReadOnlyDictionary<string, string> BuildProcessEnvironmentVariables(
        MinecraftLaunchProcessShellPlan processShellPlan)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Path"] = processShellPlan.PathEnvironmentValue,
            ["appdata"] = processShellPlan.AppDataEnvironmentValue
        };

        foreach (var environmentVariable in processShellPlan.EnvironmentVariables)
        {
            environmentVariables[environmentVariable.Key] = environmentVariable.Value;
        }

        return environmentVariables;
    }

    private static void AppendChangeDirectory(List<string> scriptLines, string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            scriptLines.Add($"cd /D \"{EscapeWindowsDoubleQuotedValue(workingDirectory)}\"");
            return;
        }

        scriptLines.Add($"cd {EscapePosixValue(workingDirectory)} || exit 1");
    }

    private static void AppendCustomCommandInvocation(
        List<string> scriptLines,
        MinecraftLaunchCustomCommandShellPlan customCommandShellPlan)
    {
        var shellCommand = BuildShellCommand(customCommandShellPlan.FileName, customCommandShellPlan.Arguments);
        if (OperatingSystem.IsWindows())
        {
            if (customCommandShellPlan.WaitForExit)
            {
                scriptLines.Add(shellCommand);
                scriptLines.Add("if errorlevel 1 exit /b %errorlevel%");
                return;
            }

            scriptLines.Add(BuildWindowsDetachedShellCommand(customCommandShellPlan.FileName, customCommandShellPlan.Arguments));
            return;
        }

        if (customCommandShellPlan.WaitForExit)
        {
            scriptLines.Add(shellCommand);
            scriptLines.Add("command_exit_code=$?");
            scriptLines.Add("if [ \"$command_exit_code\" -ne 0 ]; then exit \"$command_exit_code\"; fi");
            return;
        }

        scriptLines.Add(shellCommand + " &");
    }

    private static void AppendEnvironmentVariables(
        List<string> scriptLines,
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        foreach (var environmentVariable in environmentVariables)
        {
            if (OperatingSystem.IsWindows())
            {
                scriptLines.Add($"set \"{environmentVariable.Key}={EscapeWindowsEnvironmentValue(environmentVariable.Value)}\"");
                continue;
            }

            scriptLines.Add($"export {environmentVariable.Key}={EscapePosixValue(environmentVariable.Value)}");
        }
    }

    private static string BuildShellCommand(string fileName, string? arguments)
    {
        return OperatingSystem.IsWindows()
            ? BuildWindowsShellCommand(fileName, arguments)
            : BuildPosixShellCommand(fileName, arguments);
    }

    private static string BuildWindowsShellCommand(string fileName, string? arguments)
    {
        var command = $"\"{EscapeWindowsDoubleQuotedValue(fileName)}\"";
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            command += " " + arguments.Trim();
        }

        return command;
    }

    private static string BuildWindowsDetachedShellCommand(string fileName, string? arguments)
    {
        var command = $"start \"\" /b \"{EscapeWindowsDoubleQuotedValue(fileName)}\"";
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            command += " " + arguments.Trim();
        }

        return command;
    }

    private static string BuildPosixShellCommand(string fileName, string? arguments)
    {
        var command = EscapePosixValue(fileName);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            command += " " + arguments.Trim();
        }

        return command;
    }

    private static string EscapeWindowsEnvironmentValue(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    private static string EscapeWindowsDoubleQuotedValue(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapePosixValue(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}

public sealed record MinecraftLaunchCustomCommandWorkflowRequest(
    MinecraftLaunchCustomCommandRequest CommandRequest,
    string ShellWorkingDirectory);

public sealed record MinecraftLaunchSessionStartWorkflowRequest(
    MinecraftLaunchCustomCommandWorkflowRequest CustomCommandWorkflow,
    MinecraftLaunchProcessRequest ProcessRequest,
    MinecraftLaunchWatcherWorkflowRequest WatcherWorkflowRequest);

public sealed record MinecraftLaunchSessionStartWorkflowPlan(
    MinecraftLaunchCustomCommandPlan CustomCommandPlan,
    IReadOnlyList<MinecraftLaunchCustomCommandShellPlan> CustomCommandShellPlans,
    MinecraftLaunchProcessShellPlan ProcessShellPlan,
    MinecraftLaunchWatcherWorkflowPlan WatcherWorkflowPlan);

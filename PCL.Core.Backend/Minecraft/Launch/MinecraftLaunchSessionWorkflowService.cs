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

        return new MinecraftLaunchSessionStartWorkflowPlan(
            customCommandPlan,
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

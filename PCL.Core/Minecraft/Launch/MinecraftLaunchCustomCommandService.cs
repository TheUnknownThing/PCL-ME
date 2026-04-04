using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchCustomCommandService
{
    private const string BatchLineBreak = "\r\n";

    public static MinecraftLaunchCustomCommandPlan BuildPlan(MinecraftLaunchCustomCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.InstanceName))
        {
            throw new ArgumentException("实例名称不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("工作目录不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.JavaExecutablePath))
        {
            throw new ArgumentException("Java 可执行文件路径不能为空。", nameof(request));
        }

        var useUtf8Encoding = request.JavaMajorVersion > 8;
        var batchLines = new List<string>();

        if (useUtf8Encoding)
        {
            batchLines.Add("chcp 65001>nul");
        }

        batchLines.Add("@echo off");
        batchLines.Add($"title 启动 - {request.InstanceName}");
        batchLines.Add("echo 游戏正在启动，请稍候。");
        batchLines.Add($"cd /D \"{request.WorkingDirectory}\"");

        var commandExecutions = new List<MinecraftLaunchCustomCommandExecution>();
        AddCommand(
            commandExecutions,
            batchLines,
            request.GlobalCommand,
            request.WaitForGlobalCommand,
            MinecraftLaunchCustomCommandScope.Global);
        AddCommand(
            commandExecutions,
            batchLines,
            request.InstanceCommand,
            request.WaitForInstanceCommand,
            MinecraftLaunchCustomCommandScope.Instance);

        batchLines.Add($"\"{request.JavaExecutablePath}\" {request.LaunchArguments}");
        batchLines.Add("echo 游戏已退出。");
        batchLines.Add("pause");

        return new MinecraftLaunchCustomCommandPlan(
            string.Join(BatchLineBreak, batchLines),
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
                ? $"正在执行全局自定义命令：{command}"
                : $"正在执行实例自定义命令：{command}",
            scope == MinecraftLaunchCustomCommandScope.Global
                ? "执行全局自定义命令失败"
                : "执行实例自定义命令失败"));
    }
}

public sealed record MinecraftLaunchCustomCommandRequest(
    int JavaMajorVersion,
    string InstanceName,
    string WorkingDirectory,
    string JavaExecutablePath,
    string LaunchArguments,
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

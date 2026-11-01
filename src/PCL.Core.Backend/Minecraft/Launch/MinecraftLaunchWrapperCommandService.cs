using System;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchWrapperCommandService
{
    public static MinecraftLaunchWrappedCommandPlan BuildPlan(MinecraftLaunchWrapperCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetExecutablePath))
        {
            throw new ArgumentException("The target executable path cannot be empty.", nameof(request));
        }

        var wrapperCommand = request.WrapperCommand?.Trim();
        if (string.IsNullOrWhiteSpace(wrapperCommand))
        {
            return new MinecraftLaunchWrappedCommandPlan(
                request.TargetExecutablePath,
                request.TargetArguments ?? string.Empty,
                $"\"{EscapeDoubleQuotedValue(request.TargetExecutablePath)}\"{AppendCommandArguments(request.TargetArguments)}");
        }

        var (wrapperExecutable, wrapperArguments) = SplitExecutableAndArguments(wrapperCommand);
        if (string.IsNullOrWhiteSpace(wrapperExecutable))
        {
            return new MinecraftLaunchWrappedCommandPlan(
                request.TargetExecutablePath,
                request.TargetArguments ?? string.Empty,
                $"\"{EscapeDoubleQuotedValue(request.TargetExecutablePath)}\"{AppendCommandArguments(request.TargetArguments)}");
        }

        var quotedTargetExecutable = $"\"{EscapeDoubleQuotedValue(request.TargetExecutablePath)}\"";
        var combinedArguments = JoinArguments(
            wrapperArguments,
            quotedTargetExecutable,
            request.TargetArguments);

        return new MinecraftLaunchWrappedCommandPlan(
            wrapperExecutable,
            combinedArguments,
            JoinArguments(wrapperCommand, quotedTargetExecutable, request.TargetArguments));
    }

    private static (string Executable, string Arguments) SplitExecutableAndArguments(string wrapperCommand)
    {
        var trimmed = wrapperCommand.Trim();
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var executable = new System.Text.StringBuilder();
        char? quote = null;
        var index = 0;
        while (index < trimmed.Length)
        {
            var current = trimmed[index];
            if (quote is not null)
            {
                if (current == quote)
                {
                    quote = null;
                }
                else
                {
                    executable.Append(current);
                }

                index++;
                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                break;
            }

            executable.Append(current);
            index++;
        }

        var arguments = index >= trimmed.Length
            ? string.Empty
            : trimmed[index..].TrimStart();
        return (executable.ToString(), arguments);
    }

    private static string JoinArguments(params string?[] segments)
    {
        return string.Join(
            " ",
            segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).Select(segment => segment!.Trim()));
    }

    private static string AppendCommandArguments(string? arguments)
    {
        return string.IsNullOrWhiteSpace(arguments)
            ? string.Empty
            : " " + arguments.Trim();
    }

    private static string EscapeDoubleQuotedValue(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

public sealed record MinecraftLaunchWrapperCommandRequest(
    string TargetExecutablePath,
    string? TargetArguments,
    string? WrapperCommand);

public sealed record MinecraftLaunchWrappedCommandPlan(
    string ExecutablePath,
    string Arguments,
    string ShellCommandLine);

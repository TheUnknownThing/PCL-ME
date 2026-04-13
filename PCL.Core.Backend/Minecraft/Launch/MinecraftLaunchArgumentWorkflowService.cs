using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchArgumentWorkflowService
{
    private static readonly DateTime QuickPlayReleaseThreshold = new(2023, 4, 4);

    public static MinecraftLaunchArgumentPlan BuildPlan(MinecraftLaunchArgumentPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseArguments);
        ArgumentNullException.ThrowIfNull(request.ReplacementValues);

        var arguments = request.BaseArguments;
        if (request.JavaMajorVersion > 8)
        {
            if (!arguments.Contains("-Dstdout.encoding=", StringComparison.Ordinal))
            {
                arguments = "-Dstdout.encoding=UTF-8 " + arguments;
            }

            if (!arguments.Contains("-Dstderr.encoding=", StringComparison.Ordinal))
            {
                arguments = "-Dstderr.encoding=UTF-8 " + arguments;
            }
        }

        if (request.JavaMajorVersion >= 18 &&
            !arguments.Contains("-Dfile.encoding=", StringComparison.Ordinal))
        {
            arguments = "-Dfile.encoding=COMPAT " + arguments;
        }

        arguments = arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"", StringComparison.Ordinal);

        if (request.UseFullscreen)
        {
            arguments += " --fullscreen";
        }

        if (request.ExtraArguments is not null)
        {
            foreach (var extraArgument in request.ExtraArguments.Where(arg => !string.IsNullOrWhiteSpace(arg)))
            {
                arguments += " " + extraArgument.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CustomGameArguments))
        {
            arguments += " " + request.CustomGameArguments;
        }

        var replacementValues = request.ReplacementValues.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);

        if (replacementValues.TryGetValue("${version_type}", out var versionTypeValue) &&
            string.IsNullOrWhiteSpace(versionTypeValue))
        {
            arguments = arguments.Replace(" --versionType ${version_type}", string.Empty, StringComparison.Ordinal);
            arguments = arguments.Replace("--versionType ${version_type} ", string.Empty, StringComparison.Ordinal);
            if (string.Equals(arguments, "--versionType ${version_type}", StringComparison.Ordinal))
            {
                arguments = string.Empty;
            }

            replacementValues["${version_type}"] = "\"\"";
        }

        var finalArgumentsBuilder = new StringBuilder();
        foreach (var argumentPart in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var resolvedArgument = argumentPart;
            foreach (var entry in replacementValues)
            {
                resolvedArgument = resolvedArgument.Replace(entry.Key, entry.Value, StringComparison.Ordinal);
            }

            if ((resolvedArgument.Contains(' ') || resolvedArgument.Contains(@":\", StringComparison.Ordinal)) &&
                !resolvedArgument.EndsWith("\"", StringComparison.Ordinal))
            {
                resolvedArgument = $"\"{resolvedArgument}\"";
            }

            if (finalArgumentsBuilder.Length > 0)
            {
                finalArgumentsBuilder.Append(' ');
            }

            finalArgumentsBuilder.Append(resolvedArgument);
        }

        if (request.WorldName is not null)
        {
            finalArgumentsBuilder.Append($" --quickPlaySingleplayer \"{request.WorldName}\"");
        }

        var shouldWarnAboutLegacyServerWithOptiFine = false;
        if (string.IsNullOrWhiteSpace(request.WorldName) &&
            !string.IsNullOrWhiteSpace(request.ServerAddress))
        {
            if (request.ReleaseTime > QuickPlayReleaseThreshold)
            {
                finalArgumentsBuilder.Append($" --quickPlayMultiplayer \"{request.ServerAddress}\"");
            }
            else
            {
                var serverAddress = request.ServerAddress;
                var separatorIndex = serverAddress.IndexOf(':');
                if (separatorIndex >= 0)
                {
                    finalArgumentsBuilder.Append(" --server ");
                    finalArgumentsBuilder.Append(serverAddress[..separatorIndex]);
                    finalArgumentsBuilder.Append(" --port ");
                    finalArgumentsBuilder.Append(serverAddress[(separatorIndex + 1)..]);
                }
                else
                {
                    finalArgumentsBuilder.Append(" --server ");
                    finalArgumentsBuilder.Append(serverAddress);
                    finalArgumentsBuilder.Append(" --port 25565");
                }

                shouldWarnAboutLegacyServerWithOptiFine = request.HasOptiFine;
            }
        }

        return new MinecraftLaunchArgumentPlan(
            finalArgumentsBuilder.ToString(),
            shouldWarnAboutLegacyServerWithOptiFine);
    }
}

public sealed record MinecraftLaunchArgumentPlanRequest(
    string BaseArguments,
    int JavaMajorVersion,
    bool UseFullscreen,
    IReadOnlyList<string>? ExtraArguments,
    string? CustomGameArguments,
    IReadOnlyDictionary<string, string> ReplacementValues,
    string? WorldName,
    string? ServerAddress,
    DateTime ReleaseTime,
    bool HasOptiFine);

public sealed record MinecraftLaunchArgumentPlan(
    string FinalArguments,
    bool ShouldWarnAboutLegacyServerWithOptiFine);

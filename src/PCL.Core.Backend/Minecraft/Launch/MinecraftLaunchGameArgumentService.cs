using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchGameArgumentService
{
    public static MinecraftLaunchGameArgumentPlan BuildLegacyPlan(MinecraftLaunchLegacyGameArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parts = new List<string>();
        if (request.UseRetroWrapper)
        {
            parts.Add("--tweakClass com.zero.retrowrapper.RetroTweaker");
        }

        var argumentText = request.MinecraftArguments;
        if (!argumentText.Contains("--height", StringComparison.Ordinal))
        {
            argumentText += " --height ${resolution_height} --width ${resolution_width}";
        }

        parts.Add(argumentText);
        return NormalizeOptiFineTweaker(string.Join(" ", parts), request.HasForgeOrLiteLoader, request.HasOptiFine);
    }

    public static MinecraftLaunchGameArgumentPlan BuildModernPlan(MinecraftLaunchModernGameArgumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseArguments);

        var merged = MergeAndDeduplicateArguments(request.BaseArguments);
        return NormalizeOptiFineTweaker(string.Join(" ", merged), request.HasForgeOrLiteLoader, request.HasOptiFine);
    }

    private static MinecraftLaunchGameArgumentPlan NormalizeOptiFineTweaker(string arguments, bool hasForgeOrLiteLoader, bool hasOptiFine)
    {
        var logs = new List<string>();
        var shouldRewriteJson = false;

        if (hasForgeOrLiteLoader && hasOptiFine)
        {
            if (arguments.Contains("--tweakClass optifine.OptiFineForgeTweaker", StringComparison.Ordinal))
            {
                logs.Add("[Launch] Found the correct OptiFineForge TweakClass; current arguments: " + arguments);
                arguments = arguments
                    .Replace(" --tweakClass optifine.OptiFineForgeTweaker", string.Empty, StringComparison.Ordinal)
                    .Replace("--tweakClass optifine.OptiFineForgeTweaker ", string.Empty, StringComparison.Ordinal)
                    + " --tweakClass optifine.OptiFineForgeTweaker";
            }

            if (arguments.Contains("--tweakClass optifine.OptiFineTweaker", StringComparison.Ordinal))
            {
                logs.Add("[Launch] Found an incorrect OptiFineForge TweakClass; current arguments: " + arguments);
                arguments = arguments
                    .Replace(" --tweakClass optifine.OptiFineTweaker", string.Empty, StringComparison.Ordinal)
                    .Replace("--tweakClass optifine.OptiFineTweaker ", string.Empty, StringComparison.Ordinal)
                    + " --tweakClass optifine.OptiFineForgeTweaker";
                shouldRewriteJson = true;
            }
        }

        return new MinecraftLaunchGameArgumentPlan(arguments, shouldRewriteJson, logs);
    }

    private static IReadOnlyList<string> MergeAndDeduplicateArguments(IReadOnlyList<string> entries)
    {
        var merged = new List<string>();
        for (var index = 0; index < entries.Count; index++)
        {
            var currentEntry = entries[index];
            if (string.IsNullOrWhiteSpace(currentEntry))
            {
                continue;
            }

            if (currentEntry.StartsWith("-", StringComparison.Ordinal))
            {
                while (index < entries.Count - 1 &&
                       !string.IsNullOrWhiteSpace(entries[index + 1]) &&
                       !entries[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    index++;
                    currentEntry += " " + entries[index];
                }
            }

            merged.Add(currentEntry);
        }

        return merged.Distinct(StringComparer.Ordinal).ToList();
    }
}

public sealed record MinecraftLaunchLegacyGameArgumentRequest(
    string MinecraftArguments,
    bool UseRetroWrapper,
    bool HasForgeOrLiteLoader,
    bool HasOptiFine);

public sealed record MinecraftLaunchModernGameArgumentRequest(
    IReadOnlyList<string> BaseArguments,
    bool HasForgeOrLiteLoader,
    bool HasOptiFine);

public sealed record MinecraftLaunchGameArgumentPlan(
    string Arguments,
    bool ShouldRewriteOptiFineTweakerInJson,
    IReadOnlyList<string> LogMessages);

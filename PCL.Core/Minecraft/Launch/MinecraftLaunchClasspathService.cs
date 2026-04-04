using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchClasspathService
{
    public static MinecraftLaunchClasspathPlan BuildPlan(MinecraftLaunchClasspathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Libraries);

        var classpathEntries = new List<string>();
        string? optiFinePath = null;

        if (!string.IsNullOrWhiteSpace(request.RetroWrapperPath))
        {
            classpathEntries.Add(request.RetroWrapperPath);
        }

        foreach (var library in request.Libraries)
        {
            if (library.IsNatives || string.IsNullOrWhiteSpace(library.Path))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(library.Name) &&
                library.Name.Contains("com.cleanroommc:cleanroom:0.2", StringComparison.Ordinal))
            {
                classpathEntries.Insert(0, library.Path);
            }

            if (string.Equals(library.Name, "optifine:OptiFine", StringComparison.Ordinal))
            {
                optiFinePath = library.Path;
            }
            else
            {
                classpathEntries.Add(library.Path);
            }
        }

        if (request.CustomHeadEntries is not null)
        {
            foreach (var customHeadEntry in request.CustomHeadEntries)
            {
                if (!string.IsNullOrWhiteSpace(customHeadEntry))
                {
                    classpathEntries.Insert(0, customHeadEntry);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(optiFinePath))
        {
            var insertIndex = Math.Max(0, classpathEntries.Count - 2);
            classpathEntries.Insert(insertIndex, optiFinePath);
        }

        return new MinecraftLaunchClasspathPlan(
            classpathEntries,
            string.Join(request.ClasspathSeparator, classpathEntries));
    }
}

public sealed record MinecraftLaunchClasspathLibrary(
    string? Name,
    string Path,
    bool IsNatives);

public sealed record MinecraftLaunchClasspathRequest(
    IReadOnlyList<MinecraftLaunchClasspathLibrary> Libraries,
    IReadOnlyList<string>? CustomHeadEntries,
    string? RetroWrapperPath,
    string ClasspathSeparator);

public sealed record MinecraftLaunchClasspathPlan(
    IReadOnlyList<string> Entries,
    string JoinedClasspath);

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PCL.Core.Utils.Processes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchProcessExecutionService
{
    public static ProcessStartRequest BuildCustomCommandStartRequest(MinecraftLaunchCustomCommandShellPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new ProcessStartRequest(plan.FileName)
        {
            Arguments = plan.Arguments,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = plan.UseShellExecute,
            CreateNoWindow = plan.CreateNoWindow
        };
    }

    public static ProcessStartRequest BuildGameProcessStartRequest(MinecraftLaunchProcessShellPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new ProcessStartRequest(plan.FileName)
        {
            Arguments = plan.Arguments,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = plan.UseShellExecute,
            CreateNoWindow = plan.CreateNoWindow,
            RedirectStandardOutput = plan.RedirectStandardOutput,
            RedirectStandardError = plan.RedirectStandardError,
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Path"] = plan.PathEnvironmentValue,
                ["appdata"] = plan.AppDataEnvironmentValue
            }
        };
    }

    public static bool TryApplyPriority(Process process, MinecraftLaunchProcessPriorityKind priorityKind)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            process.PriorityBoostEnabled = true;
            switch (priorityKind)
            {
                case MinecraftLaunchProcessPriorityKind.AboveNormal:
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case MinecraftLaunchProcessPriorityKind.BelowNormal:
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

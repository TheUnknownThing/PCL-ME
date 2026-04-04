using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashExportWorkflowService
{
    public static MinecraftCrashExportPlan CreatePlan(MinecraftCrashExportPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Environment);

        var sourceFiles = EnumerateSourceFiles(request.SourceFilePaths)
            .Concat(EnumerateSourceFiles(request.AdditionalSourceFilePaths))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new MinecraftCrashExportFile(path))
            .ToArray();

        var exportRequest = new MinecraftCrashExportRequest(
            request.ReportDirectory,
            request.LauncherVersionName,
            request.UniqueAddress,
            sourceFiles,
            request.CurrentLauncherLogFilePath,
            request.Environment,
            request.CurrentAccessToken,
            request.CurrentUserUuid,
            request.UserProfilePath);

        return new MinecraftCrashExportPlan(
            MinecraftCrashWorkflowService.GetSuggestedExportArchiveName(
                request.Timestamp,
                request.Culture),
            exportRequest);
    }

    private static IEnumerable<string> EnumerateSourceFiles(IReadOnlyList<string>? sourceFilePaths)
    {
        if (sourceFilePaths is null)
        {
            yield break;
        }

        foreach (var sourceFilePath in sourceFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                yield return sourceFilePath;
            }
        }
    }
}

public sealed record MinecraftCrashExportPlanRequest(
    DateTime Timestamp,
    string ReportDirectory,
    string LauncherVersionName,
    string UniqueAddress,
    IReadOnlyList<string> SourceFilePaths,
    IReadOnlyList<string>? AdditionalSourceFilePaths,
    string? CurrentLauncherLogFilePath,
    SystemEnvironmentSnapshot Environment,
    string? CurrentAccessToken,
    string? CurrentUserUuid,
    string? UserProfilePath,
    CultureInfo? Culture = null);

public sealed record MinecraftCrashExportPlan(
    string SuggestedArchiveName,
    MinecraftCrashExportRequest ExportRequest);

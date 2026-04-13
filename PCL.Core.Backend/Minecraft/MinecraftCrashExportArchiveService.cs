using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashExportArchiveService
{
    public static MinecraftCrashExportArchiveResult CreateArchive(MinecraftCrashExportArchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExportRequest);

        var archiveDirectory = Path.GetDirectoryName(request.ArchiveFilePath);
        if (!string.IsNullOrWhiteSpace(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
        }

        if (File.Exists(request.ArchiveFilePath))
        {
            File.Delete(request.ArchiveFilePath);
        }

        var exportResult = MinecraftCrashExportService.PrepareReportDirectory(request.ExportRequest);
        ZipFile.CreateFromDirectory(exportResult.ReportDirectory, request.ArchiveFilePath);
        Directory.Delete(exportResult.ReportDirectory, true);

        return new MinecraftCrashExportArchiveResult(
            request.ArchiveFilePath,
            exportResult.WrittenFiles
                .Select(Path.GetFileName)
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .ToArray()!);
    }
}

public sealed record MinecraftCrashExportArchiveRequest(
    string ArchiveFilePath,
    MinecraftCrashExportRequest ExportRequest);

public sealed record MinecraftCrashExportArchiveResult(
    string ArchiveFilePath,
    IReadOnlyList<string> ArchivedFileNames);

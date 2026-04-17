using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendShellActionService
{
    public FrontendCrashExportResult ExportCrashReport(CrashAvaloniaPlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        Directory.CreateDirectory(RuntimePaths.FrontendArtifactDirectory);
        Directory.CreateDirectory(RuntimePaths.FrontendTempDirectory);

        var archivePath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-exports",
            crashPlan.ExportPlan.SuggestedArchiveName));
        var tempRoot = Path.Combine(RuntimePaths.FrontendTempDirectory, "crash-export", Guid.NewGuid().ToString("N"));

        try
        {
            var exportRequest = MaterializeCrashExportRequest(crashPlan.ExportPlan.ExportRequest, tempRoot);
            var archiveResult = MinecraftCrashExportArchiveService.CreateArchive(new MinecraftCrashExportArchiveRequest(
                archivePath,
                exportRequest));
            return new FrontendCrashExportResult(
                archiveResult.ArchiveFilePath,
                archiveResult.ArchivedFileNames.Count);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public string MaterializeCrashLog(CrashAvaloniaPlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        var outputPath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-logs",
            "game-output.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var exportRequest = crashPlan.ExportPlan.ExportRequest;
        var builder = new StringBuilder()
            .AppendLine(I18n.T(crashPlan.OutputPrompt.Title))
            .AppendLine()
            .AppendLine(crashPlan.OutputPrompt.Message)
            .AppendLine()
            .AppendLine(I18n.T("crash.export.heading"))
            .AppendLine(I18n.T(
                "crash.export.suggested_archive",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["archive_name"] = crashPlan.ExportPlan.SuggestedArchiveName
                }))
            .AppendLine(I18n.T(
                "crash.export.source_file_count",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["file_count"] = exportRequest.SourceFiles.Count
                }));

        foreach (var sourceFile in exportRequest.SourceFiles)
        {
            builder.AppendLine($"- {sourceFile.SourcePath}");
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
        return outputPath;
    }

    private static string GetUniqueDirectoryPath(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return directoryPath;
        }

        var extension = 1;
        while (true)
        {
            var candidate = $"{directoryPath}-{extension}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }

            extension++;
        }
    }

    private static string GetUniqueFilePath(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        var suffix = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static MinecraftCrashExportRequest MaterializeCrashExportRequest(
        MinecraftCrashExportRequest request,
        string tempRoot)
    {
        var sourceRoot = Path.Combine(tempRoot, "source");
        var reportRoot = Path.Combine(tempRoot, "report");
        Directory.CreateDirectory(sourceRoot);

        var sourceFiles = request.SourceFiles
            .Select(file => new MinecraftCrashExportFile(MaterializeCrashSourceFile(file.SourcePath, sourceRoot)))
            .ToArray();

        return request with
        {
            ReportDirectory = reportRoot,
            SourceFiles = sourceFiles
        };
    }

    private static string MaterializeCrashSourceFile(string sourcePath, string sourceRoot)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var fileName = string.IsNullOrWhiteSpace(sourcePath)
            ? "missing-log.txt"
            : Path.GetFileName(sourcePath);
        var fallbackPath = Path.Combine(sourceRoot, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
        File.WriteAllText(
            fallbackPath,
            $"Placeholder crash artifact generated for missing source file: {sourcePath}",
            new UTF8Encoding(false));
        return fallbackPath;
    }
}

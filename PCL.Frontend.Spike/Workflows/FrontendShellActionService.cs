using System.Diagnostics;
using System.Text;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal sealed class FrontendShellActionService(FrontendRuntimePaths runtimePaths, Action exitLauncher)
{
    public FrontendRuntimePaths RuntimePaths { get; } = runtimePaths;

    public void AcceptLauncherEula()
    {
        PersistSharedValue("SystemEula", true);
    }

    public void SetTelemetryEnabled(bool enabled)
    {
        PersistSharedValue("SystemTelemetry", enabled);
    }

    public void DisableNonAsciiGamePathWarning()
    {
        PersistSharedValue("HintDisableGamePathCheckTip", true);
    }

    public void ExitLauncher()
    {
        exitLauncher();
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "缺少可打开的目标。";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public FrontendCrashExportResult ExportCrashReport(CrashSpikePlan crashPlan)
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

    public string MaterializeCrashLog(CrashSpikePlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        var outputPath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-logs",
            "game-output.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var exportRequest = crashPlan.ExportPlan.ExportRequest;
        var builder = new StringBuilder()
            .AppendLine(crashPlan.OutputPrompt.Title)
            .AppendLine()
            .AppendLine(crashPlan.OutputPrompt.Message)
            .AppendLine()
            .AppendLine("导出计划")
            .AppendLine($"- 建议压缩包: {crashPlan.ExportPlan.SuggestedArchiveName}")
            .AppendLine($"- 源文件数量: {exportRequest.SourceFiles.Count}");

        foreach (var sourceFile in exportRequest.SourceFiles)
        {
            builder.AppendLine($"- {sourceFile.SourcePath}");
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
        return outputPath;
    }

    public FrontendJavaRuntimeInstallResult MaterializeJavaRuntime(LaunchSpikePlan launchPlan)
    {
        ArgumentNullException.ThrowIfNull(launchPlan);

        var manifestPlan = launchPlan.JavaRuntimeManifestPlan
                           ?? throw new InvalidOperationException("当前启动方案没有可执行的 Java 下载计划。");
        var transferPlan = launchPlan.JavaRuntimeTransferPlan
                           ?? throw new InvalidOperationException("当前启动方案没有可执行的 Java 传输计划。");

        var runtimeDirectory = GetUniqueDirectoryPath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "java-runtimes",
            SanitizePathSegment($"{manifestPlan.Selection.ComponentKey}-{manifestPlan.Selection.VersionName}")));
        var summaryPath = Path.Combine(runtimeDirectory, "download-summary.txt");

        Directory.CreateDirectory(runtimeDirectory);

        foreach (var file in transferPlan.ReusedFiles)
        {
            WriteJavaRuntimeFile(runtimeDirectory, file.RelativePath, $"Reused placeholder for {file.RelativePath}");
        }

        foreach (var file in transferPlan.FilesToDownload)
        {
            var content = $"""
                Download placeholder for {file.RelativePath}
                SHA1: {file.Sha1}
                Official URLs:
                {string.Join(Environment.NewLine, file.RequestUrls.OfficialUrls)}

                Mirror URLs:
                {string.Join(Environment.NewLine, file.RequestUrls.MirrorUrls)}
                """;
            WriteJavaRuntimeFile(runtimeDirectory, file.RelativePath, content);
        }

        var summary = $"""
            Java runtime: {manifestPlan.Selection.VersionName}
            Component: {manifestPlan.Selection.ComponentKey}
            Runtime directory: {runtimeDirectory}
            Download files: {transferPlan.FilesToDownload.Count}
            Reused files: {transferPlan.ReusedFiles.Count}
            Total bytes planned: {transferPlan.DownloadBytes}
            """;
        File.WriteAllText(summaryPath, summary, new UTF8Encoding(false));

        return new FrontendJavaRuntimeInstallResult(
            manifestPlan.Selection.VersionName,
            runtimeDirectory,
            transferPlan.FilesToDownload.Count,
            transferPlan.ReusedFiles.Count,
            summaryPath);
    }

    private void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        provider.Set(key, value);
        provider.Sync();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary frontend artifacts.
        }
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

    private static void WriteJavaRuntimeFile(string runtimeDirectory, string relativePath, string content)
    {
        var segments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var filePath = Path.Combine([runtimeDirectory, ..segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }

    private static string SanitizePathSegment(string raw)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "artifact" : cleaned;
    }
}

internal sealed record FrontendCrashExportResult(
    string ArchivePath,
    int ArchivedFileCount);

internal sealed record FrontendJavaRuntimeInstallResult(
    string VersionName,
    string RuntimeDirectory,
    int DownloadedFileCount,
    int ReusedFileCount,
    string SummaryPath);

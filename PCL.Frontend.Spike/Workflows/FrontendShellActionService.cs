using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
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

    public void PersistLocalValue<T>(string key, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = new YamlFileProvider(RuntimePaths.LocalConfigPath);
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistProtectedSharedValue(string key, string value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        provider.Set(key, ProtectSharedValue(value));
        provider.Sync();
    }

    public void RemoveLocalValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = new YamlFileProvider(RuntimePaths.LocalConfigPath);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void RemoveSharedValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void PersistInstanceValue<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = OpenInstanceConfigProvider(instanceDirectory);
        provider.Set(key, value);
        provider.Sync();
    }

    public void RemoveInstanceValues(string instanceDirectory, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = OpenInstanceConfigProvider(instanceDirectory);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
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

    public async Task<string?> PickOpenFileAsync(string title, string typeName, params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "当前环境不支持文件选择器。");
        var fileTypes = patterns.Length == 0
            ? null
            : new List<FilePickerFileType>
            {
                new(typeName)
                {
                    Patterns = patterns
                }
            };

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
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

    public FrontendJavaRuntimeInstallResult MaterializeJavaRuntime(
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        ArgumentNullException.ThrowIfNull(manifestPlan);
        ArgumentNullException.ThrowIfNull(transferPlan);

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

    private static IStorageProvider? TryGetStorageProvider(out string? error)
    {
        error = null;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.StorageProvider is null)
        {
            error = "当前壳层未提供文件选择器。";
            return null;
        }

        return desktop.MainWindow.StorageProvider;
    }

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        var configPath = Path.Combine(pclDirectory, "config.v1.yml");
        if (!File.Exists(configPath))
        {
            var legacyPath = Path.Combine(pclDirectory, "Setup.ini");
            if (File.Exists(legacyPath))
            {
                Directory.CreateDirectory(pclDirectory);
                var provider = new YamlFileProvider(configPath);
                foreach (var line in File.ReadLines(legacyPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var splitIndex = line.IndexOf(':');
                    if (splitIndex <= 0)
                    {
                        continue;
                    }

                    provider.Set(line[..splitIndex], line[(splitIndex + 1)..]);
                }

                provider.Sync();
            }
        }

        return new YamlFileProvider(configPath);
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

    private string ProtectSharedValue(string plainText)
    {
        var encryptionKey = ResolveSharedEncryptionKey();
        return LauncherDataProtectionService.Protect(plainText, encryptionKey);
    }

    private byte[] ResolveSharedEncryptionKey()
    {
        var explicitKey = LauncherSecretKeyResolutionService.ParseExplicitKeyOverride(
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
        if (explicitKey is not null)
        {
            return explicitKey;
        }

        var persistedKeyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(RuntimePaths.SharedConfigDirectory);
        var persistedEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(persistedKeyPath)
            ?? throw new InvalidOperationException("Launcher encryption key is unavailable.");
        var storedKey = LauncherVersionedDataService.Parse(persistedEnvelope);
        return storedKey.Version switch
        {
            1 => ReadWindowsProtectedKey(storedKey.Data),
            2 => storedKey.Data,
            _ => throw new NotSupportedException("Unsupported launcher key version.")
        };
    }

    private static byte[] ReadWindowsProtectedKey(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected launcher keys are only supported on Windows.");
        }

        return ProtectedData.Unprotect(data, Encoding.UTF8.GetBytes("PCL CE Encryption Key"), DataProtectionScope.CurrentUser);
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

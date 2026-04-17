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
    public FrontendJavaRuntimeInstallResult MaterializeJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installPlan);

        return installPlan.Kind switch
        {
            FrontendJavaRuntimeInstallPlanKind.MojangManifest => MaterializeMojangJavaRuntime(
                installPlan,
                onProgress,
                cancellationToken),
            FrontendJavaRuntimeInstallPlanKind.ArchivePackage => MaterializeArchiveJavaRuntime(
                installPlan,
                onProgress,
                cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported Java download plan: {installPlan.Kind}")
        };
    }

    private FrontendJavaRuntimeInstallResult MaterializeMojangJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress,
        CancellationToken cancellationToken)
    {
        var manifestPlan = installPlan.MojangManifestPlan
                           ?? throw new InvalidOperationException("The Mojang Java download plan is missing a manifest.");
        var transferPlan = installPlan.MojangTransferPlan
                           ?? throw new InvalidOperationException("The Mojang Java download plan is missing a transfer plan.");
        var effectiveTransferPlan = ResolveEffectiveJavaTransferPlan(manifestPlan, transferPlan);
        var runtimeDirectory = effectiveTransferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory;
        var downloadProvider = GetDownloadProvider();
        var speedLimiter = GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;

        Directory.CreateDirectory(runtimeDirectory);

        var totalFileCount = effectiveTransferPlan.FilesToDownload.Count;
        var completedFileCount = 0;
        var downloadedBytes = 0L;
        onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
            FrontendJavaRuntimeInstallPhase.Prepare,
            string.Empty,
            completedFileCount,
            totalFileCount,
            downloadedBytes,
            effectiveTransferPlan.DownloadBytes,
            0d));
        foreach (var file in effectiveTransferPlan.FilesToDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lastReportedBytes = 0L;
            var lastReportedAt = Environment.TickCount64;
            DownloadJavaRuntimeFile(
                file,
                downloadProvider,
                speedLimiter,
                transferredBytes =>
                {
                    var now = Environment.TickCount64;
                    if (now - lastReportedAt < 250)
                    {
                        return;
                    }

                    var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                    var speed = (transferredBytes - lastReportedBytes) / elapsedSeconds;
                    lastReportedBytes = transferredBytes;
                    lastReportedAt = now;
                    onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                        FrontendJavaRuntimeInstallPhase.Download,
                        file.RelativePath,
                        completedFileCount,
                        totalFileCount,
                        downloadedBytes + transferredBytes,
                        effectiveTransferPlan.DownloadBytes,
                        speed));
                },
                cancellationToken);
            downloadedBytes += file.Size;
            completedFileCount++;
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                FrontendJavaRuntimeInstallPhase.Download,
                file.RelativePath,
                completedFileCount,
                totalFileCount,
                downloadedBytes,
                effectiveTransferPlan.DownloadBytes,
                0d));
        }

        onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
            FrontendJavaRuntimeInstallPhase.Finalize,
            "Applying Java runtime",
            completedFileCount,
            totalFileCount,
            downloadedBytes,
            effectiveTransferPlan.DownloadBytes,
            0d));
        EnsureUnixExecutableBits(runtimeDirectory, effectiveTransferPlan);
        var summaryPath = WriteJavaRuntimeSummary(
            installPlan,
            runtimeDirectory,
            effectiveTransferPlan.FilesToDownload.Count,
            effectiveTransferPlan.ReusedFiles.Count,
            effectiveTransferPlan.DownloadBytes);
        return new FrontendJavaRuntimeInstallResult(
            installPlan.VersionName,
            runtimeDirectory,
            effectiveTransferPlan.FilesToDownload.Count,
            effectiveTransferPlan.ReusedFiles.Count,
            summaryPath);
    }

    private FrontendJavaRuntimeInstallResult MaterializeArchiveJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress,
        CancellationToken cancellationToken)
    {
        var archivePlan = installPlan.ArchivePlan
                          ?? throw new InvalidOperationException("The archive Java download plan is missing archive metadata.");
        var runtimeDirectory = installPlan.RuntimeDirectory;
        var downloadProvider = GetDownloadProvider();
        var speedLimiter = GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        var stagingDirectory = runtimeDirectory + ".staging";
        var extractDirectory = Path.Combine(stagingDirectory, "extract");
        var archivePath = Path.Combine(stagingDirectory, archivePlan.PackageName);

        try
        {
            TryDeleteDirectory(stagingDirectory);
            Directory.CreateDirectory(stagingDirectory);
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                FrontendJavaRuntimeInstallPhase.Prepare,
                archivePlan.PackageName,
                0,
                1,
                0L,
                archivePlan.Size,
                0d));

            var lastReportedBytes = 0L;
            var lastReportedAt = Environment.TickCount64;
            DownloadJavaRuntimeArchivePackage(
                archivePlan,
                archivePath,
                downloadProvider,
                speedLimiter,
                transferredBytes =>
                {
                    var now = Environment.TickCount64;
                    if (now - lastReportedAt < 250)
                    {
                        return;
                    }

                    var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                    var speed = (transferredBytes - lastReportedBytes) / elapsedSeconds;
                    lastReportedBytes = transferredBytes;
                    lastReportedAt = now;
                    onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                        FrontendJavaRuntimeInstallPhase.Download,
                        archivePlan.PackageName,
                        0,
                        1,
                        transferredBytes,
                        archivePlan.Size,
                        speed));
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                FrontendJavaRuntimeInstallPhase.Finalize,
                "Extracting Java runtime",
                0,
                1,
                archivePlan.Size,
                archivePlan.Size,
                0d));

            ExtractJavaRuntimeArchive(archivePath, extractDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            var extractedRoot = ResolveExtractedJavaRuntimeRoot(extractDirectory)
                                ?? throw new InvalidOperationException("No usable Java runtime directory was found in the archive.");

            TryDeleteDirectory(runtimeDirectory);
            Directory.CreateDirectory(runtimeDirectory);
            MoveDirectoryContents(extractedRoot, runtimeDirectory);
            TryDeleteDirectory(stagingDirectory);
            EnsureUnixExecutableBits(runtimeDirectory);

            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                FrontendJavaRuntimeInstallPhase.Finalize,
                archivePlan.PackageName,
                1,
                1,
                archivePlan.Size,
                archivePlan.Size,
                0d));

            var summaryPath = WriteJavaRuntimeSummary(
                installPlan,
                runtimeDirectory,
                downloadedFileCount: 1,
                reusedFileCount: 0,
                totalBytes: archivePlan.Size);
            return new FrontendJavaRuntimeInstallResult(
                installPlan.VersionName,
                runtimeDirectory,
                1,
                0,
                summaryPath);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    public string GetJavaExecutablePath(string runtimeDirectory) => PlatformAdapter.GetJavaExecutablePath(runtimeDirectory);

    public IReadOnlyList<string> GetDefaultJavaDetectionCandidates() => PlatformAdapter.GetDefaultJavaDetectionCandidates();

    private MinecraftJavaRuntimeDownloadTransferPlan ResolveEffectiveJavaTransferPlan(
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        var hasPlaceholderUrls = transferPlan.FilesToDownload.Any(file =>
            file.RequestUrls.AllUrls.Any(url => url.Contains("example.invalid", StringComparison.OrdinalIgnoreCase))) ||
                                 manifestPlan.RequestUrls.AllUrls.Any(url => url.Contains("example.invalid", StringComparison.OrdinalIgnoreCase));
        if (!hasPlaceholderUrls)
        {
            return transferPlan;
        }

        var downloadProvider = GetDownloadProvider();
        var indexJson = DownloadUtf8String(downloadProvider.GetPreferredUrls(
            MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().OfficialUrls,
            MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().MirrorUrls));
        var liveManifestPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            new MinecraftJavaRuntimeManifestRequestPlanRequest(
                indexJson,
                manifestPlan.Selection.PlatformKey,
                manifestPlan.Selection.RequestedComponent,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        var manifestJson = DownloadUtf8String(downloadProvider.GetPreferredUrls(
            liveManifestPlan.RequestUrls.OfficialUrls,
            liveManifestPlan.RequestUrls.MirrorUrls));
        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestJson,
                transferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory,
                MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes(),
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
        var existingRelativePaths = workflowPlan.Files
            .Where(file => File.Exists(file.TargetPath))
            .Select(file => file.RelativePath)
            .ToArray();

        return MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
            new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                workflowPlan,
                existingRelativePaths));
    }

    private static string DownloadUtf8String(IReadOnlyList<string> urls)
    {
        Exception? lastError = null;
        foreach (var url in urls)
        {
            try
            {
                return JavaRuntimeHttpClient.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException("Unable to download Java runtime metadata.", lastError);
    }

    private static void DownloadJavaRuntimeFile(
        MinecraftJavaRuntimeDownloadRequestFilePlan file,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        Action<long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);

        Exception? lastError = null;
        foreach (var url in downloadProvider.GetPreferredUrls(file.RequestUrls.OfficialUrls, file.RequestUrls.MirrorUrls))
        {
            try
            {
                var tempPath = file.TargetPath + ".download";
                FrontendDownloadTransferService.DownloadToPath(
                    JavaRuntimeHttpClient,
                    url,
                    tempPath,
                    onProgress,
                    speedLimiter: speedLimiter,
                    cancelToken: cancellationToken);
                var sha1 = ComputeSha1FromFile(tempPath);
                if (!string.Equals(sha1, file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(tempPath);
                    throw new InvalidOperationException($"Java file verification failed: {file.RelativePath}");
                }

                File.Move(tempPath, file.TargetPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(file.TargetPath + ".download");
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(file.TargetPath + ".download");
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download Java file: {file.RelativePath}", lastError);
    }

    private static void DownloadJavaRuntimeArchivePackage(
        FrontendJavaRuntimeArchiveDownloadPlan archivePlan,
        string targetPath,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        Action<long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        Exception? lastError = null;
        foreach (var url in downloadProvider.GetPreferredUrls(archivePlan.RequestUrls.OfficialUrls, archivePlan.RequestUrls.MirrorUrls))
        {
            try
            {
                var tempPath = targetPath + ".download";
                FrontendDownloadTransferService.DownloadToPath(
                    JavaRuntimeHttpClient,
                    url,
                    tempPath,
                    onProgress,
                    speedLimiter: speedLimiter,
                    cancelToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(archivePlan.Sha256))
                {
                    var sha256 = ComputeSha256FromFile(tempPath);
                    if (!string.Equals(sha256, archivePlan.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteFile(tempPath);
                        throw new InvalidOperationException($"Java archive validation failed: {archivePlan.PackageName}");
                    }
                }

                File.Move(tempPath, targetPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(targetPath + ".download");
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(targetPath + ".download");
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download Java archive: {archivePlan.PackageName}", lastError);
    }

    private static string ComputeSha1FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeSha256FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ExtractJavaRuntimeArchive(string archivePath, string extractDirectory)
    {
        TryDeleteDirectory(extractDirectory);
        Directory.CreateDirectory(extractDirectory);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDirectory, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            using var archiveStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzipStream, extractDirectory, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException($"Unsupported Java archive format: {Path.GetFileName(archivePath)}");
    }

    private string? ResolveExtractedJavaRuntimeRoot(string extractDirectory)
    {
        if (HasJavaExecutable(extractDirectory))
        {
            return extractDirectory;
        }

        var childDirectories = Directory.Exists(extractDirectory)
            ? Directory.EnumerateDirectories(extractDirectory).ToArray()
            : [];
        if (childDirectories.Length == 1 && HasJavaExecutable(childDirectories[0]))
        {
            return childDirectories[0];
        }

        return childDirectories.FirstOrDefault(HasJavaExecutable);
    }

    private bool HasJavaExecutable(string runtimeDirectory)
    {
        return File.Exists(GetJavaExecutablePath(runtimeDirectory));
    }

    private static void MoveDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(directory));
            Directory.Move(directory, targetPath);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Move(file, targetPath, overwrite: true);
        }
    }

    private static void EnsureUnixExecutableBits(
        string runtimeDirectory,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var file in transferPlan.WorkflowPlan.Files)
        {
            if (!RequiresUnixExecutableBits(file.RelativePath))
            {
                continue;
            }

            if (!File.Exists(file.TargetPath))
            {
                continue;
            }

            ApplyUnixExecutableBits(file.TargetPath);
        }
    }

    private void EnsureUnixExecutableBits(string runtimeDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var path in EnumerateUnixExecutablePaths(runtimeDirectory))
        {
            ApplyUnixExecutableBits(path);
        }
    }

    internal static bool RequiresUnixExecutableBits(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        return normalizedPath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.EndsWith("/jspawnhelper", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedPath, "jspawnhelper", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateUnixExecutablePaths(string runtimeDirectory)
    {
        if (!Directory.Exists(runtimeDirectory))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(runtimeDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(runtimeDirectory, filePath);
            if (RequiresUnixExecutableBits(relativePath))
            {
                yield return filePath;
            }
        }
    }

    private static void ApplyUnixExecutableBits(string path)
    {
        if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
    }

    private static string WriteJavaRuntimeSummary(
        FrontendJavaRuntimeInstallPlan installPlan,
        string runtimeDirectory,
        int downloadedFileCount,
        int reusedFileCount,
        long totalBytes)
    {
        var summaryPath = Path.Combine(runtimeDirectory, "download-summary.txt");
        var summary = $"""
            Java runtime: {installPlan.VersionName}
            Source: {installPlan.SourceName}
            Requested component: {installPlan.RequestedComponent}
            Platform: {installPlan.PlatformKey}
            Runtime directory: {runtimeDirectory}
            Download files: {downloadedFileCount}
            Reused files: {reusedFileCount}
            Total bytes planned: {totalBytes}
            """;
        File.WriteAllText(summaryPath, summary, new UTF8Encoding(false));
        return summaryPath;
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

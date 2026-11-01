using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendUpdateInstallWorkflowService
{
    private static readonly HttpClient HttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromMinutes(5));

    public static async Task<FrontendPreparedUpdateInstall> PrepareAsync(
        FrontendUpdateInstallRequest request,
        Action<FrontendUpdateInstallProgressSnapshot>? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Prepare,
            string.Empty,
            0L,
            0L,
            0d));

        var platform = DetectCurrentPlatform();
        var layout = ResolveInstallLayout(platform, request.ExecutableDirectory, request.ProcessPath);
        var artifactDirectory = Path.Combine(request.ArtifactDirectory, "update-downloads");
        var tempRoot = Path.Combine(request.TempDirectory, "update-install", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);
        Directory.CreateDirectory(tempRoot);

        var archiveExtension = ResolveArchiveExtension(request.DownloadUrl);
        var archivePath = Path.Combine(
            artifactDirectory,
            $"{request.ReleaseFileStem}{archiveExtension}");
        await DownloadArchiveAsync(request.DownloadUrl, archivePath, progressReporter, cancellationToken);
        var archiveLength = new FileInfo(archivePath).Length;
        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Verify,
            archivePath,
            archiveLength,
            archiveLength,
            0d));
        if (!string.IsNullOrWhiteSpace(request.ExpectedSha256))
        {
            VerifyArchiveHash(archivePath, request.ExpectedSha256);
        }

        var extractedRoot = Path.Combine(tempRoot, "extracted");
        Directory.CreateDirectory(extractedRoot);
        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Extract,
            archivePath,
            archiveLength,
            archiveLength,
            0d));
        ExtractArchive(archivePath, extractedRoot);

        var packageRoot = ResolveExtractedPackageRoot(platform, extractedRoot, layout.CurrentPackageName);
        var scriptPath = Path.Combine(
            artifactDirectory,
            $"{request.ReleaseFileStem}-apply{GetScriptExtension(platform)}");
        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Finalize,
            archivePath,
            archiveLength,
            archiveLength,
            0d));
        WriteInstallerScript(scriptPath, BuildInstallerScript(platform, layout, packageRoot, request.ProcessId));
        if (platform is not FrontendUpdateInstallPlatform.Windows)
        {
            request.PlatformAdapter.EnsureFileExecutable(scriptPath);
        }

        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Completed,
            archivePath,
            archiveLength,
            archiveLength,
            0d));

        return new FrontendPreparedUpdateInstall(
            ArchivePath: archivePath,
            ExtractedPackagePath: packageRoot,
            InstallerScriptPath: scriptPath,
            Layout: layout);
    }

    internal static FrontendUpdateInstallPlatform DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return FrontendUpdateInstallPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return FrontendUpdateInstallPlatform.MacOS;
        }

        return FrontendUpdateInstallPlatform.Linux;
    }

    internal static FrontendUpdateInstallLayout ResolveInstallLayout(
        FrontendUpdateInstallPlatform platform,
        string executableDirectory,
        string? processPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);

        var normalizedExecutableDirectory = NormalizePath(executableDirectory);
        var normalizedProcessPath = NormalizePath(processPath ?? Environment.ProcessPath ?? Path.Combine(
            normalizedExecutableDirectory,
            platform == FrontendUpdateInstallPlatform.Windows ? "PCL.Frontend.Avalonia.exe" : "PCL.Frontend.Avalonia"));

        if (platform == FrontendUpdateInstallPlatform.MacOS
            && TryGetMacAppBundlePath(normalizedExecutableDirectory, out var appBundlePath))
        {
            return new FrontendUpdateInstallLayout(
                FrontendUpdateInstallTargetKind.MacAppBundle,
                InstallDirectory: Path.GetDirectoryName(appBundlePath)!,
                TargetPath: appBundlePath,
                RestartTargetPath: appBundlePath,
                CurrentPackageName: Path.GetFileName(appBundlePath));
        }

        return new FrontendUpdateInstallLayout(
            FrontendUpdateInstallTargetKind.Directory,
            InstallDirectory: normalizedExecutableDirectory,
            TargetPath: normalizedExecutableDirectory,
            RestartTargetPath: normalizedProcessPath,
            CurrentPackageName: Path.GetFileName(normalizedExecutableDirectory));
    }

    internal static string ResolveExtractedPackageRoot(
        FrontendUpdateInstallPlatform platform,
        string extractedRoot,
        string? currentPackageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractedRoot);

        var normalizedExtractedRoot = NormalizePath(extractedRoot);
        if (!Directory.Exists(normalizedExtractedRoot))
        {
            throw new DirectoryNotFoundException($"Update extraction directory was not found: {normalizedExtractedRoot}");
        }

        if (platform == FrontendUpdateInstallPlatform.MacOS)
        {
            var appBundlePath = Directory.EnumerateDirectories(normalizedExtractedRoot, "*.app", SearchOption.AllDirectories)
                .OrderBy(path => GetRelativePathDepth(normalizedExtractedRoot, path))
                .FirstOrDefault(path => string.IsNullOrWhiteSpace(currentPackageName)
                    || string.Equals(Path.GetFileName(path), currentPackageName, StringComparison.OrdinalIgnoreCase))
                ?? Directory.EnumerateDirectories(normalizedExtractedRoot, "*.app", SearchOption.AllDirectories)
                    .OrderBy(path => GetRelativePathDepth(normalizedExtractedRoot, path))
                    .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(appBundlePath))
            {
                return appBundlePath;
            }
        }

        var children = Directory.EnumerateFileSystemEntries(normalizedExtractedRoot, "*", SearchOption.TopDirectoryOnly)
            .ToArray();
        if (children.Length == 1 && Directory.Exists(children[0]))
        {
            return children[0];
        }

        return normalizedExtractedRoot;
    }

    internal static string BuildInstallerScript(
        FrontendUpdateInstallPlatform platform,
        FrontendUpdateInstallLayout layout,
        string packageRoot,
        int processId)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);

        return platform switch
        {
            FrontendUpdateInstallPlatform.Windows => BuildWindowsInstallerScript(layout, packageRoot, processId),
            _ => BuildUnixInstallerScript(layout, packageRoot, processId)
        };
    }

    private static async Task DownloadArchiveAsync(
        string url,
        string archivePath,
        Action<FrontendUpdateInstallProgressSnapshot>? progressReporter,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(archivePath);
        var lastReportedBytes = 0L;
        var lastReportedAt = Environment.TickCount64;
        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Download,
            archivePath,
            0L,
            contentLength,
            0d));
        await FrontendDownloadTransferService.CopyAsync(
            responseStream,
            fileStream,
            totalRead =>
            {
                var now = Environment.TickCount64;
                if (now - lastReportedAt < 250)
                {
                    return;
                }

                var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                lastReportedAt = now;
                lastReportedBytes = totalRead;
                progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
                    FrontendUpdateInstallProgressPhase.Download,
                    archivePath,
                    totalRead,
                    contentLength,
                    speed));
            },
            stalledTransferTimeout: TimeSpan.FromMinutes(5),
            cancelToken: cancellationToken);
        progressReporter?.Invoke(new FrontendUpdateInstallProgressSnapshot(
            FrontendUpdateInstallProgressPhase.Download,
            archivePath,
            new FileInfo(archivePath).Length,
            contentLength,
            0d));
    }

    private static void VerifyArchiveHash(string archivePath, string expectedSha256)
    {
        using var stream = File.OpenRead(archivePath);
        var hash = SHA256.HashData(stream);
        var actual = Convert.ToHexString(hash);
        if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Update package verification failed. Expected SHA256 {expectedSha256}, actual {actual}.");
        }
    }

    private static void ExtractArchive(string archivePath, string extractedRoot)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractedRoot, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using var archiveStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzipStream, extractedRoot, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException($"Unsupported update archive format: {Path.GetFileName(archivePath)}");
    }

    private static void WriteInstallerScript(string scriptPath, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, contents, new UTF8Encoding(false));
    }

    private static string BuildWindowsInstallerScript(
        FrontendUpdateInstallLayout layout,
        string packageRoot,
        int processId)
    {
        var workingDirectory = Path.GetDirectoryName(layout.RestartTargetPath) ?? layout.InstallDirectory;
        return $"""
            @echo off
            setlocal
            set "PID={processId}"
            set "SOURCE={QuoteForWindows(packageRoot)}"
            set "TARGET={QuoteForWindows(layout.TargetPath)}"
            set "LAUNCH={QuoteForWindows(layout.RestartTargetPath)}"
            set "WORKDIR={QuoteForWindows(workingDirectory)}"
            :wait
            tasklist /FI "PID eq %PID%" | find "%PID%" >nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak >nul
                goto wait
            )
            robocopy "%SOURCE%" "%TARGET%" /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 >nul
            set "RC=%ERRORLEVEL%"
            if %RC% GEQ 8 exit /b %RC%
            start "" /D "%WORKDIR%" "%LAUNCH%"
            exit /b 0
            """;
    }

    private static string BuildUnixInstallerScript(
        FrontendUpdateInstallLayout layout,
        string packageRoot,
        int processId)
    {
        if (layout.TargetKind == FrontendUpdateInstallTargetKind.MacAppBundle)
        {
            return $"""
                #!/bin/sh
                set -eu
                PID="{processId}"
                SOURCE={QuoteForPosix(packageRoot)}
                TARGET={QuoteForPosix(layout.TargetPath)}
                while kill -0 "$PID" 2>/dev/null; do
                    sleep 1
                done
                rm -rf "$TARGET"
                cp -R "$SOURCE" "$TARGET"
                open "$TARGET" >/dev/null 2>&1 &
                """;
        }

        return $"""
            #!/bin/sh
            set -eu
            PID="{processId}"
            SOURCE={QuoteForPosix(packageRoot)}
            TARGET={QuoteForPosix(layout.TargetPath)}
            LAUNCH={QuoteForPosix(layout.RestartTargetPath)}
            while kill -0 "$PID" 2>/dev/null; do
                sleep 1
            done
            rm -rf "$TARGET"
            mkdir -p "$TARGET"
            cp -R "$SOURCE"/. "$TARGET"/
            chmod +x "$LAUNCH" || true
            nohup "$LAUNCH" >/dev/null 2>&1 &
            """;
    }

    private static string ResolveArchiveExtension(string url)
    {
        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".tar.gz";
        }

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ".zip";
        }

        throw new InvalidOperationException($"Update download URL does not contain a supported archive extension: {url}");
    }

    private static string GetScriptExtension(FrontendUpdateInstallPlatform platform)
    {
        return platform == FrontendUpdateInstallPlatform.Windows ? ".cmd" : ".sh";
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static int GetRelativePathDepth(string rootPath, string targetPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, targetPath);
        return relativePath.Count(character => character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar);
    }

    private static bool TryGetMacAppBundlePath(string executableDirectory, out string appBundlePath)
    {
        appBundlePath = string.Empty;
        var normalizedDirectory = NormalizePath(executableDirectory);
        var contentsDirectory = Path.GetDirectoryName(normalizedDirectory);
        if (string.IsNullOrWhiteSpace(contentsDirectory)
            || !string.Equals(Path.GetFileName(contentsDirectory), "Contents", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(normalizedDirectory), "MacOS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bundlePath = Path.GetDirectoryName(contentsDirectory);
        if (string.IsNullOrWhiteSpace(bundlePath)
            || !bundlePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        appBundlePath = bundlePath;
        return true;
    }

    private static string QuoteForWindows(string path)
    {
        return path.Replace("\"", "\"\"");
    }

    private static string QuoteForPosix(string path)
    {
        return $"'{path.Replace("'", "'\"'\"'")}'";
    }
}

internal enum FrontendUpdateInstallPlatform
{
    Windows = 0,
    MacOS = 1,
    Linux = 2
}

internal enum FrontendUpdateInstallTargetKind
{
    Directory = 0,
    MacAppBundle = 1
}

internal sealed record FrontendUpdateInstallRequest(
    string DownloadUrl,
    string ReleaseFileStem,
    string? ExpectedSha256,
    string ArtifactDirectory,
    string TempDirectory,
    string ExecutableDirectory,
    string? ProcessPath,
    int ProcessId,
    FrontendPlatformAdapter PlatformAdapter);

internal sealed record FrontendUpdateInstallLayout(
    FrontendUpdateInstallTargetKind TargetKind,
    string InstallDirectory,
    string TargetPath,
    string RestartTargetPath,
    string CurrentPackageName);

internal sealed record FrontendPreparedUpdateInstall(
    string ArchivePath,
    string ExtractedPackagePath,
    string InstallerScriptPath,
    FrontendUpdateInstallLayout Layout);

internal enum FrontendUpdateInstallProgressPhase
{
    Prepare = 0,
    Download = 1,
    Verify = 2,
    Extract = 3,
    Finalize = 4,
    Completed = 5
}

internal sealed record FrontendUpdateInstallProgressSnapshot(
    FrontendUpdateInstallProgressPhase Phase,
    string ArchivePath,
    long DownloadedBytes,
    long TotalBytes,
    double SpeedBytesPerSecond);

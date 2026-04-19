using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static async Task<bool> EnsurePackFileAsync(
        FrontendModpackFileDownloadPlan file,
        int communitySourcePreference,
        HttpClient httpClient,
        FrontendDownloadTransferOptions? downloadOptions,
        FrontendDownloadSpeedLimiter? speedLimiter,
        CancellationToken cancelToken,
        II18nService? i18n)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);
        if (File.Exists(file.TargetPath) && ValidateExistingFile(file))
        {
            return false;
        }

        foreach (var url in BuildPreferredDownloadUrls(file.DownloadUrls, communitySourcePreference))
        {
            cancelToken.ThrowIfCancellationRequested();
            var tempFile = file.TargetPath + ".pcltmp";
            try
            {
                TryDeleteFile(tempFile);
                await FrontendDownloadTransferService.DownloadToPathAsync(
                        httpClient,
                        url,
                        tempFile,
                        speedLimiter: speedLimiter,
                        stalledTransferTimeout: downloadOptions?.StalledTransferTimeout,
                        maxAttempts: downloadOptions?.MaxFileDownloadAttempts ?? 3,
                        cancelToken: cancelToken)
                    .ConfigureAwait(false);

                if (!ValidateDownloadedFile(tempFile, file))
                {
                    TryDeleteFile(tempFile);
                    continue;
                }

                if (File.Exists(file.TargetPath))
                {
                    File.Delete(file.TargetPath);
                }

                File.Move(tempFile, file.TargetPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(tempFile);
                throw;
            }
            catch
            {
                TryDeleteFile(tempFile);
                // Try the next candidate URL.
            }
        }

        throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.file_download_failed", ("file_name", file.DisplayName)));
    }

    private static bool ValidateExistingFile(FrontendModpackFileDownloadPlan file)
    {
        var info = new FileInfo(file.TargetPath);
        if (file.Size is long expectedSize && info.Exists && info.Length != expectedSize)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.Sha1)
            || string.Equals(ComputeSha1(file.TargetPath), file.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateDownloadedFile(string path, FrontendModpackFileDownloadPlan file)
    {
        var info = new FileInfo(path);
        if (file.Size is long expectedSize && info.Length != expectedSize)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.Sha1)
            || string.Equals(ComputeSha1(path), file.Sha1, StringComparison.OrdinalIgnoreCase);
    }
    private static void ExtractArchiveToDirectory(
        string archivePath,
        string destinationDirectory,
        Action<double>? onProgress,
        CancellationToken cancelToken)
    {
        using var archive = OpenArchiveRead(archivePath);
        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = NormalizeDirectoryRoot(destinationDirectory);
        var totalEntries = Math.Max(archive.Entries.Count, 1);

        for (var index = 0; index < archive.Entries.Count; index += 1)
        {
            cancelToken.ThrowIfCancellationRequested();
            var entry = archive.Entries[index];
            var normalizedEntryPath = entry.FullName
                .Replace('\\', '/')
                .TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedEntryPath))
            {
                onProgress?.Invoke((index + 1d) / totalEntries);
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedEntryPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathWithinDirectory(destinationPath, destinationRoot))
            {
                throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.archive_illegal_path", ("entry_path", entry.FullName)));
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                onProgress?.Invoke((index + 1d) / totalEntries);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
            onProgress?.Invoke((index + 1d) / totalEntries);
        }
    }

    private static ZipArchive OpenArchiveRead(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex) when (archivePath.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.rar_unsupported"), ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.archive_open_failed"), ex);
        }
    }
    private static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

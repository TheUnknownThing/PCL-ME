using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static void CopyEmbeddedForgelikeLibraries(
        ZipArchive archive,
        JsonObject installProfile,
        string launcherDirectory)
    {
        if (installProfile["libraries"] is JsonArray libraries)
        {
            foreach (var node in libraries)
            {
                if (node is not JsonObject library)
                {
                    continue;
                }

                var relativePath = ResolveLibraryRelativePath(library);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                TryCopyInstallerEntryToFile(
                    archive,
                    "maven/" + relativePath.Replace('\\', '/'),
                    Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        var mainArtifact = GetArtifactDescriptor(installProfile["path"]);
        if (!string.IsNullOrWhiteSpace(mainArtifact))
        {
            var relativePath = FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(mainArtifact);
            TryCopyInstallerEntryToFile(
                archive,
                "maven/" + relativePath.Replace('\\', '/'),
                Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }


    private static void EnsureForgelikeLibrariesAvailable(
        JsonObject installProfile,
        string launcherDirectory,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        if (installProfile["libraries"] is not JsonArray libraries)
        {
            return;
        }

        foreach (var node in libraries)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (node is not JsonObject library)
            {
                continue;
            }

            EnsureForgelikeLibraryAvailable(library, launcherDirectory, downloadProvider, speedLimiter, cancelToken);
        }
    }


    private static void EnsureForgelikeLibraryAvailable(
        JsonObject library,
        string launcherDirectory,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        var relativePath = ResolveLibraryRelativePath(library);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var localPath = Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
        {
            var expectedSha1 = GetLibraryArtifactSha1(library);
            if (string.IsNullOrWhiteSpace(expectedSha1)
                || string.Equals(ComputeFileSha1(localPath), expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TryDeleteFile(localPath);
        }

        var downloadUrl = ResolveLibraryArtifactUrl(library, relativePath);
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        DownloadFileToPath(downloadUrl, localPath, GetLibraryArtifactSha1(library), downloadProvider, speedLimiter, cancelToken);
    }


    private static string GetRequiredArtifactPath(
        JsonNode? node,
        string librariesDirectory,
        string errorMessage)
    {
        var descriptor = GetArtifactDescriptor(node);
        return string.IsNullOrWhiteSpace(descriptor)
            ? throw new InvalidOperationException(errorMessage)
            : GetArtifactAbsolutePath(librariesDirectory, descriptor);
    }


    private static string GetArtifactAbsolutePath(string librariesDirectory, string descriptor)
    {
        return Path.Combine(
            librariesDirectory,
            FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(descriptor).Replace('/', Path.DirectorySeparatorChar));
    }


    private static string GetRequiredString(JsonObject source, string key, string errorMessage)
    {
        var value = source[key]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(errorMessage) : value;
    }


    private static string? ResolveLibraryRelativePath(JsonObject library)
    {
        var explicitPath = library["downloads"]?["artifact"]?["path"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath.Replace('\\', '/');
        }

        var descriptor = GetArtifactDescriptor(library["name"]);
        return string.IsNullOrWhiteSpace(descriptor)
            ? null
            : FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(descriptor).Replace('\\', '/');
    }


    private static string? ResolveLibraryArtifactUrl(JsonObject library, string relativePath)
    {
        var explicitArtifactUrl = library["downloads"]?["artifact"]?["url"]?.GetValue<string>();
        if (library["downloads"]?["artifact"] is JsonObject && explicitArtifactUrl is not null)
        {
            return string.IsNullOrWhiteSpace(explicitArtifactUrl) ? null : explicitArtifactUrl;
        }

        return FrontendLibraryArtifactResolver.BuildLibraryUrl(
            library["url"]?.GetValue<string>(),
            relativePath);
    }


    private static string? GetLibraryArtifactSha1(JsonObject library)
    {
        return library["downloads"]?["artifact"]?["sha1"]?.GetValue<string>()
               ?? library["sha1"]?.GetValue<string>();
    }


    private static string? GetArtifactDescriptor(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue)
        {
            return node.GetValue<string>();
        }

        if (node is JsonObject obj)
        {
            return obj["name"]?.GetValue<string>();
        }

        return null;
    }


    private static void CopyInstallerEntryToFile(ZipArchive archive, string entryPath, string targetPath)
    {
        using var source = OpenInstallerEntry(archive, entryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var target = File.Create(targetPath);
        source.CopyTo(target);
    }


    private static bool TryCopyInstallerEntryToFile(ZipArchive archive, string entryPath, string targetPath)
    {
        var entry = archive.GetEntry(entryPath.TrimStart('/').Replace('\\', '/'));
        if (entry is null)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var source = entry.Open();
        using var target = File.Create(targetPath);
        source.CopyTo(target);
        return true;
    }


    private static string ExtractInstallerEntryToTempFile(ZipArchive archive, string entryPath, string tempDirectory)
    {
        Directory.CreateDirectory(tempDirectory);
        var extension = Path.GetExtension(entryPath);
        var outputPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + extension);
        CopyInstallerEntryToFile(archive, entryPath, outputPath);
        return Path.GetFullPath(outputPath);
    }


    private static Stream OpenInstallerEntry(ZipArchive archive, string entryPath)
    {
        return archive.GetEntry(entryPath.TrimStart('/').Replace('\\', '/'))?.Open()
               ?? throw new InvalidOperationException($"Installer is missing entry: {entryPath}");
    }


    private static string ReadJarMainClass(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        using var stream = OpenInstallerEntry(archive, "META-INF/MANIFEST.MF");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? currentHeader = null;
        var currentValue = new StringBuilder();

        void FlushCurrentHeader()
        {
            if (string.Equals(currentHeader, "Main-Class", StringComparison.OrdinalIgnoreCase))
            {
                throw new OperationCanceledException(currentValue.ToString());
            }

            currentHeader = null;
            currentValue.Clear();
        }

        try
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    FlushCurrentHeader();
                    continue;
                }

                if (line[0] == ' ' && currentHeader is not null)
                {
                    currentValue.Append(line[1..]);
                    continue;
                }

                FlushCurrentHeader();
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                currentHeader = line[..separatorIndex];
                currentValue.Append(line[(separatorIndex + 1)..].TrimStart());
            }

            FlushCurrentHeader();
        }
        catch (OperationCanceledException ex)
        {
            return ex.Message;
        }

        return string.Empty;
    }


    private static string ComputeFileSha1(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }


    private static void DownloadFileToPath(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        FrontendDownloadProvider? downloadProvider = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            DownloadToPathWithCandidates(url, tempPath, downloadProvider, speedLimiter, cancelToken);

            if (!string.IsNullOrWhiteSpace(expectedSha1))
            {
                var actualSha1 = ComputeFileSha1(tempPath);
                if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Downloaded installer dependency checksum mismatch for {targetPath}: expected {expectedSha1}, actual {actualSha1}.");
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }


    private static void DownloadToPathWithCandidates(
        string url,
        string targetPath,
        FrontendDownloadProvider? downloadProvider = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancelToken = default)
    {
        Exception? lastError = null;
        var candidateUrls = downloadProvider?.GetPreferredUrls(url) ?? [url];
        foreach (var candidateUrl in candidateUrls)
        {
            try
            {
                FrontendDownloadTransferService.DownloadToPath(
                    HttpClient,
                    candidateUrl,
                    targetPath,
                    speedLimiter: speedLimiter,
                    cancelToken: cancelToken);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download file: {url}", lastError);
    }

}

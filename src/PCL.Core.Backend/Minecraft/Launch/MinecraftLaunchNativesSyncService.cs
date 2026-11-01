using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchNativesSyncService
{
    public static MinecraftLaunchNativesSyncResult Sync(MinecraftLaunchNativesSyncRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.NativeArchives);

        var logs = new List<string> { "Extracting native files" };
        var retainedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetRoot = Path.GetFullPath(request.TargetDirectory);

        Directory.CreateDirectory(targetRoot);

        foreach (var archiveRequest in request.NativeArchives)
        {
            ArgumentNullException.ThrowIfNull(archiveRequest);

            var archivePath = archiveRequest.ArchivePath;
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                continue;
            }

            logs.Add("Native archive: " + archivePath);
            if (archiveRequest.ExtractExcludes.Count > 0)
            {
                logs.Add("Exclusion rules: " + string.Join(", ", archiveRequest.ExtractExcludes));
            }

            try
            {
                using var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    var entryPath = entry.FullName.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(entryPath) ||
                        entryPath.EndsWith("/", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(entry.Name))
                    {
                        continue;
                    }

                    if (archiveRequest.ExtractExcludes.Any(prefix =>
                            entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (request.LogSkippedFiles)
                        {
                            logs.Add("Skipped by rule: " + entryPath);
                        }

                        continue;
                    }

                    if (entry.Name.EndsWith(".sha1", StringComparison.OrdinalIgnoreCase) ||
                        entry.Name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!IsNativeLibraryFile(entry.Name))
                    {
                        if (request.LogSkippedFiles)
                        {
                            logs.Add("Skipped by file type: " + entryPath);
                        }

                        continue;
                    }

                    var relativePath = entryPath.Replace('/', Path.DirectorySeparatorChar);
                    var targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                    if (!IsPathWithinDirectory(targetPath, targetRoot))
                    {
                        logs.Add("Skipped out-of-bounds path: " + entryPath);
                        continue;
                    }

                    retainedFiles.Add(targetPath);

                    var targetDirectory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    var existingFile = new FileInfo(targetPath);
                    if (existingFile.Exists)
                    {
                        if (existingFile.Length == entry.Length)
                        {
                            if (request.LogSkippedFiles)
                            {
                                logs.Add("No extraction needed: " + targetPath);
                            }

                            continue;
                        }

                        try
                        {
                            File.Delete(targetPath);
                        }
                        catch (UnauthorizedAccessException exception)
                        {
                            logs.Add("Access denied while deleting the existing native file, which usually means Minecraft is running; skipping extraction: " + targetPath);
                            logs.Add("Actual error: " + exception);
                            break;
                        }
                    }

                    using var sourceStream = entry.Open();
                    using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    sourceStream.CopyTo(targetStream);
                    logs.Add("Extracted: " + targetPath);
                }
            }
            catch (InvalidDataException exception)
            {
                TryDeleteCorruptedArchive(archivePath);
                throw new InvalidDataException($"Could not open the native archive ({archivePath}); the file may be corrupted. Please try launching again.", exception);
            }
        }

        foreach (var filePath in Directory.GetFiles(targetRoot, "*", SearchOption.AllDirectories))
        {
            if (retainedFiles.Contains(filePath))
            {
                continue;
            }

            try
            {
                logs.Add("Deleted: " + filePath);
                File.Delete(filePath);
            }
            catch (UnauthorizedAccessException exception)
            {
                logs.Add("Access denied while deleting extra files; skipping the delete step");
                logs.Add("Actual error: " + exception);
                return new MinecraftLaunchNativesSyncResult(logs);
            }
        }

        return new MinecraftLaunchNativesSyncResult(logs);
    }

    private static void TryDeleteCorruptedArchive(string archivePath)
    {
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
        catch
        {
        }
    }

    private static bool IsNativeLibraryFile(string fileName)
    {
        return Path.GetExtension(fileName) switch
        {
            ".dll" => true,
            ".so" => true,
            ".dylib" => true,
            ".jnilib" => true,
            _ => false
        };
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var normalizedDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedDirectory, GetPathComparison());
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}

public sealed record MinecraftLaunchNativeArchive(
    string ArchivePath,
    IReadOnlyList<string> ExtractExcludes);

public sealed record MinecraftLaunchNativesSyncRequest(
    string TargetDirectory,
    IReadOnlyList<MinecraftLaunchNativeArchive> NativeArchives,
    bool LogSkippedFiles);

public sealed record MinecraftLaunchNativesSyncResult(
    IReadOnlyList<string> LogMessages);

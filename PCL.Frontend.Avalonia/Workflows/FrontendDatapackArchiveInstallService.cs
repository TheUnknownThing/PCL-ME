using System.IO.Compression;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDatapackArchiveInstallService
{
    private static readonly HashSet<string> KnownDatapackRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "data"
    };

    private static readonly HashSet<string> KnownDatapackRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "pack.mcmeta",
        "pack.png",
        "pack.txt"
    };

    internal static string ExtractInstalledDatapackArchive(string archivePath, string? replacedPath = null)
    {
        var extension = Path.GetExtension(archivePath);
        if (!(string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
              || string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase))
            || !File.Exists(archivePath))
        {
            return archivePath;
        }

        var datapacksDirectory = Path.GetDirectoryName(archivePath)
            ?? throw new InvalidOperationException("数据包安装目录不可用。");
        var fallbackDirectoryName = Path.GetFileNameWithoutExtension(archivePath);
        var extractionRoot = Path.Combine(
            datapacksDirectory,
            $".pcl-datapack-import-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(extractionRoot);

        try
        {
            var entryNameEncoding = FrontendWorldArchiveInstallService.DetermineZipEntryNameEncoding(archivePath);
            ZipFile.ExtractToDirectory(archivePath, extractionRoot, entryNameEncoding, overwriteFiles: true);

            var layout = ResolveExtractedDatapackLayout(extractionRoot, fallbackDirectoryName);
            var finalDirectory = ResolveFinalDirectory(datapacksDirectory, layout.TargetDirectoryName, replacedPath);

            if (Directory.Exists(finalDirectory))
            {
                Directory.Delete(finalDirectory, recursive: true);
            }

            if (string.Equals(layout.DatapackRootPath, extractionRoot, StringComparison.Ordinal))
            {
                Directory.CreateDirectory(finalDirectory);
                MoveDirectoryContents(layout.DatapackRootPath, finalDirectory);
            }
            else
            {
                Directory.Move(layout.DatapackRootPath, finalDirectory);
            }

            File.Delete(archivePath);
            return finalDirectory;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"数据包下载完成，但自动解压失败：{ex.Message}", ex);
        }
        finally
        {
            if (Directory.Exists(extractionRoot))
            {
                try
                {
                    Directory.Delete(extractionRoot, recursive: true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
    }

    private static ExtractedDatapackLayout ResolveExtractedDatapackLayout(string extractedRoot, string fallbackDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDirectoryName);

        var currentDirectory = extractedRoot;

        while (true)
        {
            var entries = EnumerateRelevantEntries(currentDirectory).ToArray();
            if (entries.Length == 0)
            {
                throw new InvalidOperationException("压缩包中没有可导入的数据包内容。");
            }

            if (LooksLikeDatapackRootDirectory(entries))
            {
                var targetDirectoryName = string.Equals(currentDirectory, extractedRoot, StringComparison.Ordinal)
                    ? fallbackDirectoryName
                    : new DirectoryInfo(currentDirectory).Name;
                return new ExtractedDatapackLayout(currentDirectory, targetDirectoryName);
            }

            var childDirectories = entries
                .OfType<DirectoryInfo>()
                .ToArray();
            if (childDirectories.Length != 1)
            {
                throw new InvalidOperationException("压缩包中没有可识别的 Minecraft 数据包结构。");
            }

            currentDirectory = childDirectories[0].FullName;
        }
    }

    private static IEnumerable<FileSystemInfo> EnumerateRelevantEntries(string directory)
    {
        return new DirectoryInfo(directory)
            .EnumerateFileSystemInfos()
            .Where(entry => !IsIgnoredExtractedEntry(entry));
    }

    private static bool LooksLikeDatapackRootDirectory(IEnumerable<FileSystemInfo> entries)
    {
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            switch (entry)
            {
                case FileInfo file:
                    fileNames.Add(file.Name);
                    break;
                case DirectoryInfo directory:
                    directoryNames.Add(directory.Name);
                    break;
            }
        }

        return fileNames.Overlaps(KnownDatapackRootFiles) || directoryNames.Overlaps(KnownDatapackRootDirectories);
    }

    private static bool IsIgnoredExtractedEntry(FileSystemInfo entry)
    {
        return string.Equals(entry.Name, "__MACOSX", StringComparison.OrdinalIgnoreCase)
               || string.Equals(entry.Name, ".DS_Store", StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        foreach (var entry in new DirectoryInfo(sourceDirectory).EnumerateFileSystemInfos())
        {
            if (IsIgnoredExtractedEntry(entry))
            {
                continue;
            }

            var targetPath = Path.Combine(targetDirectory, entry.Name);
            switch (entry)
            {
                case DirectoryInfo:
                    Directory.Move(entry.FullName, targetPath);
                    break;
                case FileInfo:
                    File.Move(entry.FullName, targetPath);
                    break;
            }
        }
    }

    private static string ResolveFinalDirectory(
        string datapacksDirectory,
        string targetDirectoryName,
        string? replacedPath)
    {
        if (!string.IsNullOrWhiteSpace(replacedPath)
            && string.Equals(Path.GetDirectoryName(replacedPath), datapacksDirectory, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(replacedPath))
        {
            return replacedPath;
        }

        return GetUniqueChildPath(datapacksDirectory, targetDirectoryName);
    }

    private static string GetUniqueChildPath(string parentDirectory, string name)
    {
        var candidate = Path.Combine(parentDirectory, name);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var index = 2;
        while (true)
        {
            candidate = Path.Combine(parentDirectory, $"{name} ({index})");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private sealed record ExtractedDatapackLayout(
        string DatapackRootPath,
        string TargetDirectoryName);
}

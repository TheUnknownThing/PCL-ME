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

        var logs = new List<string> { "正在解压 Natives 文件" };
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

            logs.Add("Native 存档：" + archivePath);
            if (archiveRequest.ExtractExcludes.Count > 0)
            {
                logs.Add("排除规则：" + string.Join(", ", archiveRequest.ExtractExcludes));
            }

            ZipArchive archive;
            try
            {
                archive = new ZipArchive(new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (InvalidDataException exception)
            {
                TryDeleteCorruptedArchive(archivePath);
                throw new InvalidDataException($"无法打开 Natives 文件（{archivePath}），该文件可能已损坏，请重新尝试启动游戏", exception);
            }

            using (archive)
            {
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
                            logs.Add("按规则跳过：" + entryPath);
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
                            logs.Add("按文件类型跳过：" + entryPath);
                        }

                        continue;
                    }

                    var relativePath = entryPath.Replace('/', Path.DirectorySeparatorChar);
                    var targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                    if (!targetPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        logs.Add("跳过越界路径：" + entryPath);
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
                                logs.Add("无需解压：" + targetPath);
                            }

                            continue;
                        }

                        try
                        {
                            File.Delete(targetPath);
                        }
                        catch (UnauthorizedAccessException exception)
                        {
                            logs.Add("删除原 Native 文件访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" + targetPath);
                            logs.Add("实际的错误信息：" + exception);
                            break;
                        }
                    }

                    using var sourceStream = entry.Open();
                    using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    sourceStream.CopyTo(targetStream);
                    logs.Add("已解压：" + targetPath);
                }
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
                logs.Add("删除：" + filePath);
                File.Delete(filePath);
            }
            catch (UnauthorizedAccessException exception)
            {
                logs.Add("删除多余文件访问被拒绝，跳过删除步骤");
                logs.Add("实际的错误信息：" + exception);
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

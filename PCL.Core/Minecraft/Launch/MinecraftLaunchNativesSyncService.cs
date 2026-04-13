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
        ArgumentNullException.ThrowIfNull(request.NativeArchivePaths);

        var logs = new List<string> { "正在解压 Natives 文件" };
        var retainedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(request.TargetDirectory);

        foreach (var archivePath in request.NativeArchivePaths)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                continue;
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
                    if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var targetPath = Path.Combine(request.TargetDirectory, relativePath);
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
                            logs.Add("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" + targetPath);
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

        foreach (var filePath in Directory.GetFiles(request.TargetDirectory))
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
}

public sealed record MinecraftLaunchNativesSyncRequest(
    string TargetDirectory,
    IReadOnlyList<string> NativeArchivePaths,
    bool LogSkippedFiles);

public sealed record MinecraftLaunchNativesSyncResult(
    IReadOnlyList<string> LogMessages);

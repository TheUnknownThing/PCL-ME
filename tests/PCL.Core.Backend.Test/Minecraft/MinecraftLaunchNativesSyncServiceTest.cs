using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchNativesSyncServiceTest
{
    [TestMethod]
    public void SyncExtractsDllsDeletesExtraFilesAndSkipsNonDllEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var archivePath = Path.Combine(root, "native.zip");
            CreateArchive(
                archivePath,
                ("a.dll", "alpha"),
                ("nested/b.dll", "bravo"),
                ("readme.txt", "ignore"));

            var targetDirectory = Path.Combine(root, "natives");
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(targetDirectory, "stale.dll"), "stale");

            var result = MinecraftLaunchNativesSyncService.Sync(
                new MinecraftLaunchNativesSyncRequest(
                    targetDirectory,
                    [new MinecraftLaunchNativeArchive(archivePath, [])],
                    LogSkippedFiles: false));

            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "a.dll")));
            Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(targetDirectory, "a.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "nested", "b.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "readme.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "stale.dll")));
            Assert.IsTrue(result.LogMessages.Any(log => log.Contains("Extracted: " + Path.Combine(targetDirectory, "a.dll"), StringComparison.Ordinal)));
            Assert.IsTrue(result.LogMessages.Any(log => log.Contains("Deleted: " + Path.Combine(targetDirectory, "stale.dll"), StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void SyncLogsSkippedFilesWhenExistingDllAlreadyMatchesLength()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-skip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var archivePath = Path.Combine(root, "native.zip");
            CreateArchive(archivePath, ("match.dll", "same"));

            var targetDirectory = Path.Combine(root, "natives");
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(targetDirectory, "match.dll"), "xxxx", Encoding.UTF8);

            var result = MinecraftLaunchNativesSyncService.Sync(
                new MinecraftLaunchNativesSyncRequest(
                    targetDirectory,
                    [new MinecraftLaunchNativeArchive(archivePath, [])],
                    LogSkippedFiles: true));

            Assert.IsTrue(result.LogMessages.Any(log => log.Contains("No extraction needed: " + Path.Combine(targetDirectory, "match.dll"), StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void SyncDeletesCorruptedArchiveAndThrowsFriendlyError()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var archivePath = Path.Combine(root, "broken.zip");
            File.WriteAllText(archivePath, "not a zip");

            var exception = Assert.ThrowsExactly<InvalidDataException>(
                () => MinecraftLaunchNativesSyncService.Sync(
                    new MinecraftLaunchNativesSyncRequest(
                        Path.Combine(root, "natives"),
                        [new MinecraftLaunchNativeArchive(archivePath, [])],
                        LogSkippedFiles: false)));

            StringAssert.Contains(exception.Message, "Could not open the native archive");
            Assert.IsFalse(File.Exists(archivePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void SyncSkipsEntriesThatEscapeTargetDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-escape-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var archivePath = Path.Combine(root, "native.zip");
            CreateArchive(archivePath, ("../natives-escape.dll", "escape"), ("safe.dll", "safe"));

            var targetDirectory = Path.Combine(root, "natives");
            Directory.CreateDirectory(targetDirectory);

            var result = MinecraftLaunchNativesSyncService.Sync(
                new MinecraftLaunchNativesSyncRequest(
                    targetDirectory,
                    [new MinecraftLaunchNativeArchive(archivePath, [])],
                    LogSkippedFiles: true));

            Assert.IsTrue(File.Exists(Path.Combine(targetDirectory, "safe.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "natives-escape.dll")));
            Assert.IsTrue(result.LogMessages.Any(log => log.Contains("Skipped out-of-bounds path: ../natives-escape.dll", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void CreateArchive(string archivePath, params (string path, string content)[] entries)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }
    }
}

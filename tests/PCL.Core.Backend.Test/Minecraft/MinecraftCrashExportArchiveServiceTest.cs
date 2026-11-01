using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftCrashExportArchiveServiceTest
{
    [TestMethod]
    public void CreateArchiveBuildsZipAndCleansReportDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-archive-" + Guid.NewGuid().ToString("N"));
        var reportDirectory = Path.Combine(root, "Report");
        var archivePath = Path.Combine(root, "Export", "crash-report.zip");
        Directory.CreateDirectory(root);

        try
        {
            var launcherLogPath = Path.Combine(root, "Log-CE1.log");
            File.WriteAllText(launcherLogPath, "launcher log", Encoding.UTF8);

            var result = MinecraftCrashExportArchiveService.CreateArchive(
                new MinecraftCrashExportArchiveRequest(
                    archivePath,
                    new MinecraftCrashExportRequest(
                        reportDirectory,
                        "2.14.5",
                        "device-123",
                        [new MinecraftCrashExportFile(launcherLogPath)],
                        launcherLogPath,
                        new SystemEnvironmentSnapshot(
                            "Windows",
                            new Version(10, 0, 22635, 0),
                            Architecture.X64,
                            true,
                            16UL * 1024 * 1024 * 1024,
                            "AMD Ryzen",
                            []),
                        CurrentAccessToken: null,
                        CurrentUserUuid: null,
                        UserProfilePath: null)));

            Assert.AreEqual(archivePath, result.ArchiveFilePath);
            Assert.IsFalse(Directory.Exists(reportDirectory));
            CollectionAssert.Contains(result.ArchivedFileNames.ToArray(), "PCL Launcher Log.txt");
            CollectionAssert.Contains(result.ArchivedFileNames.ToArray(), "Environment and Launch Info.txt");

            using var archive = ZipFile.OpenRead(archivePath);
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "PCL Launcher Log.txt");
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "Environment and Launch Info.txt");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}

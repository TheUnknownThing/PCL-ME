using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;
using System.IO.Compression;
using System.Text;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendWorldArchiveInstallServiceTest
{
    [TestMethod]
    public void ResolveExtractedWorldLayoutUsesArchiveNameWhenWorldIsAlreadyAtRoot()
    {
        using var root = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(root.Path, "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(root.Path, "region"));

        var layout = FrontendWorldArchiveInstallService.ResolveExtractedWorldLayout(root.Path, "MyWorld");

        Assert.AreEqual(root.Path, layout.WorldRootPath);
        Assert.AreEqual("MyWorld", layout.TargetDirectoryName);
    }

    [TestMethod]
    public void ResolveExtractedWorldLayoutStripsSingleWorldWrapperDirectory()
    {
        using var root = new TemporaryDirectory();
        var worldDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "SkyBlock"));
        File.WriteAllText(Path.Combine(worldDirectory.FullName, "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(worldDirectory.FullName, "region"));

        var layout = FrontendWorldArchiveInstallService.ResolveExtractedWorldLayout(root.Path, "downloaded-world");

        Assert.AreEqual(worldDirectory.FullName, layout.WorldRootPath);
        Assert.AreEqual("SkyBlock", layout.TargetDirectoryName);
    }

    [TestMethod]
    public void ResolveExtractedWorldLayoutCollapsesNestedWrapperDirectories()
    {
        using var root = new TemporaryDirectory();
        var worldDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "downloads", "SkyBlock"));
        File.WriteAllText(Path.Combine(worldDirectory.FullName, "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(worldDirectory.FullName, "region"));

        var layout = FrontendWorldArchiveInstallService.ResolveExtractedWorldLayout(root.Path, "downloaded-world");

        Assert.AreEqual(worldDirectory.FullName, layout.WorldRootPath);
        Assert.AreEqual("SkyBlock", layout.TargetDirectoryName);
    }

    [TestMethod]
    public void ResolveExtractedWorldLayoutIgnoresMacOsMetadataEntries()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(root.Path, "__MACOSX"));
        File.WriteAllText(Path.Combine(root.Path, ".DS_Store"), "ignore");
        var worldDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "SkyBlock"));
        File.WriteAllText(Path.Combine(worldDirectory.FullName, ".DS_Store"), "ignore");
        File.WriteAllText(Path.Combine(worldDirectory.FullName, "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(worldDirectory.FullName, "region"));

        var layout = FrontendWorldArchiveInstallService.ResolveExtractedWorldLayout(root.Path, "downloaded-world");

        Assert.AreEqual(worldDirectory.FullName, layout.WorldRootPath);
        Assert.AreEqual("SkyBlock", layout.TargetDirectoryName);
    }

    [TestMethod]
    public void ResolveExtractedWorldLayoutDescendsPastReadmeWrapper()
    {
        using var root = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(root.Path, "README.txt"), "metadata");
        var worldDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "Lucky OneBlock 1.21.11 (v0.5)"));
        File.WriteAllText(Path.Combine(worldDirectory.FullName, "level.dat"), "world");
        Directory.CreateDirectory(Path.Combine(worldDirectory.FullName, "region"));

        var layout = FrontendWorldArchiveInstallService.ResolveExtractedWorldLayout(root.Path, "downloaded-world");

        Assert.AreEqual(worldDirectory.FullName, layout.WorldRootPath);
        Assert.AreEqual("Lucky OneBlock 1.21.11 (v0.5)", layout.TargetDirectoryName);
    }

    [TestMethod]
    public void ExtractInstalledWorldArchivePreservesGb18030FolderName()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var root = new TemporaryDirectory();
        var savesDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "saves")).FullName;
        var archivePath = Path.Combine(savesDirectory, "压缩包.zip");

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.GetEncoding("GB18030")))
        {
            archive.CreateEntry("中文存档/");
            archive.CreateEntry("中文存档/level.dat");
            archive.CreateEntry("中文存档/region/");
            archive.CreateEntry("中文存档/region/r.0.0.mca");
        }

        var extractedPath = FrontendWorldArchiveInstallService.ExtractInstalledWorldArchive(archivePath);

        Assert.AreEqual(Path.Combine(savesDirectory, "中文存档"), extractedPath);
        Assert.IsTrue(File.Exists(Path.Combine(extractedPath, "level.dat")));
        Assert.IsTrue(Directory.Exists(Path.Combine(extractedPath, "region")));
    }

    [TestMethod]
    public void DetermineZipEntryNameEncodingReturnsUtf8ForUtf8FlaggedArchive()
    {
        using var root = new TemporaryDirectory();
        var archivePath = Path.Combine(root.Path, "utf8-flagged.zip");

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
        {
            archive.CreateEntry("§aOneblock §2Original §rv3_4_2/");
            archive.CreateEntry("§aOneblock §2Original §rv3_4_2/level.dat");
        }

        var encoding = FrontendWorldArchiveInstallService.DetermineZipEntryNameEncoding(archivePath);

        Assert.AreEqual(Encoding.UTF8.CodePage, encoding.CodePage);
    }

    [TestMethod]
    public void DetermineZipEntryNameEncodingPrefersUtf8WithoutLanguageFlag()
    {
        using var root = new TemporaryDirectory();
        var archivePath = Path.Combine(root.Path, "utf8-no-flag.zip");

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
        {
            archive.CreateEntry("§aOneblock §2Original §rv3_4_0/");
            archive.CreateEntry("§aOneblock §2Original §rv3_4_0/level.dat");
        }

        ClearUtf8LanguageEncodingFlag(archivePath);

        var encoding = FrontendWorldArchiveInstallService.DetermineZipEntryNameEncoding(archivePath);

        Assert.AreEqual(Encoding.UTF8.CodePage, encoding.CodePage);
    }

    [TestMethod]
    public void DetermineZipEntryNameEncodingReturnsGb18030ForGb18030Archive()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var root = new TemporaryDirectory();
        var archivePath = Path.Combine(root.Path, "gb18030.zip");

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.GetEncoding("GB18030")))
        {
            archive.CreateEntry("中文存档/");
            archive.CreateEntry("中文存档/level.dat");
        }

        var encoding = FrontendWorldArchiveInstallService.DetermineZipEntryNameEncoding(archivePath);

        Assert.AreEqual(Encoding.GetEncoding("GB18030").CodePage, encoding.CodePage);
    }

    [TestMethod]
    public void ExtractInstalledWorldArchivePrefersUtf8NameOverGb18030Mojibake()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var root = new TemporaryDirectory();
        var savesDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "saves")).FullName;
        var archivePath = Path.Combine(savesDirectory, "oneblock.zip");

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
        {
            archive.CreateEntry("§aOneblock §2Original §rv3_4_0/");
            archive.CreateEntry("§aOneblock §2Original §rv3_4_0/level.dat");
            archive.CreateEntry("§aOneblock §2Original §rv3_4_0/region/");
        }

        ClearUtf8LanguageEncodingFlag(archivePath);

        var extractedPath = FrontendWorldArchiveInstallService.ExtractInstalledWorldArchive(archivePath);

        Assert.AreEqual(Path.Combine(savesDirectory, "§aOneblock §2Original §rv3_4_0"), extractedPath);
        Assert.IsFalse(Directory.Exists(Path.Combine(savesDirectory, "鮝Oneblock ?2Original 鮮v3_4_0")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-world-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static void ClearUtf8LanguageEncodingFlag(string archivePath)
    {
        var bytes = File.ReadAllBytes(archivePath);

        for (var index = 0; index <= bytes.Length - 4; index += 1)
        {
            var signature = BitConverter.ToUInt32(bytes, index);
            switch (signature)
            {
                case 0x04034B50:
                    ClearBit11(bytes, index + 6);
                    break;
                case 0x02014B50:
                    ClearBit11(bytes, index + 8);
                    break;
            }
        }

        File.WriteAllBytes(archivePath, bytes);
    }

    private static void ClearBit11(byte[] bytes, int offset)
    {
        var flags = BitConverter.ToUInt16(bytes, offset);
        flags = (ushort)(flags & ~0x0800);
        bytes[offset] = (byte)(flags & 0xFF);
        bytes[offset + 1] = (byte)(flags >> 8);
    }
}

using System.IO.Compression;
using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendWorldArchiveInstallService
{
    private static readonly Encoding Gb18030Encoding;
    private static readonly UTF8Encoding Utf8Encoding = new(false, true);
    private static readonly HashSet<string> KnownWorldRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "advancements",
        "data",
        "datapacks",
        "DIM-1",
        "DIM1",
        "entities",
        "generated",
        "playerdata",
        "poi",
        "region",
        "serverconfig",
        "stats"
    };

    private static readonly HashSet<string> KnownWorldRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "level.dat",
        "level.dat_old",
        "session.lock",
        "icon.png"
    };

    static FrontendWorldArchiveInstallService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Gb18030Encoding = Encoding.GetEncoding("GB18030");
    }

    internal static string ExtractInstalledWorldArchive(string archivePath)
    {
        var extension = Path.GetExtension(archivePath);
        if (!(string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
              || string.Equals(extension, ".mcworld", StringComparison.OrdinalIgnoreCase))
            || !File.Exists(archivePath))
        {
            return archivePath;
        }

        var savesDirectory = Path.GetDirectoryName(archivePath)
            ?? throw new InvalidOperationException("存档安装目录不可用。");
        var fallbackDirectoryName = Path.GetFileNameWithoutExtension(archivePath);
        var extractionRoot = Path.Combine(
            savesDirectory,
            $".pcl-world-import-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(extractionRoot);

        try
        {
            var entryNameEncoding = DetermineZipEntryNameEncoding(archivePath);
            ZipFile.ExtractToDirectory(archivePath, extractionRoot, entryNameEncoding, overwriteFiles: true);

            var layout = ResolveExtractedWorldLayout(extractionRoot, fallbackDirectoryName);
            var finalDirectory = GetUniqueChildPath(savesDirectory, layout.TargetDirectoryName);

            if (string.Equals(layout.WorldRootPath, extractionRoot, StringComparison.Ordinal))
            {
                Directory.CreateDirectory(finalDirectory);
                MoveDirectoryContents(layout.WorldRootPath, finalDirectory);
            }
            else
            {
                Directory.Move(layout.WorldRootPath, finalDirectory);
            }

            File.Delete(archivePath);
            return finalDirectory;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"存档压缩包下载完成，但自动解压失败：{ex.Message}", ex);
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

    internal static Encoding DetermineZipEntryNameEncoding(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        var entries = ReadCentralDirectoryEntries(stream)
            .ToArray();
        if (entries.Length == 0 || TestEncoding(entries, Utf8Encoding))
        {
            return Encoding.UTF8;
        }

        var nativeEncoding = Encoding.Default;
        if (nativeEncoding.CodePage != Encoding.UTF8.CodePage && TestEncoding(entries, nativeEncoding))
        {
            return nativeEncoding;
        }

        foreach (var candidate in GetEncodingCandidates())
        {
            if (candidate.CodePage != nativeEncoding.CodePage && TestEncoding(entries, candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("无法确定压缩包文件名编码。");
    }

    internal static ExtractedWorldLayout ResolveExtractedWorldLayout(string extractedRoot, string fallbackDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractedRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDirectoryName);

        var currentDirectory = extractedRoot;

        while (true)
        {
            var entries = EnumerateRelevantEntries(currentDirectory).ToArray();
            if (entries.Length == 0)
            {
                throw new InvalidOperationException("压缩包中没有可导入的存档内容。");
            }

            if (LooksLikeWorldRootDirectory(entries))
            {
                var targetDirectoryName = string.Equals(currentDirectory, extractedRoot, StringComparison.Ordinal)
                    ? fallbackDirectoryName
                    : new DirectoryInfo(currentDirectory).Name;
                return new ExtractedWorldLayout(currentDirectory, targetDirectoryName);
            }

            var childDirectories = entries
                .OfType<DirectoryInfo>()
                .ToArray();
            if (childDirectories.Length != 1)
            {
                throw new InvalidOperationException("压缩包中没有可识别的 Minecraft 存档结构。");
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

    private static bool LooksLikeWorldRootDirectory(IEnumerable<FileSystemInfo> entries)
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

        return fileNames.Overlaps(KnownWorldRootFiles) || directoryNames.Overlaps(KnownWorldRootDirectories);
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

    private static string GetUniqueChildPath(string parentDirectory, string name)
    {
        var candidate = Path.Combine(parentDirectory, name);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        for (var index = 2; ; index += 1)
        {
            candidate = Path.Combine(parentDirectory, $"{name} ({index})");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool TestEncoding(IEnumerable<ZipCentralDirectoryEntryName> entries, Encoding encoding)
    {
        try
        {
            var decoder = CreateStrictDecoder(encoding);
            foreach (var entry in entries)
            {
                if (entry.IsUtf8 || entry.FileNameBytes.Length == 0)
                {
                    continue;
                }

                decoder.Reset();
                var buffer = new char[encoding.GetMaxCharCount(entry.FileNameBytes.Length)];
                decoder.GetChars(entry.FileNameBytes, 0, entry.FileNameBytes.Length, buffer, 0, flush: true);
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static IReadOnlyList<Encoding> GetEncodingCandidates()
    {
        var candidates = new List<Encoding>();
        foreach (var name in new[]
        {
            "GB18030",
            "Big5",
            "Shift_JIS",
            "EUC-JP",
            "ISO-2022-JP",
            "EUC-KR",
            "ISO-2022-KR",
            "KOI8-R",
            "windows-1251",
            "x-MacCyrillic",
            "IBM855",
            "IBM866",
            "windows-1252",
            "ISO-8859-1",
            "ISO-8859-5",
            "ISO-8859-7",
            "ISO-8859-8",
            "UTF-16LE",
            "UTF-16BE",
            "UTF-32LE",
            "UTF-32BE"
        })
        {
            try
            {
                var encoding = Encoding.GetEncoding(name);
                if (candidates.All(existing => existing.CodePage != encoding.CodePage))
                {
                    candidates.Add(encoding);
                }
            }
            catch (ArgumentException)
            {
                // Skip unsupported encodings on the current runtime.
            }
        }

        return candidates;
    }

    private static Decoder CreateStrictDecoder(Encoding encoding)
    {
        var strictEncoding = (Encoding)encoding.Clone();
        strictEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        return strictEncoding.GetDecoder();
    }

    private static IReadOnlyList<ZipCentralDirectoryEntryName> ReadCentralDirectoryEntries(Stream stream)
    {
        const uint endOfCentralDirectorySignature = 0x06054B50;
        const uint centralDirectoryHeaderSignature = 0x02014B50;
        const int minimumEndOfCentralDirectorySize = 22;
        const int maximumCommentLength = ushort.MaxValue;
        const int searchWindow = minimumEndOfCentralDirectorySize + maximumCommentLength;

        if (stream.Length < minimumEndOfCentralDirectorySize)
        {
            return [];
        }

        var searchLength = (int)Math.Min(stream.Length, searchWindow);
        var buffer = new byte[searchLength];
        stream.Seek(-searchLength, SeekOrigin.End);
        stream.ReadExactly(buffer);

        var endRecordIndex = -1;
        for (var index = buffer.Length - minimumEndOfCentralDirectorySize; index >= 0; index -= 1)
        {
            if (BitConverter.ToUInt32(buffer, index) == endOfCentralDirectorySignature)
            {
                endRecordIndex = index;
                break;
            }
        }

        if (endRecordIndex < 0)
        {
            return [];
        }

        var centralDirectoryOffset = BitConverter.ToUInt32(buffer, endRecordIndex + 16);
        stream.Seek(centralDirectoryOffset, SeekOrigin.Begin);

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var results = new List<ZipCentralDirectoryEntryName>();
        while (stream.Position <= stream.Length - 46)
        {
            if (reader.ReadUInt32() != centralDirectoryHeaderSignature)
            {
                break;
            }

            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            var generalPurposeFlag = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var fileNameLength = reader.ReadUInt16();
            var extraFieldLength = reader.ReadUInt16();
            var fileCommentLength = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();

            var fileNameBytes = reader.ReadBytes(fileNameLength);
            stream.Seek(extraFieldLength + fileCommentLength, SeekOrigin.Current);

            results.Add(new ZipCentralDirectoryEntryName(
                fileNameBytes,
                (generalPurposeFlag & 0x0800) != 0));
        }

        return results;
    }
}

internal sealed record ExtractedWorldLayout(string WorldRootPath, string TargetDirectoryName);
internal sealed record ZipCentralDirectoryEntryName(byte[] FileNameBytes, bool IsUtf8);

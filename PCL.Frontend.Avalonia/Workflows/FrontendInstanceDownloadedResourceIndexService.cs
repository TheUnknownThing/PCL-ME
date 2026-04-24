using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal enum FrontendDownloadedResourceMatchKind
{
    None,
    ExactInstalled,
    ExactDisabled,
    DifferentVersion
}

internal sealed record FrontendDownloadedResourceFileMatch(
    FrontendDownloadedResourceIndexEntry Entry,
    bool HashVerified);

internal sealed record FrontendInstalledCommunityResourceMatch(
    FrontendInstanceResourceEntry Entry,
    FrontendDownloadedResourceIndexEntry IndexEntry,
    FrontendDownloadedResourceMatchKind Kind)
{
    public bool IsInstalled => Kind is FrontendDownloadedResourceMatchKind.ExactInstalled
        or FrontendDownloadedResourceMatchKind.ExactDisabled
        or FrontendDownloadedResourceMatchKind.DifferentVersion;
}

internal sealed record FrontendDownloadedResourceInstallRecord(
    string Kind,
    string Source,
    string ProjectId,
    string Title,
    string ReleaseTitle,
    string? ReleaseId,
    string? FileId,
    string? SuggestedFileName,
    string? Sha1,
    string? Sha512,
    string InstalledPath,
    DateTimeOffset InstalledAt);

internal sealed record FrontendDownloadedResourceMoveRecord(
    string OldPath,
    string NewPath);

internal sealed class FrontendDownloadedResourceIndexEntry
{
    public string Kind { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ReleaseTitle { get; set; } = string.Empty;

    public string ReleaseId { get; set; } = string.Empty;

    public string FileId { get; set; } = string.Empty;

    public string SuggestedFileName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Sha1 { get; set; } = string.Empty;

    public string Sha512 { get; set; } = string.Empty;

    public long InstalledAtUnixTime { get; set; }
}

internal sealed class FrontendDownloadedResourceIndexDocument
{
    public int Version { get; set; } = 1;

    public List<FrontendDownloadedResourceIndexEntry> Entries { get; set; } = [];
}

internal static class FrontendInstanceDownloadedResourceIndexService
{
    private const string IndexRelativePath = "PCL/downloaded-resources.v1.json";
    private const int MaxIndexDocumentCacheEntries = 128;
    private const int MaxFileHashCacheEntries = 4096;
    private static readonly object IndexDocumentCacheLock = new();
    private static readonly object FileHashCacheLock = new();
    private static readonly Dictionary<string, IndexDocumentCacheEntry> IndexDocumentCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FileHashCacheEntry> FileHashCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record IndexDocumentCacheEntry(
        long Length,
        DateTime LastWriteTimeUtc,
        FrontendDownloadedResourceIndexDocument Document);

    private sealed class FileHashCacheEntry(long length, DateTime lastWriteTimeUtc)
    {
        public long Length { get; } = length;

        public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;

        public string Sha1 { get; set; } = string.Empty;

        public string Sha512 { get; set; } = string.Empty;
    }

    public static void RecordInstalledResource(
        string instanceIndieDirectory,
        FrontendDownloadedResourceInstallRecord record)
    {
        if (string.IsNullOrWhiteSpace(instanceIndieDirectory)
            || string.IsNullOrWhiteSpace(record.InstalledPath)
            || !File.Exists(record.InstalledPath))
        {
            return;
        }

        try
        {
            var document = ReadDocument(instanceIndieDirectory);
            var relativePath = NormalizeRelativePath(instanceIndieDirectory, record.InstalledPath);
            var fileName = Path.GetFileName(record.InstalledPath);
            var sha1 = NormalizeHash(record.Sha1);
            var sha512 = NormalizeHash(record.Sha512);
            if (string.IsNullOrWhiteSpace(sha1))
            {
                sha1 = ComputeSha1(record.InstalledPath);
            }

            document.Entries.RemoveAll(entry =>
                IsSameResource(entry, record.Kind, record.Source, record.ProjectId, record.ReleaseId, record.FileId)
                || (!string.IsNullOrWhiteSpace(relativePath)
                    && string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)));

            document.Entries.Add(new FrontendDownloadedResourceIndexEntry
            {
                Kind = NormalizeKind(record.Kind),
                Source = record.Source.Trim(),
                ProjectId = record.ProjectId.Trim(),
                Title = record.Title.Trim(),
                ReleaseTitle = record.ReleaseTitle.Trim(),
                ReleaseId = record.ReleaseId?.Trim() ?? string.Empty,
                FileId = record.FileId?.Trim() ?? string.Empty,
                SuggestedFileName = record.SuggestedFileName?.Trim() ?? string.Empty,
                FileName = fileName,
                RelativePath = relativePath,
                Sha1 = sha1,
                Sha512 = sha512,
                InstalledAtUnixTime = record.InstalledAt.ToUnixTimeSeconds()
            });
            WriteDocument(instanceIndieDirectory, document);
        }
        catch
        {
            // The index is only provenance metadata; installation must remain successful if it cannot be updated.
        }
    }

    public static void RecordResourceMoved(string instanceIndieDirectory, FrontendDownloadedResourceMoveRecord move)
    {
        if (string.IsNullOrWhiteSpace(instanceIndieDirectory)
            || string.IsNullOrWhiteSpace(move.OldPath)
            || string.IsNullOrWhiteSpace(move.NewPath))
        {
            return;
        }

        try
        {
            var document = ReadDocument(instanceIndieDirectory);
            var oldRelativePath = NormalizeRelativePath(instanceIndieDirectory, move.OldPath);
            var newRelativePath = NormalizeRelativePath(instanceIndieDirectory, move.NewPath);
            var newSha1 = File.Exists(move.NewPath) ? ComputeSha1(move.NewPath) : string.Empty;
            var changed = false;

            foreach (var entry in document.Entries)
            {
                if (!string.Equals(entry.RelativePath, oldRelativePath, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(newSha1)
                        || !string.Equals(entry.Sha1, newSha1, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                entry.RelativePath = newRelativePath;
                entry.FileName = Path.GetFileName(move.NewPath);
                changed = true;
            }

            if (changed)
            {
                WriteDocument(instanceIndieDirectory, document);
            }
        }
        catch
        {
            // Best effort metadata maintenance.
        }
    }

    public static void RemoveResourceRecord(string instanceIndieDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(instanceIndieDirectory) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var document = ReadDocument(instanceIndieDirectory);
            var relativePath = NormalizeRelativePath(instanceIndieDirectory, path);
            var removed = document.Entries.RemoveAll(entry =>
                string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                WriteDocument(instanceIndieDirectory, document);
            }
        }
        catch
        {
            // Best effort metadata maintenance.
        }
    }

    public static FrontendDownloadedResourceFileMatch? FindRecordForArtifact(
        string instanceIndieDirectory,
        string artifactPath,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(instanceIndieDirectory)
            || string.IsNullOrWhiteSpace(artifactPath)
            || !File.Exists(artifactPath))
        {
            return null;
        }

        try
        {
            var document = ReadDocument(instanceIndieDirectory);
            var normalizedKind = NormalizeKind(kind);
            var relativePath = NormalizeRelativePath(instanceIndieDirectory, artifactPath);
            var pathMatch = document.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
            if (pathMatch is not null)
            {
                if (string.IsNullOrWhiteSpace(pathMatch.Sha1))
                {
                    return new FrontendDownloadedResourceFileMatch(pathMatch, HashVerified: false);
                }

                var sha1 = ComputeSha1(artifactPath);
                return string.Equals(pathMatch.Sha1, sha1, StringComparison.OrdinalIgnoreCase)
                    ? new FrontendDownloadedResourceFileMatch(pathMatch, HashVerified: true)
                    : null;
            }

            var fileSha1 = ComputeSha1(artifactPath);
            var hashMatch = document.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.Sha1)
                && string.Equals(entry.Sha1, fileSha1, StringComparison.OrdinalIgnoreCase));
            return hashMatch is null ? null : new FrontendDownloadedResourceFileMatch(hashMatch, HashVerified: true);
        }
        catch
        {
            return null;
        }
    }

    public static FrontendInstalledCommunityResourceMatch? FindInstalledMod(
        FrontendInstanceComposition composition,
        FrontendCommunityProjectState project,
        FrontendCommunityProjectReleaseEntry? release)
    {
        if (!composition.Selection.HasSelection
            || string.IsNullOrWhiteSpace(project.ProjectId)
            || string.IsNullOrWhiteSpace(project.Source))
        {
            return null;
        }

        var entries = composition.Mods.Entries
            .Concat(composition.DisabledMods.Entries)
            .ToArray();
        foreach (var entry in entries)
        {
            var fileMatch = FindRecordForArtifact(composition.Selection.IndieDirectory, entry.Path, "mod");
            if (fileMatch is null
                || !MatchesProject(fileMatch.Entry, project.Source, project.ProjectId))
            {
                continue;
            }

            var kind = IsSameRelease(fileMatch.Entry, release)
                ? entry.IsEnabled ? FrontendDownloadedResourceMatchKind.ExactInstalled : FrontendDownloadedResourceMatchKind.ExactDisabled
                : FrontendDownloadedResourceMatchKind.DifferentVersion;
            return new FrontendInstalledCommunityResourceMatch(entry, fileMatch.Entry, kind);
        }

        if (release is null || (string.IsNullOrWhiteSpace(release.Sha1) && string.IsNullOrWhiteSpace(release.Sha512)))
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (!File.Exists(entry.Path))
            {
                continue;
            }

            var matchesSha1 = !string.IsNullOrWhiteSpace(release.Sha1)
                              && string.Equals(ComputeSha1(entry.Path), NormalizeHash(release.Sha1), StringComparison.OrdinalIgnoreCase);
            var matchesSha512 = !matchesSha1
                                && !string.IsNullOrWhiteSpace(release.Sha512)
                                && string.Equals(ComputeSha512(entry.Path), NormalizeHash(release.Sha512), StringComparison.OrdinalIgnoreCase);
            if (!matchesSha1 && !matchesSha512)
            {
                continue;
            }

            var synthesized = new FrontendDownloadedResourceIndexEntry
            {
                Kind = "mod",
                Source = project.Source,
                ProjectId = project.ProjectId,
                Title = project.Title,
                ReleaseTitle = release.Title,
                ReleaseId = release.ReleaseId ?? string.Empty,
                FileId = release.FileId ?? string.Empty,
                SuggestedFileName = release.SuggestedFileName ?? string.Empty,
                FileName = Path.GetFileName(entry.Path),
                RelativePath = NormalizeRelativePath(composition.Selection.IndieDirectory, entry.Path),
                Sha1 = NormalizeHash(release.Sha1),
                Sha512 = NormalizeHash(release.Sha512)
            };
            return new FrontendInstalledCommunityResourceMatch(
                entry,
                synthesized,
                entry.IsEnabled ? FrontendDownloadedResourceMatchKind.ExactInstalled : FrontendDownloadedResourceMatchKind.ExactDisabled);
        }

        return null;
    }

    private static bool MatchesProject(FrontendDownloadedResourceIndexEntry entry, string source, string projectId)
    {
        return string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase)
               && string.Equals(entry.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameRelease(
        FrontendDownloadedResourceIndexEntry entry,
        FrontendCommunityProjectReleaseEntry? release)
    {
        if (release is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(release.ReleaseId)
            && string.Equals(entry.ReleaseId, release.ReleaseId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(release.FileId)
            && string.Equals(entry.FileId, release.FileId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (!string.IsNullOrWhiteSpace(release.Sha1)
                && string.Equals(entry.Sha1, NormalizeHash(release.Sha1), StringComparison.OrdinalIgnoreCase))
               || (!string.IsNullOrWhiteSpace(release.Sha512)
                   && string.Equals(entry.Sha512, NormalizeHash(release.Sha512), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameResource(
        FrontendDownloadedResourceIndexEntry entry,
        string kind,
        string source,
        string projectId,
        string? releaseId,
        string? fileId)
    {
        if (!string.Equals(entry.Kind, NormalizeKind(kind), StringComparison.OrdinalIgnoreCase)
            || !MatchesProject(entry, source, projectId))
        {
            return false;
        }

        if (NormalizeKind(kind) == "mod")
        {
            return true;
        }

        return (!string.IsNullOrWhiteSpace(releaseId)
                && string.Equals(entry.ReleaseId, releaseId, StringComparison.OrdinalIgnoreCase))
               || (!string.IsNullOrWhiteSpace(fileId)
                   && string.Equals(entry.FileId, fileId, StringComparison.OrdinalIgnoreCase));
    }

    private static FrontendDownloadedResourceIndexDocument ReadDocument(string instanceIndieDirectory)
    {
        var path = ResolveIndexPath(instanceIndieDirectory);
        FileInfo file;
        try
        {
            file = new FileInfo(path);
            if (!file.Exists)
            {
                return new FrontendDownloadedResourceIndexDocument();
            }
        }
        catch
        {
            return new FrontendDownloadedResourceIndexDocument();
        }

        lock (IndexDocumentCacheLock)
        {
            if (IndexDocumentCache.TryGetValue(file.FullName, out var cached)
                && cached.Length == file.Length
                && cached.LastWriteTimeUtc == file.LastWriteTimeUtc)
            {
                return cached.Document;
            }
        }

        FrontendDownloadedResourceIndexDocument document;
        try
        {
            document = JsonSerializer.Deserialize<FrontendDownloadedResourceIndexDocument>(File.ReadAllText(file.FullName), JsonOptions)
                       ?? new FrontendDownloadedResourceIndexDocument();
        }
        catch
        {
            document = new FrontendDownloadedResourceIndexDocument();
        }

        lock (IndexDocumentCacheLock)
        {
            if (IndexDocumentCache.Count >= MaxIndexDocumentCacheEntries)
            {
                IndexDocumentCache.Clear();
            }

            IndexDocumentCache[file.FullName] = new IndexDocumentCacheEntry(
                file.Length,
                file.LastWriteTimeUtc,
                document);
        }

        return document;
    }

    private static void WriteDocument(string instanceIndieDirectory, FrontendDownloadedResourceIndexDocument document)
    {
        var path = ResolveIndexPath(instanceIndieDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        try
        {
            var file = new FileInfo(path);
            lock (IndexDocumentCacheLock)
            {
                if (IndexDocumentCache.Count >= MaxIndexDocumentCacheEntries)
                {
                    IndexDocumentCache.Clear();
                }

                IndexDocumentCache[file.FullName] = new IndexDocumentCacheEntry(
                    file.Length,
                    file.LastWriteTimeUtc,
                    document);
            }
        }
        catch
        {
            lock (IndexDocumentCacheLock)
            {
                IndexDocumentCache.Remove(path);
            }
        }
    }

    private static string ResolveIndexPath(string instanceIndieDirectory)
    {
        return Path.Combine(instanceIndieDirectory, IndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string NormalizeRelativePath(string rootDirectory, string path)
    {
        try
        {
            return Path.GetRelativePath(rootDirectory, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }
        catch
        {
            return Path.GetFileName(path);
        }
    }

    private static string NormalizeKind(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "mod" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeHash(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string ComputeSha1(string path)
    {
        return ComputeCachedHash(path, sha512: false);
    }

    private static string ComputeSha512(string path)
    {
        return ComputeCachedHash(path, sha512: true);
    }

    private static string ComputeCachedHash(string path, bool sha512)
    {
        FileInfo file;
        try
        {
            file = new FileInfo(path);
            if (!file.Exists)
            {
                return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        lock (FileHashCacheLock)
        {
            if (FileHashCache.TryGetValue(file.FullName, out var cached)
                && cached.Length == file.Length
                && cached.LastWriteTimeUtc == file.LastWriteTimeUtc)
            {
                var cachedHash = sha512 ? cached.Sha512 : cached.Sha1;
                if (!string.IsNullOrWhiteSpace(cachedHash))
                {
                    return cachedHash;
                }
            }
        }

        string hash;
        using (var stream = file.OpenRead())
        {
            hash = Convert.ToHexString(sha512 ? SHA512.HashData(stream) : SHA1.HashData(stream)).ToLowerInvariant();
        }

        lock (FileHashCacheLock)
        {
            if (FileHashCache.Count >= MaxFileHashCacheEntries)
            {
                FileHashCache.Clear();
            }

            if (!FileHashCache.TryGetValue(file.FullName, out var cached)
                || cached.Length != file.Length
                || cached.LastWriteTimeUtc != file.LastWriteTimeUtc)
            {
                cached = new FileHashCacheEntry(file.Length, file.LastWriteTimeUtc);
                FileHashCache[file.FullName] = cached;
            }

            if (sha512)
            {
                cached.Sha512 = hash;
            }
            else
            {
                cached.Sha1 = hash;
            }
        }

        return hash;
    }
}

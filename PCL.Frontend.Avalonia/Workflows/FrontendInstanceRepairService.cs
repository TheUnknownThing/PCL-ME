using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendInstanceRepairService
{
    private static readonly HttpClient HttpClient = new();

    public static FrontendInstanceRepairResult Repair(
        FrontendInstanceRepairRequest request,
        Action<FrontendInstanceRepairProgressSnapshot>? onProgress = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtimeArchitecture = FrontendLibraryArtifactResolver.GetCurrentRuntimeArchitecture();
        var filePlans = new Dictionary<string, FrontendInstanceRepairFilePlan>(StringComparer.OrdinalIgnoreCase);
        var downloadedFiles = new List<string>();
        var reusedFiles = new List<string>();
        var manifestDocuments = LoadManifestDocuments(request.LauncherDirectory, request.InstanceName);
        var progressTracker = onProgress is null ? null : new FrontendInstanceRepairProgressTracker(onProgress);

        if (manifestDocuments.Count == 0)
        {
            throw new InvalidOperationException("当前实例缺少可读取的版本清单。");
        }

        JsonElement? effectiveAssetIndex = null;
        foreach (var (versionName, root) in manifestDocuments)
        {
            AddClientDownload(filePlans, root, request.LauncherDirectory, versionName, request.ForceCoreRefresh);
            AddLibraryDownloads(filePlans, root, request.LauncherDirectory, runtimeArchitecture, request.ForceCoreRefresh);

            if (effectiveAssetIndex is null &&
                root.TryGetProperty("assetIndex", out var assetIndex) &&
                assetIndex.ValueKind == JsonValueKind.Object)
            {
                effectiveAssetIndex = assetIndex;
            }
        }

        if (effectiveAssetIndex is JsonElement resolvedAssetIndex)
        {
            var assetIndexPlan = CreateAssetIndexDownload(request.LauncherDirectory, resolvedAssetIndex, request.ForceCoreRefresh);
            if (assetIndexPlan is not null)
            {
                progressTracker?.UpsertPlan(assetIndexPlan);
                MaterializeFile(assetIndexPlan, downloadedFiles, reusedFiles, progressTracker, cancelToken);
            }

            AddAssetObjectDownloads(filePlans, request.LauncherDirectory, request.InstanceDirectory, resolvedAssetIndex);
        }

        progressTracker?.UpsertPlans(filePlans.Values);
        foreach (var plan in filePlans.Values)
        {
            MaterializeFile(plan, downloadedFiles, reusedFiles, progressTracker, cancelToken);
        }

        return new FrontendInstanceRepairResult(downloadedFiles, reusedFiles);
    }

    private static List<(string VersionName, JsonElement Root)> LoadManifestDocuments(string launcherDirectory, string instanceName)
    {
        var documents = new List<(string VersionName, JsonElement Root)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentVersion = instanceName;

        while (!string.IsNullOrWhiteSpace(currentVersion) && visited.Add(currentVersion))
        {
            var manifestPath = Path.Combine(launcherDirectory, "versions", currentVersion, $"{currentVersion}.json");
            if (!File.Exists(manifestPath))
            {
                break;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var raw = document.RootElement.GetRawText();
            using var detached = JsonDocument.Parse(raw);
            var root = detached.RootElement.Clone();
            documents.Add((currentVersion, root));
            currentVersion = GetString(root, "inheritsFrom");
        }

        return documents;
    }

    private static void AddClientDownload(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement root,
        string launcherDirectory,
        string versionName,
        bool forceDownload)
    {
        if (!root.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("client", out var client) ||
            client.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var url = GetString(client, "url");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var localPath = Path.Combine(launcherDirectory, "versions", versionName, $"{versionName}.jar");
        AddOrMergeFilePlan(
            filePlans,
            new FrontendInstanceRepairFilePlan(
                localPath,
                [url],
                GetString(client, "sha1"),
                GetLong(client, "size"),
                forceDownload,
                FrontendInstanceRepairFileGroup.Client));
    }

    private static void AddLibraryDownloads(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement root,
        string launcherDirectory,
        MachineType runtimeArchitecture,
        bool forceDownload)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!FrontendLibraryArtifactResolver.IsLibraryAllowed(library, runtimeArchitecture))
            {
                continue;
            }

            AddArtifactDownload(filePlans, library, launcherDirectory, runtimeArchitecture, forceDownload);
            AddNativeDownload(filePlans, library, launcherDirectory, runtimeArchitecture, forceDownload);
        }
    }

    private static void AddArtifactDownload(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement library,
        string launcherDirectory,
        MachineType runtimeArchitecture,
        bool forceDownload)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveArtifactDownload(
                library,
                launcherDirectory,
                runtimeArchitecture,
                out var resolved))
        {
            return;
        }

        var filePlan = new FrontendInstanceRepairFilePlan(
            resolved.TargetPath,
            string.IsNullOrWhiteSpace(resolved.DownloadUrl) ? [] : [resolved.DownloadUrl],
            resolved.Sha1,
            resolved.Size,
            false,
            FrontendInstanceRepairFileGroup.Libraries);
        var shouldForceDownload = (forceDownload || filePlan.ForceDownload) && filePlan.Urls.Count > 0;
        AddOrMergeFilePlan(filePlans, filePlan with { ForceDownload = shouldForceDownload });
    }

    private static void AddNativeDownload(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement library,
        string launcherDirectory,
        MachineType runtimeArchitecture,
        bool forceDownload)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveNativeArchiveDownload(
                library,
                launcherDirectory,
                runtimeArchitecture,
                out var resolved))
        {
            return;
        }

        var filePlan = new FrontendInstanceRepairFilePlan(
            resolved.TargetPath,
            string.IsNullOrWhiteSpace(resolved.DownloadUrl) ? [] : [resolved.DownloadUrl],
            resolved.Sha1,
            resolved.Size,
            false,
            FrontendInstanceRepairFileGroup.Libraries);
        var shouldForceDownload = (forceDownload || filePlan.ForceDownload) && filePlan.Urls.Count > 0;
        AddOrMergeFilePlan(filePlans, filePlan with { ForceDownload = shouldForceDownload });
    }

    private static bool TryGetLibraryDownload(
        JsonElement library,
        string entryName,
        string launcherDirectory,
        out FrontendInstanceRepairFilePlan filePlan)
    {
        filePlan = default!;
        if (!library.TryGetProperty("downloads", out var downloads) || downloads.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement downloadEntry;
        if (string.Equals(entryName, "artifact", StringComparison.Ordinal))
        {
            if (!downloads.TryGetProperty("artifact", out downloadEntry) || downloadEntry.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
        }
        else
        {
            if (!downloads.TryGetProperty("classifiers", out var classifiers) ||
                classifiers.ValueKind != JsonValueKind.Object ||
                !classifiers.TryGetProperty(entryName, out downloadEntry) ||
                downloadEntry.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
        }

        var path = GetString(downloadEntry, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            var libraryName = GetString(library, "name");
            if (string.IsNullOrWhiteSpace(libraryName))
            {
                return false;
            }

            path = DeriveLibraryPathFromName(libraryName, string.Equals(entryName, "artifact", StringComparison.Ordinal) ? null : entryName);
        }

        var localPath = Path.Combine(launcherDirectory, "libraries", path.Replace('/', Path.DirectorySeparatorChar));
        var hasExplicitUrl = downloadEntry.TryGetProperty("url", out var urlElement);
        var url = hasExplicitUrl && urlElement.ValueKind == JsonValueKind.String
            ? urlElement.GetString()
            : GetString(downloadEntry, "url");
        if (hasExplicitUrl && string.IsNullOrWhiteSpace(url))
        {
            filePlan = new FrontendInstanceRepairFilePlan(
                localPath,
                [],
                GetString(downloadEntry, "sha1"),
                GetLong(downloadEntry, "size"),
                false,
                FrontendInstanceRepairFileGroup.Libraries);
            return true;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            url = BuildLibraryUrl(library, path);
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        filePlan = new FrontendInstanceRepairFilePlan(
            localPath,
            [url],
            GetString(downloadEntry, "sha1"),
            GetLong(downloadEntry, "size"),
            false,
            FrontendInstanceRepairFileGroup.Libraries);
        return true;
    }

    private static FrontendInstanceRepairFilePlan? CreateAssetIndexDownload(
        string launcherDirectory,
        JsonElement assetIndex,
        bool forceDownload)
    {
        var indexName = GetString(assetIndex, "id");
        var url = GetString(assetIndex, "url");
        if (string.IsNullOrWhiteSpace(indexName) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new FrontendInstanceRepairFilePlan(
            Path.Combine(launcherDirectory, "assets", "indexes", $"{indexName}.json"),
            [url],
            GetString(assetIndex, "sha1"),
            GetLong(assetIndex, "size"),
            forceDownload,
            FrontendInstanceRepairFileGroup.AssetIndex);
    }

    private static void AddAssetObjectDownloads(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        string launcherDirectory,
        string instanceDirectory,
        JsonElement assetIndex)
    {
        var indexName = GetString(assetIndex, "id");
        if (string.IsNullOrWhiteSpace(indexName))
        {
            return;
        }

        var indexPath = Path.Combine(launcherDirectory, "assets", "indexes", $"{indexName}.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var mapToResources = GetBool(root, "map_to_resources");
        var useVirtualAssets = GetBool(root, "virtual");

        foreach (var asset in objects.EnumerateObject())
        {
            var hash = GetString(asset.Value, "hash");
            if (string.IsNullOrWhiteSpace(hash))
            {
                continue;
            }

            var localPath = mapToResources
                ? Path.Combine(instanceDirectory, "resources", asset.Name.Replace('/', Path.DirectorySeparatorChar))
                : useVirtualAssets
                    ? Path.Combine(launcherDirectory, "assets", "virtual", "legacy", asset.Name.Replace('/', Path.DirectorySeparatorChar))
                    : Path.Combine(launcherDirectory, "assets", "objects", hash[..2], hash);

            AddOrMergeFilePlan(
                filePlans,
                new FrontendInstanceRepairFilePlan(
                    localPath,
                    [$"https://resources.download.minecraft.net/{hash[..2]}/{hash}"],
                    hash,
                    GetLong(asset.Value, "size"),
                    false,
                    FrontendInstanceRepairFileGroup.Assets));
        }
    }

    private static void AddOrMergeFilePlan(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        FrontendInstanceRepairFilePlan filePlan)
    {
        if (filePlans.TryGetValue(filePlan.LocalPath, out var existing))
        {
            filePlans[filePlan.LocalPath] = new FrontendInstanceRepairFilePlan(
                filePlan.LocalPath,
                existing.Urls.Count >= filePlan.Urls.Count ? existing.Urls : filePlan.Urls,
                existing.Sha1 ?? filePlan.Sha1,
                existing.Size ?? filePlan.Size,
                existing.ForceDownload || filePlan.ForceDownload,
                filePlan.Group);
            return;
        }

        filePlans[filePlan.LocalPath] = filePlan;
    }

    private static bool IsLibraryAllowed(JsonElement library, string osKey)
    {
        if (!library.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (!RuleMatches(rule, osKey))
            {
                continue;
            }

            allowed = string.Equals(GetString(rule, "action"), "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static bool RuleMatches(JsonElement rule, string osKey)
    {
        if (!rule.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var name = NormalizeOsName(GetString(os, "name"));
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, osKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var arch = GetString(os, "arch");
        if (string.Equals(arch, "x86", StringComparison.OrdinalIgnoreCase) && Environment.Is64BitOperatingSystem)
        {
            return false;
        }

        return true;
    }

    private static bool IsFileValid(FrontendInstanceRepairFilePlan filePlan)
    {
        if (!File.Exists(filePlan.LocalPath))
        {
            return false;
        }

        var fileInfo = new FileInfo(filePlan.LocalPath);
        if (filePlan.Size is long size && size > 0 && fileInfo.Length != size)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePlan.Sha1))
        {
            return true;
        }

        using var stream = File.OpenRead(filePlan.LocalPath);
        var hash = Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
        return string.Equals(hash, filePlan.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static void MaterializeFile(
        FrontendInstanceRepairFilePlan filePlan,
        ICollection<string> downloadedFiles,
        ICollection<string> reusedFiles,
        FrontendInstanceRepairProgressTracker? progressTracker,
        CancellationToken cancelToken)
    {
        var isFileValid = IsFileValid(filePlan);
        if (!filePlan.ForceDownload && isFileValid)
        {
            reusedFiles.Add(filePlan.LocalPath);
            progressTracker?.MarkReused(filePlan);
            return;
        }

        if (filePlan.Urls.Count == 0)
        {
            if (isFileValid)
            {
                reusedFiles.Add(filePlan.LocalPath);
                progressTracker?.MarkReused(filePlan);
                return;
            }

            throw new InvalidOperationException($"实例修复文件缺少可用下载源：{filePlan.LocalPath}");
        }

        DownloadFile(filePlan, progressTracker, cancelToken);
        downloadedFiles.Add(filePlan.LocalPath);
        progressTracker?.MarkDownloaded(filePlan);
    }

    private static void DownloadFile(
        FrontendInstanceRepairFilePlan filePlan,
        FrontendInstanceRepairProgressTracker? progressTracker,
        CancellationToken cancelToken)
    {
        Exception? lastError = null;
        var tempPath = $"{filePlan.LocalPath}.download";

        foreach (var url in filePlan.Urls)
        {
            try
            {
                cancelToken.ThrowIfCancellationRequested();
                progressTracker?.StartDownload(filePlan);

                using var response = HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelToken).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStreamAsync(cancelToken).GetAwaiter().GetResult();

                Directory.CreateDirectory(Path.GetDirectoryName(filePlan.LocalPath)!);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                using (var output = File.Create(tempPath))
                {
                    var buffer = new byte[81920];
                    long transferredBytes = 0;
                    while (true)
                    {
                        var read = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken).GetAwaiter().GetResult();
                        if (read <= 0)
                        {
                            break;
                        }

                        output.Write(buffer, 0, read);
                        transferredBytes += read;
                        progressTracker?.ReportDownloadProgress(filePlan, transferredBytes);
                    }
                }

                if (File.Exists(filePlan.LocalPath))
                {
                    File.Delete(filePlan.LocalPath);
                }

                File.Move(tempPath, filePlan.LocalPath);

                if (!IsFileValid(filePlan))
                {
                    throw new InvalidOperationException($"下载后的文件校验失败：{filePlan.LocalPath}");
                }

                return;
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            $"无法下载实例修复文件：{filePlan.LocalPath}",
            lastError);
    }

    private static string BuildLibraryUrl(JsonElement library, string relativePath)
    {
        var baseUrl = GetString(library, "url");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://libraries.minecraft.net/";
        }

        return $"{baseUrl.TrimEnd('/')}/{relativePath}";
    }

    private static string DeriveLibraryPathFromName(string name, string? classifier = null)
    {
        var segments = name.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            return name.Replace(':', '/');
        }

        var group = segments[0].Replace('.', '/');
        var artifact = segments[1];
        var version = segments[2];
        var resolvedClassifier = classifier;
        var extension = "jar";

        if (segments.Length >= 5 && !string.IsNullOrWhiteSpace(segments[4]))
        {
            extension = segments[4];
        }

        if (string.IsNullOrWhiteSpace(resolvedClassifier) && segments.Length >= 4)
        {
            resolvedClassifier = segments[3];
        }

        if (!string.IsNullOrWhiteSpace(resolvedClassifier))
        {
            ParseLibraryCoordinateExtension(ref resolvedClassifier, ref extension);
        }

        ParseLibraryCoordinateExtension(ref version, ref extension);
        var suffix = string.IsNullOrWhiteSpace(resolvedClassifier) ? string.Empty : $"-{resolvedClassifier}";
        return $"{group}/{artifact}/{version}/{artifact}-{version}{suffix}.{extension}";
    }

    private static void ParseLibraryCoordinateExtension(ref string? value, ref string extension)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var extensionIndex = value.IndexOf('@');
        if (extensionIndex < 0)
        {
            return;
        }

        if (extensionIndex < value.Length - 1)
        {
            extension = value[(extensionIndex + 1)..];
        }

        value = value[..extensionIndex];
    }

    private static string GetOsKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return "linux";
    }

    private static string? NormalizeOsName(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "osx" or "mac" or "macos" => "osx",
            "windows" => "windows",
            "linux" => "linux",
            _ => value?.ToLowerInvariant()
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numericResult))
        {
            return numericResult;
        }

        return long.TryParse(value.ToString(), out var parsedResult) ? parsedResult : null;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }
}

internal sealed record FrontendInstanceRepairRequest(
    string LauncherDirectory,
    string InstanceDirectory,
    string InstanceName,
    bool ForceCoreRefresh);

internal sealed record FrontendInstanceRepairResult(
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);

internal sealed record FrontendInstanceRepairFilePlan(
    string LocalPath,
    IReadOnlyList<string> Urls,
    string? Sha1,
    long? Size,
    bool ForceDownload,
    FrontendInstanceRepairFileGroup Group);

internal sealed class FrontendInstanceRepairProgressTracker(Action<FrontendInstanceRepairProgressSnapshot> onProgress)
{
    private const int ProgressIntervalMs = 120;
    private readonly Dictionary<string, ProgressEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastSampleBytes;
    private long _lastSampleTimestampMs;
    private long _lastProgressTimestampMs;
    private double _speedBytesPerSecond;
    private string _currentFileName = string.Empty;

    public void UpsertPlans(IEnumerable<FrontendInstanceRepairFilePlan> plans)
    {
        var changed = false;
        foreach (var plan in plans)
        {
            if (_entries.ContainsKey(plan.LocalPath))
            {
                continue;
            }

            _entries.Add(plan.LocalPath, new ProgressEntry(plan));
            changed = true;
        }

        if (changed)
        {
            Publish(forceEmit: true);
        }
    }

    public void UpsertPlan(FrontendInstanceRepairFilePlan plan)
    {
        UpsertPlans([plan]);
    }

    public void MarkReused(FrontendInstanceRepairFilePlan plan)
    {
        var entry = GetEntry(plan);
        entry.CurrentBytes = entry.TotalBytes;
        entry.IsCompleted = true;
        entry.WasDownloaded = false;
        entry.TransferBytes = 0;
        _currentFileName = Path.GetFileName(plan.LocalPath);
        Publish(forceEmit: true);
    }

    public void StartDownload(FrontendInstanceRepairFilePlan plan)
    {
        var entry = GetEntry(plan);
        entry.CurrentBytes = 0;
        entry.IsCompleted = false;
        entry.WasDownloaded = false;
        entry.TransferBytes = 0;
        _currentFileName = Path.GetFileName(plan.LocalPath);
        Publish(forceSpeedRefresh: true);
    }

    public void ReportDownloadProgress(FrontendInstanceRepairFilePlan plan, long transferredBytes)
    {
        var entry = GetEntry(plan);
        entry.CurrentBytes = Math.Clamp(transferredBytes, 0, entry.TotalBytes > 0 ? entry.TotalBytes : transferredBytes);
        entry.TransferBytes = transferredBytes;
        _currentFileName = Path.GetFileName(plan.LocalPath);
        Publish(forceSpeedRefresh: true);
    }

    public void MarkDownloaded(FrontendInstanceRepairFilePlan plan)
    {
        var entry = GetEntry(plan);
        entry.CurrentBytes = entry.TotalBytes > 0 ? entry.TotalBytes : Math.Max(entry.CurrentBytes, 1);
        entry.IsCompleted = true;
        entry.WasDownloaded = true;
        entry.TransferBytes = entry.CurrentBytes;
        _currentFileName = Path.GetFileName(plan.LocalPath);
        Publish(forceSpeedRefresh: true, forceEmit: true);
    }

    private ProgressEntry GetEntry(FrontendInstanceRepairFilePlan plan)
    {
        if (_entries.TryGetValue(plan.LocalPath, out var entry))
        {
            return entry;
        }

        entry = new ProgressEntry(plan);
        _entries.Add(plan.LocalPath, entry);
        return entry;
    }

    private void Publish(bool forceSpeedRefresh = false, bool forceEmit = false)
    {
        var nowMs = _stopwatch.ElapsedMilliseconds;
        var completedBytes = _entries.Values.Sum(entry => entry.CompletedBytes);
        var transferredBytes = _entries.Values.Sum(entry => entry.TransferBytes);
        if (forceSpeedRefresh)
        {
            var elapsedMs = Math.Max(1, nowMs - _lastSampleTimestampMs);
            if (elapsedMs >= 180 || _lastSampleTimestampMs == 0)
            {
                _speedBytesPerSecond = Math.Max(0d, transferredBytes - _lastSampleBytes) * 1000d / elapsedMs;
                _lastSampleBytes = transferredBytes;
                _lastSampleTimestampMs = nowMs;
            }
        }

        if (!forceEmit && nowMs - _lastProgressTimestampMs < ProgressIntervalMs)
        {
            return;
        }

        var groups = _entries.Values
            .GroupBy(entry => entry.Plan.Group)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var active = group.FirstOrDefault(entry => !entry.IsCompleted && entry.CurrentBytes > 0)
                                 ?? group.FirstOrDefault(entry => !entry.IsCompleted)
                                 ?? group.First();
                    return new FrontendInstanceRepairGroupSnapshot(
                        group.Key,
                        group.Count(entry => entry.IsCompleted),
                        group.Count(),
                        group.Sum(entry => entry.CompletedBytes),
                        group.Sum(entry => entry.TotalBytes),
                        Path.GetFileName(active.Plan.LocalPath));
                });

        var totalFiles = _entries.Count;
        var completedFiles = _entries.Values.Count(entry => entry.IsCompleted);
        _lastProgressTimestampMs = nowMs;
        onProgress(
            new FrontendInstanceRepairProgressSnapshot(
                groups,
                _currentFileName,
                _entries.Values.Count(entry => entry.IsCompleted && entry.WasDownloaded),
                _entries.Values.Count(entry => entry.IsCompleted && !entry.WasDownloaded),
                totalFiles,
                Math.Max(0, totalFiles - completedFiles),
                completedBytes,
                _entries.Values.Sum(entry => entry.TotalBytes),
                _speedBytesPerSecond));
    }

    private sealed class ProgressEntry(FrontendInstanceRepairFilePlan plan)
    {
        public FrontendInstanceRepairFilePlan Plan { get; } = plan;

        public long TotalBytes { get; } = Math.Max(0, plan.Size ?? 0);

        public long CurrentBytes { get; set; }

        public bool IsCompleted { get; set; }

        public bool WasDownloaded { get; set; }

        public long TransferBytes { get; set; }

        public long CompletedBytes => IsCompleted
            ? (TotalBytes > 0 ? TotalBytes : CurrentBytes)
            : CurrentBytes;
    }
}

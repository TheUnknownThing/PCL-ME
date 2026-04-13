using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendInstanceRepairService
{
    private static readonly HttpClient HttpClient = new();

    public static FrontendInstanceRepairResult Repair(FrontendInstanceRepairRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var osKey = GetOsKey();
        var filePlans = new Dictionary<string, FrontendInstanceRepairFilePlan>(StringComparer.OrdinalIgnoreCase);
        var downloadedFiles = new List<string>();
        var reusedFiles = new List<string>();
        var manifestDocuments = LoadManifestDocuments(request.LauncherDirectory, request.InstanceName);

        if (manifestDocuments.Count == 0)
        {
            throw new InvalidOperationException("当前实例缺少可读取的版本清单。");
        }

        JsonElement? effectiveAssetIndex = null;
        foreach (var (versionName, root) in manifestDocuments)
        {
            AddClientDownload(filePlans, root, request.LauncherDirectory, versionName, request.ForceCoreRefresh);
            AddLibraryDownloads(filePlans, root, request.LauncherDirectory, osKey, request.ForceCoreRefresh);

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
                MaterializeFile(assetIndexPlan, downloadedFiles, reusedFiles);
            }

            AddAssetObjectDownloads(filePlans, request.LauncherDirectory, request.InstanceDirectory, resolvedAssetIndex);
        }

        foreach (var plan in filePlans.Values)
        {
            MaterializeFile(plan, downloadedFiles, reusedFiles);
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
                forceDownload));
    }

    private static void AddLibraryDownloads(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement root,
        string launcherDirectory,
        string osKey,
        bool forceDownload)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowed(library, osKey))
            {
                continue;
            }

            var name = GetString(library, "name");
            AddArtifactDownload(filePlans, library, launcherDirectory, forceDownload);
            AddNativeDownload(filePlans, library, launcherDirectory, osKey, name, forceDownload);
        }
    }

    private static void AddArtifactDownload(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement library,
        string launcherDirectory,
        bool forceDownload)
    {
        if (!TryGetLibraryDownload(library, "artifact", launcherDirectory, out var filePlan))
        {
            return;
        }

        var shouldForceDownload = (forceDownload || filePlan.ForceDownload) && filePlan.Urls.Count > 0;
        AddOrMergeFilePlan(filePlans, filePlan with { ForceDownload = shouldForceDownload });
    }

    private static void AddNativeDownload(
        IDictionary<string, FrontendInstanceRepairFilePlan> filePlans,
        JsonElement library,
        string launcherDirectory,
        string osKey,
        string? libraryName,
        bool forceDownload)
    {
        if (!library.TryGetProperty("natives", out var natives) || natives.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!natives.TryGetProperty(osKey, out var classifierValue) || classifierValue.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var classifier = classifierValue.GetString()?.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(classifier))
        {
            return;
        }

        if (!TryGetLibraryDownload(library, classifier, launcherDirectory, out var filePlan))
        {
            if (!string.IsNullOrWhiteSpace(libraryName))
            {
                var derivedPath = DeriveLibraryPathFromName(libraryName, classifier);
                var derivedUrl = BuildLibraryUrl(library, derivedPath);
                if (string.IsNullOrWhiteSpace(derivedUrl))
                {
                    return;
                }

                filePlan = new FrontendInstanceRepairFilePlan(
                    Path.Combine(launcherDirectory, "libraries", derivedPath.Replace('/', Path.DirectorySeparatorChar)),
                    [derivedUrl],
                    null,
                    null,
                    forceDownload);
            }
            else
            {
                return;
            }
        }

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
                false);
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
            false);
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
            forceDownload);
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
                    false));
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
                existing.ForceDownload || filePlan.ForceDownload);
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
        ICollection<string> reusedFiles)
    {
        var isFileValid = IsFileValid(filePlan);
        if (!filePlan.ForceDownload && isFileValid)
        {
            reusedFiles.Add(filePlan.LocalPath);
            return;
        }

        if (filePlan.Urls.Count == 0)
        {
            if (isFileValid)
            {
                reusedFiles.Add(filePlan.LocalPath);
                return;
            }

            throw new InvalidOperationException($"实例修复文件缺少可用下载源：{filePlan.LocalPath}");
        }

        DownloadFile(filePlan);
        downloadedFiles.Add(filePlan.LocalPath);
    }

    private static void DownloadFile(FrontendInstanceRepairFilePlan filePlan)
    {
        Exception? lastError = null;

        foreach (var url in filePlan.Urls)
        {
            try
            {
                var bytes = HttpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                Directory.CreateDirectory(Path.GetDirectoryName(filePlan.LocalPath)!);
                File.WriteAllBytes(filePlan.LocalPath, bytes);

                if (!IsFileValid(filePlan))
                {
                    throw new InvalidOperationException($"下载后的文件校验失败：{filePlan.LocalPath}");
                }

                return;
            }
            catch (Exception ex)
            {
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
        var extension = segments.Length >= 4 && !string.IsNullOrWhiteSpace(segments[3]) ? segments[3] : "jar";
        var suffix = string.IsNullOrWhiteSpace(classifier) ? string.Empty : $"-{classifier}";
        return $"{group}/{artifact}/{version}/{artifact}-{version}{suffix}.{extension}";
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
    bool ForceDownload);

using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftJavaRuntimeDownloadService
{
    public static MinecraftJavaRuntimeSelection SelectRuntime(MinecraftJavaRuntimeSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = ParseObject(request.IndexJson);
        var platformNode = root[request.PlatformKey] as JsonObject
                           ?? throw new InvalidOperationException($"Java 运行时索引缺少平台 {request.PlatformKey}。");

        JsonArray? runtimeEntries = null;
        var componentKey = request.RequestedComponent;
        var matchedByPrefix = false;
        if (platformNode[request.RequestedComponent] is JsonArray exactMatch)
        {
            runtimeEntries = exactMatch;
        }
        else
        {
            foreach (var property in platformNode)
            {
                var versionName = (property.Value as JsonArray)?
                    .FirstOrDefault()?["version"]?["name"]?.ToString();
                if (versionName is not null &&
                    versionName.StartsWith(request.RequestedComponent, StringComparison.OrdinalIgnoreCase))
                {
                    runtimeEntries = property.Value as JsonArray;
                    componentKey = property.Key;
                    matchedByPrefix = true;
                    break;
                }
            }
        }

        if (runtimeEntries is null)
        {
            throw new InvalidOperationException($"未能找到所需的 Java {request.RequestedComponent}");
        }

        var firstEntry = runtimeEntries.FirstOrDefault() as JsonObject
                         ?? throw new InvalidOperationException($"Mojang 未提供所需的 Java {componentKey}");
        var manifest = firstEntry["manifest"] as JsonObject
                       ?? throw new InvalidOperationException($"Java 运行时 {componentKey} 缺少 manifest。");

        return new MinecraftJavaRuntimeSelection(
            request.PlatformKey,
            request.RequestedComponent,
            componentKey,
            GetRequiredString(firstEntry["version"] as JsonObject, "name"),
            GetRequiredString(manifest, "url"),
            matchedByPrefix);
    }

    public static MinecraftJavaRuntimeDownloadPlan BuildDownloadPlan(MinecraftJavaRuntimeDownloadPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ignoredSha1Hashes = request.IgnoredSha1Hashes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                                [];
        var baseDirectory = NormalizeBaseDirectory(request.RuntimeBaseDirectory);
        var root = ParseObject(request.ManifestJson);
        var filesNode = root["files"] as JsonObject
                        ?? throw new InvalidOperationException("Java manifest 缺少 files 字段。");

        var filePlans = new List<MinecraftJavaRuntimeDownloadFilePlan>();
        foreach (var file in filesNode)
        {
            if (file.Value is not JsonObject fileObject)
            {
                continue;
            }

            var rawDownload = fileObject["downloads"]?["raw"] as JsonObject;
            if (rawDownload is null)
            {
                continue;
            }

            var sha1 = GetRequiredString(rawDownload, "sha1");
            if (ignoredSha1Hashes.Contains(sha1))
            {
                continue;
            }

            var relativePath = NormalizeRelativePath(file.Key);
            var targetPath = CombineRuntimePath(baseDirectory, relativePath);
            if (!IsPathWithinDirectory(targetPath, baseDirectory))
            {
                throw new InvalidOperationException($"{targetPath} 不在 {baseDirectory} 中");
            }

            filePlans.Add(new MinecraftJavaRuntimeDownloadFilePlan(
                relativePath,
                targetPath,
                GetRequiredString(rawDownload, "url"),
                GetRequiredLong(rawDownload, "size"),
                sha1));
        }

        return new MinecraftJavaRuntimeDownloadPlan(baseDirectory, filePlans);
    }

    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON 内容不能为空。", nameof(json));
        }

        return JsonNode.Parse(json) as JsonObject
               ?? throw new InvalidOperationException("JSON 内容不是对象。");
    }

    private static string GetRequiredString(JsonObject? obj, string propertyName)
    {
        var value = obj?[propertyName]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"JSON 内容缺少 {propertyName} 字段。");
        }

        return value;
    }

    private static long GetRequiredLong(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is null)
        {
            throw new InvalidOperationException($"JSON 内容缺少 {propertyName} 字段。");
        }

        if (obj[propertyName] is JsonValue value &&
            value.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        if (long.TryParse(obj[propertyName]!.ToString(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"JSON 中的 {propertyName} 字段不是数字。");
    }

    private static string NormalizeBaseDirectory(string runtimeBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(runtimeBaseDirectory))
        {
            throw new ArgumentException("运行时目录不能为空。", nameof(runtimeBaseDirectory));
        }

        return IsWindowsStyleAbsolutePath(runtimeBaseDirectory)
            ? runtimeBaseDirectory
                .Replace('/', '\\')
                .TrimEnd('\\')
            : Path.GetFullPath(runtimeBaseDirectory);
    }

    private static string CombineRuntimePath(string baseDirectory, string relativePath)
    {
        if (IsWindowsStyleAbsolutePath(baseDirectory))
        {
            return $"{baseDirectory}\\{relativePath.Replace('/', '\\')}";
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        if (IsWindowsStyleAbsolutePath(directory))
        {
            var normalizedDirectory = directory.TrimEnd('\\', '/') + "\\";
            return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        var normalized = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Java manifest 包含空文件路径。");
        }

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || IsWindowsStyleAbsolutePath(normalized))
        {
            throw new InvalidOperationException($"Java manifest 包含越界路径：{relativePath}");
        }

        var segments = new List<string>();
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidOperationException($"Java manifest 包含越界路径：{relativePath}");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new InvalidOperationException($"Java manifest 包含空文件路径：{relativePath}");
        }

        return string.Join("/", segments);
    }

    private static bool IsWindowsStyleAbsolutePath(string path)
    {
        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path[2] == '\\' || path[2] == '/');
    }

}

public sealed record MinecraftJavaRuntimeSelectionRequest(
    string IndexJson,
    string PlatformKey,
    string RequestedComponent);

public sealed record MinecraftJavaRuntimeSelection(
    string PlatformKey,
    string RequestedComponent,
    string ComponentKey,
    string VersionName,
    string ManifestUrl,
    bool MatchedByPrefix);

public sealed record MinecraftJavaRuntimeDownloadPlanRequest(
    string ManifestJson,
    string RuntimeBaseDirectory,
    IReadOnlyList<string>? IgnoredSha1Hashes = null);

public sealed record MinecraftJavaRuntimeDownloadPlan(
    string RuntimeBaseDirectory,
    IReadOnlyList<MinecraftJavaRuntimeDownloadFilePlan> Files);

public sealed record MinecraftJavaRuntimeDownloadFilePlan(
    string RelativePath,
    string TargetPath,
    string Url,
    long Size,
    string Sha1);

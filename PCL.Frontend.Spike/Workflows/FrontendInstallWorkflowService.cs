using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendInstallWorkflowService
{
    private const string MojangVersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<FrontendInstallChoice> GetMinecraftChoices(string? preferredVersion)
    {
        var root = ReadJsonObject(MojangVersionManifestUrl);
        if (root["versions"] is not JsonArray versions)
        {
            return [];
        }

        var choices = versions
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Where(node => string.Equals(node!["type"]?.GetValue<string>(), "release", StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var version = node!["id"]?.GetValue<string>() ?? string.Empty;
                var releaseTime = node["releaseTime"]?.GetValue<string>() ?? string.Empty;
                return new FrontendInstallChoice(
                    Id: $"minecraft:{version}",
                    Title: version,
                    Summary: string.IsNullOrWhiteSpace(releaseTime)
                        ? "正式版"
                        : $"正式版 • {FormatReleaseTime(releaseTime)}",
                    Version: version,
                    Kind: FrontendInstallChoiceKind.Minecraft,
                    ManifestUrl: node["url"]?.GetValue<string>());
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version) && !string.IsNullOrWhiteSpace(choice.ManifestUrl))
            .Take(36)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && choices.All(choice => !string.Equals(choice.Version, preferredVersion, StringComparison.OrdinalIgnoreCase)))
        {
            var extra = versions
                .Select(node => node as JsonObject)
                .FirstOrDefault(node => string.Equals(node?["id"]?.GetValue<string>(), preferredVersion, StringComparison.OrdinalIgnoreCase));
            if (extra is not null)
            {
                choices.Insert(
                    0,
                    new FrontendInstallChoice(
                        Id: $"minecraft:{preferredVersion}",
                        Title: preferredVersion,
                        Summary: $"当前实例 • {FormatReleaseTime(extra["releaseTime"]?.GetValue<string>())}",
                        Version: preferredVersion,
                        Kind: FrontendInstallChoiceKind.Minecraft,
                        ManifestUrl: extra["url"]?.GetValue<string>()));
            }
        }

        return choices;
    }

    public static IReadOnlyList<FrontendInstallChoice> GetSupportedChoices(
        string optionTitle,
        string minecraftVersion)
    {
        return optionTitle switch
        {
            "Fabric" => GetFabricLoaderChoices(minecraftVersion),
            "Legacy Fabric" => GetLegacyFabricLoaderChoices(minecraftVersion),
            "Quilt" => GetQuiltLoaderChoices(minecraftVersion),
            "LabyMod" => GetLabyModChoices(minecraftVersion),
            "Fabric API" => GetModrinthFileChoices("fabric-api", minecraftVersion, ["fabric"]),
            "Legacy Fabric API" => GetModrinthFileChoices("9CJED7xi", minecraftVersion, null),
            "QFAPI / QSL" => GetModrinthFileChoices("qvIfYCYJ", minecraftVersion, ["quilt"], allowVersionFallback: true),
            _ => []
        };
    }

    public static bool IsFrontendManagedOption(string optionTitle)
    {
        return optionTitle is "Fabric"
            or "Legacy Fabric"
            or "Quilt"
            or "LabyMod"
            or "Fabric API"
            or "Legacy Fabric API"
            or "QFAPI / QSL";
    }

    public static FrontendInstallApplyResult Apply(FrontendInstallApplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var launcherDirectory = request.LauncherDirectory;
        var targetDirectory = Path.Combine(launcherDirectory, "versions", request.TargetInstanceName);
        Directory.CreateDirectory(targetDirectory);

        var manifestNode = BuildTargetManifest(request);
        var manifestPath = Path.Combine(targetDirectory, $"{request.TargetInstanceName}.json");
        File.WriteAllText(manifestPath, manifestNode.ToJsonString(JsonNodeOptions), Utf8NoBom);

        var instanceConfig = OpenInstanceConfigProvider(targetDirectory);
        instanceConfig.Set("VersionVanillaName", request.MinecraftChoice.Version);
        instanceConfig.Set("VersionArgumentIndieV2", request.UseInstanceIsolation);
        instanceConfig.Sync();

        var modsDirectory = request.UseInstanceIsolation
            ? Path.Combine(targetDirectory, "mods")
            : Path.Combine(launcherDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);

        ApplyManagedModSelection(modsDirectory, "fabric-api", request.FabricApiChoice);
        ApplyManagedModSelection(modsDirectory, "legacy-fabric-api", request.LegacyFabricApiChoice);
        ApplyManagedModSelection(modsDirectory, "quilted-fabric-api", request.QslChoice);
        ApplyManagedModSelection(modsDirectory, "qsl", request.QslChoice);

        var repairResult = request.RunRepair
            ? FrontendInstanceRepairService.Repair(new FrontendInstanceRepairRequest(
                launcherDirectory,
                targetDirectory,
                request.TargetInstanceName,
                request.ForceCoreRefresh))
            : new FrontendInstanceRepairResult([], []);

        EnsureResourceFolders(targetDirectory, request.UseInstanceIsolation ? targetDirectory : launcherDirectory);

        return new FrontendInstallApplyResult(
            targetDirectory,
            manifestPath,
            repairResult.DownloadedFiles,
            repairResult.ReusedFiles);
    }

    private static readonly JsonSerializerOptions JsonNodeOptions = new()
    {
        WriteIndented = true
    };

    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(false);

    private static JsonObject BuildTargetManifest(FrontendInstallApplyRequest request)
    {
        var baseManifest = ReadJsonObject(request.MinecraftChoice.ManifestUrl ?? MojangVersionManifestUrl);
        JsonObject targetManifest;

        switch (request.PrimaryLoaderChoice?.Kind)
        {
            case FrontendInstallChoiceKind.FabricLoader:
            case FrontendInstallChoiceKind.LegacyFabricLoader:
            case FrontendInstallChoiceKind.QuiltLoader:
                targetManifest = MergeBaseAndLoaderManifest(
                    baseManifest,
                    ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                   ?? throw new InvalidOperationException("缺少安装器清单地址。")),
                    request.TargetInstanceName);
                break;
            case FrontendInstallChoiceKind.LabyMod:
                targetManifest = ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                               ?? throw new InvalidOperationException("缺少 LabyMod 清单地址。"));
                targetManifest["id"] = request.TargetInstanceName;
                break;
            default:
                targetManifest = CloneObject(baseManifest);
                targetManifest["id"] = request.TargetInstanceName;
                break;
        }

        targetManifest["id"] = request.TargetInstanceName;
        return targetManifest;
    }

    private static JsonObject MergeBaseAndLoaderManifest(
        JsonObject baseManifest,
        JsonObject loaderManifest,
        string targetInstanceName)
    {
        var merged = CloneObject(baseManifest);

        foreach (var pair in loaderManifest)
        {
            if (pair.Value is null)
            {
                continue;
            }

            if (pair.Key is "id" or "inheritsFrom" or "releaseTime" or "time")
            {
                continue;
            }

            if (pair.Key == "libraries")
            {
                merged["libraries"] = MergeLibraries(
                    merged["libraries"] as JsonArray,
                    pair.Value as JsonArray);
                continue;
            }

            if (pair.Key == "arguments")
            {
                merged["arguments"] = MergeArguments(
                    merged["arguments"] as JsonObject,
                    pair.Value as JsonObject);
                continue;
            }

            merged[pair.Key] = pair.Value.DeepClone();
        }

        merged.Remove("inheritsFrom");
        merged["id"] = targetInstanceName;
        return merged;
    }

    private static JsonArray MergeLibraries(JsonArray? baseLibraries, JsonArray? loaderLibraries)
    {
        var merged = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Append(JsonArray? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var node in source)
            {
                if (node is not JsonObject library)
                {
                    continue;
                }

                var name = library["name"]?.GetValue<string>() ?? library.ToJsonString();
                if (!seen.Add(name))
                {
                    continue;
                }

                merged.Add(library.DeepClone());
            }
        }

        Append(baseLibraries);
        Append(loaderLibraries);
        return merged;
    }

    private static JsonObject MergeArguments(JsonObject? baseArguments, JsonObject? loaderArguments)
    {
        var merged = baseArguments is null ? new JsonObject() : CloneObject(baseArguments);
        foreach (var key in new[] { "game", "jvm" })
        {
            var values = new JsonArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Append(JsonObject? source)
            {
                if (source?[key] is not JsonArray array)
                {
                    return;
                }

                foreach (var item in array)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    var signature = item.ToJsonString();
                    if (!seen.Add(signature))
                    {
                        continue;
                    }

                    values.Add(item.DeepClone());
                }
            }

            Append(baseArguments);
            Append(loaderArguments);
            if (values.Count > 0)
            {
                merged[key] = values;
            }
        }

        return merged;
    }

    private static IReadOnlyList<FrontendInstallChoice> GetFabricLoaderChoices(string minecraftVersion)
    {
        return ReadLoaderChoices(
            $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.FabricLoader,
            "Fabric");
    }

    private static IReadOnlyList<FrontendInstallChoice> GetLegacyFabricLoaderChoices(string minecraftVersion)
    {
        return ReadLoaderChoices(
            $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.LegacyFabricLoader,
            "Legacy Fabric");
    }

    private static IReadOnlyList<FrontendInstallChoice> GetQuiltLoaderChoices(string minecraftVersion)
    {
        var root = ReadJsonArray($"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}");
        return root
            .Select(node => node as JsonObject)
            .Where(node => node?["loader"] is JsonObject)
            .Select(node =>
            {
                var loader = (JsonObject)node!["loader"]!;
                var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                var profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{version}/profile/json";
                return new FrontendInstallChoice(
                    Id: $"quilt:{version}",
                    Title: version,
                    Summary: loader["maven"]?.GetValue<string>() ?? "Quilt 安装器",
                    Version: version,
                    Kind: FrontendInstallChoiceKind.QuiltLoader,
                    ManifestUrl: profileUrl);
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version))
            .Take(18)
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstallChoice> GetLabyModChoices(string minecraftVersion)
    {
        var production = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json");
        var snapshot = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json");
        var choices = new List<FrontendInstallChoice>();

        AddLabyChoice(choices, production, minecraftVersion, "production", "稳定版");
        AddLabyChoice(choices, snapshot, minecraftVersion, "snapshot", "快照版");
        return choices;
    }

    private static void AddLabyChoice(
        ICollection<FrontendInstallChoice> choices,
        JsonObject manifest,
        string minecraftVersion,
        string channel,
        string channelLabel)
    {
        if (manifest["minecraftVersions"] is not JsonArray versions)
        {
            return;
        }

        var versionEntry = versions
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => string.Equals(node?["version"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase));
        if (versionEntry is null)
        {
            return;
        }

        var commitReference = manifest["commitReference"]?.GetValue<string>() ?? string.Empty;
        var versionLabel = manifest["labyModVersion"]?.GetValue<string>() ?? commitReference;
        choices.Add(new FrontendInstallChoice(
            Id: $"labymod:{channel}:{commitReference}",
            Title: $"{versionLabel} {channelLabel}",
            Summary: $"LabyMod 4 • {minecraftVersion}",
            Version: versionLabel,
            Kind: FrontendInstallChoiceKind.LabyMod,
            ManifestUrl: versionEntry["customManifestUrl"]?.GetValue<string>()));
    }

    private static IReadOnlyList<FrontendInstallChoice> GetModrinthFileChoices(
        string projectId,
        string minecraftVersion,
        IReadOnlyList<string>? loaders,
        bool allowVersionFallback = false)
    {
        foreach (var candidateVersion in GetVersionCandidates(minecraftVersion, allowVersionFallback))
        {
            var url = BuildModrinthVersionUrl(projectId, candidateVersion, loaders);
            var root = ReadJsonArray(url);
            var choices = root
                .Select(node => node as JsonObject)
                .Where(node => node is not null)
                .Select(ToModrinthChoice)
                .Where(choice => choice is not null)
                .Cast<FrontendInstallChoice>()
                .Take(18)
                .ToArray();
            if (choices.Length > 0)
            {
                return choices;
            }
        }

        return [];
    }

    private static FrontendInstallChoice? ToModrinthChoice(JsonObject? version)
    {
        if (version?["files"] is not JsonArray files)
        {
            return null;
        }

        var primaryFile = files
            .Select(node => node as JsonObject)
            .FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
            ?? files.Select(node => node as JsonObject).FirstOrDefault(file => file is not null);
        if (primaryFile is null)
        {
            return null;
        }

        var title = version["version_number"]?.GetValue<string>()
                    ?? version["name"]?.GetValue<string>()
                    ?? primaryFile["filename"]?.GetValue<string>()
                    ?? string.Empty;
        var published = version["date_published"]?.GetValue<string>() ?? string.Empty;
        var versionType = version["version_type"]?.GetValue<string>() ?? "release";

        return new FrontendInstallChoice(
            Id: $"mod:{version["id"]?.GetValue<string>()}",
            Title: title,
            Summary: $"{NormalizeVersionType(versionType)} • {FormatReleaseTime(published)}",
            Version: title,
            Kind: FrontendInstallChoiceKind.ModFile,
            DownloadUrl: primaryFile["url"]?.GetValue<string>(),
            FileName: primaryFile["filename"]?.GetValue<string>());
    }

    private static IReadOnlyList<FrontendInstallChoice> ReadLoaderChoices(
        string url,
        FrontendInstallChoiceKind kind,
        string prefix)
    {
        var root = ReadJsonArray(url);
        return root
            .Select(node => node as JsonObject)
            .Where(node => node?["loader"] is JsonObject)
            .Select(node =>
            {
                var loader = (JsonObject)node!["loader"]!;
                var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                var gameVersion = node["intermediary"]?["version"]?.GetValue<string>() ?? string.Empty;
                return new FrontendInstallChoice(
                    Id: $"{kind}:{version}",
                    Title: version,
                    Summary: loader["stable"]?.GetValue<bool>() == true ? "稳定版" : $"{prefix} 测试版",
                    Version: version,
                    Kind: kind,
                    ManifestUrl: $"{url.TrimEnd('/')}/{version}/profile/json");
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version))
            .Take(18)
            .ToArray();
    }

    private static IEnumerable<string> GetVersionCandidates(string minecraftVersion, bool allowFallback)
    {
        yield return minecraftVersion;

        if (!allowFallback)
        {
            yield break;
        }

        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            yield return $"{parts[0]}.{parts[1]}";
        }
    }

    private static void ApplyManagedModSelection(
        string modsDirectory,
        string filePrefix,
        FrontendInstallChoice? selectedChoice)
    {
        foreach (var file in Directory.EnumerateFiles(modsDirectory, $"{filePrefix}*.jar", SearchOption.TopDirectoryOnly))
        {
            if (selectedChoice is not null
                && string.Equals(Path.GetFileName(file), selectedChoice.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(file);
        }

        if (selectedChoice is null || string.IsNullOrWhiteSpace(selectedChoice.DownloadUrl) || string.IsNullOrWhiteSpace(selectedChoice.FileName))
        {
            return;
        }

        var outputPath = Path.Combine(modsDirectory, selectedChoice.FileName);
        if (File.Exists(outputPath))
        {
            return;
        }

        Directory.CreateDirectory(modsDirectory);
        File.WriteAllBytes(outputPath, HttpClient.GetByteArrayAsync(selectedChoice.DownloadUrl).GetAwaiter().GetResult());
    }

    private static void EnsureResourceFolders(string instanceDirectory, string runtimeDirectory)
    {
        Directory.CreateDirectory(Path.Combine(runtimeDirectory, "resourcepacks"));
        Directory.CreateDirectory(Path.Combine(runtimeDirectory, "mods"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "PCL"));
    }

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        Directory.CreateDirectory(pclDirectory);
        return new YamlFileProvider(Path.Combine(pclDirectory, "config.v1.yml"));
    }

    private static JsonObject ReadJsonObject(string url)
    {
        var content = HttpClient.GetStringAsync(url).GetAwaiter().GetResult();
        return JsonNode.Parse(content)?.AsObject()
               ?? throw new InvalidOperationException($"无法读取 JSON 对象：{url}");
    }

    private static JsonArray ReadJsonArray(string url)
    {
        var content = HttpClient.GetStringAsync(url).GetAwaiter().GetResult();
        return JsonNode.Parse(content)?.AsArray()
               ?? throw new InvalidOperationException($"无法读取 JSON 数组：{url}");
    }

    private static JsonObject CloneObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())?.AsObject()
               ?? throw new InvalidOperationException("复制安装清单失败。");
    }

    private static string BuildModrinthVersionUrl(string projectId, string minecraftVersion, IReadOnlyList<string>? loaders)
    {
        var builder = new UriBuilder($"https://api.modrinth.com/v2/project/{projectId}/version");
        var query = new List<string> { $"game_versions=%5B%22{Uri.EscapeDataString(minecraftVersion)}%22%5D" };
        if (loaders is { Count: > 0 })
        {
            query.Add($"loaders=%5B%22{string.Join("%22,%22", loaders.Select(Uri.EscapeDataString))}%22%5D");
        }

        builder.Query = string.Join("&", query);
        return builder.ToString();
    }

    private static string FormatReleaseTime(string? rawValue)
    {
        return DateTimeOffset.TryParse(rawValue, out var value)
            ? value.LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            : "未记录发布时间";
    }

    private static string NormalizeVersionType(string rawValue)
    {
        return rawValue switch
        {
            "release" => "正式版",
            "beta" => "测试版",
            "alpha" => "预览版",
            _ => rawValue
        };
    }
}

internal enum FrontendInstallChoiceKind
{
    Minecraft,
    FabricLoader,
    LegacyFabricLoader,
    QuiltLoader,
    LabyMod,
    ModFile
}

internal sealed record FrontendInstallChoice(
    string Id,
    string Title,
    string Summary,
    string Version,
    FrontendInstallChoiceKind Kind,
    string? ManifestUrl = null,
    string? DownloadUrl = null,
    string? FileName = null);

internal sealed record FrontendInstallApplyRequest(
    string LauncherDirectory,
    string TargetInstanceName,
    FrontendInstallChoice MinecraftChoice,
    FrontendInstallChoice? PrimaryLoaderChoice,
    FrontendInstallChoice? FabricApiChoice,
    FrontendInstallChoice? LegacyFabricApiChoice,
    FrontendInstallChoice? QslChoice,
    bool UseInstanceIsolation,
    bool RunRepair,
    bool ForceCoreRefresh);

internal sealed record FrontendInstallApplyResult(
    string TargetDirectory,
    string ManifestPath,
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);

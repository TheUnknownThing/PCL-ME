using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static IReadOnlyList<FrontendInstallChoice> GetForgeChoices(string minecraftVersion, II18nService? i18n = null)
    {
        var root = ReadJsonArray($"https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node =>
            {
                var installerFile = node!["files"] is JsonArray files
                    ? files.Select(file => file as JsonObject)
                        .FirstOrDefault(file =>
                            string.Equals(file?["category"]?.GetValue<string>(), "installer", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(file?["format"]?.GetValue<string>(), "jar", StringComparison.OrdinalIgnoreCase))
                    : null;
                if (installerFile is null)
                {
                    return null;
                }

                var version = node["version"]?.GetValue<string>() ?? string.Empty;
                var branch = node["branch"]?.GetValue<string>();
                var fileVersion = version + (string.IsNullOrWhiteSpace(branch) ? string.Empty : "-" + branch);
                var fileName = $"{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}/forge-{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}-installer.jar";
                return new FrontendInstallChoice(
                    Id: $"forge:{minecraftVersion}:{version}",
                    Title: version,
                    Summary: Text(
                        i18n,
                        "download.install.choices.summaries.installer_with_time",
                        "Installer • {published_at}",
                        ("published_at", FormatReleaseTime(i18n, node["modified"]?.GetValue<string>()))),
                    Version: version,
                    Kind: FrontendInstallChoiceKind.Forge,
                    DownloadUrl: $"https://maven.minecraftforge.net/net/minecraftforge/forge/{fileName}",
                    FileName: Path.GetFileName(fileName),
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["fileVersion"] = fileVersion,
                        ["hash"] = installerFile["hash"]?.GetValue<string>(),
                        ["releaseTime"] = ParseCatalogReleaseTime(node["modified"]?.GetValue<string>())?.ToString("O")
                    });
            })
            .Where(choice => choice is not null)
            .Cast<FrontendInstallChoice>());
    }


    private static IReadOnlyList<FrontendInstallChoice> GetNeoForgeChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        var main = ReadJsonObject("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", downloadProvider);
        var legacy = ReadJsonObject("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", downloadProvider);
        var choices = new List<FrontendInstallChoice>();

        AddNeoForgeChoices(choices, main, FrontendInstallChoiceKind.NeoForge, minecraftVersion, i18n);
        AddNeoForgeChoices(choices, legacy, FrontendInstallChoiceKind.NeoForge, minecraftVersion, i18n);

        return SortInstallChoicesByVersionDescending(
            choices
            .GroupBy(choice => choice.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()));
    }


    private static void AddNeoForgeChoices(
        ICollection<FrontendInstallChoice> choices,
        JsonObject root,
        FrontendInstallChoiceKind kind,
        string minecraftVersion,
        II18nService? i18n = null)
    {
        if (root["versions"] is not JsonArray files)
        {
            return;
        }

        foreach (var file in files.Select(node => node as JsonValue))
        {
            var apiName = file?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(apiName))
            {
                continue;
            }

            var (inherit, versionName, packageName) = ParseNeoForgeApiName(apiName);
            if (!string.Equals(inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            choices.Add(new FrontendInstallChoice(
                Id: $"neoforge:{apiName}",
                Title: versionName,
                Summary: apiName.Contains("beta", StringComparison.OrdinalIgnoreCase) || apiName.Contains("alpha", StringComparison.OrdinalIgnoreCase)
                    ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                    : Text(i18n, "download.install.choices.summaries.stable", "Stable"),
                Version: versionName,
                Kind: kind,
                DownloadUrl: $"https://maven.neoforged.net/releases/net/neoforged/{packageName}/{apiName}/{packageName}-{apiName}-installer.jar",
                FileName: $"{packageName}-{apiName}-installer.jar",
                Metadata: new JsonObject
                {
                    ["apiName"] = apiName,
                    ["minecraftVersion"] = inherit
                }));
        }
    }


    private static (string MinecraftVersion, string VersionName, string PackageName) ParseNeoForgeApiName(string apiName)
    {
        if (apiName.Contains("1.20.1-", StringComparison.Ordinal))
        {
            return ("1.20.1", apiName.Replace("1.20.1-", string.Empty, StringComparison.Ordinal), "forge");
        }

        if (apiName.StartsWith("0.", StringComparison.Ordinal))
        {
            var parts = apiName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return (parts.Length > 1 ? parts[1] : apiName, apiName, "neoforge");
        }

        var versionCore = apiName.Split('-', 2)[0];
        var segments = versionCore.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return (apiName, apiName, "neoforge");
        }

        var major = int.TryParse(segments[0], out var parsedMajor) ? parsedMajor : 0;
        var minor = segments.Length > 1 && int.TryParse(segments[1], out var parsedMinor) ? parsedMinor : 0;
        var inherit = major >= 24
            ? versionCore.TrimEnd('0').TrimEnd('.')
            : "1." + major + (minor > 0 ? "." + minor : string.Empty);
        if (apiName.Contains('+', StringComparison.Ordinal))
        {
            inherit += "-" + apiName.Split('+', 2)[1];
        }

        return (inherit, apiName, "neoforge");
    }


    private static IReadOnlyList<FrontendInstallChoice> GetCleanroomChoices(string minecraftVersion, II18nService? i18n = null)
    {
        if (!string.Equals(minecraftVersion, "1.12.2", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var root = ReadJsonArray("https://api.github.com/repos/CleanroomMC/Cleanroom/releases");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["tag_name"]?.GetValue<string>()))
            .Select(node =>
            {
                var tag = node!["tag_name"]!.GetValue<string>();
                return new FrontendInstallChoice(
                    Id: $"cleanroom:{tag}",
                    Title: tag,
                    Summary: tag.Contains("alpha", StringComparison.OrdinalIgnoreCase)
                        ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                        : Text(i18n, "download.install.choices.summaries.stable", "Stable"),
                    Version: tag,
                    Kind: FrontendInstallChoiceKind.Cleanroom,
                    DownloadUrl: $"https://github.com/CleanroomMC/Cleanroom/releases/download/{tag}/cleanroom-{tag}-installer.jar",
                    FileName: $"cleanroom-{tag}-installer.jar",
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["releaseTime"] = ParseCatalogReleaseTime(node["published_at"]?.GetValue<string>())?.ToString("O")
                    });
            })
            .Cast<FrontendInstallChoice>());
    }


    private static IReadOnlyList<FrontendInstallChoice> GetFabricLoaderChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        return ReadLoaderChoices(
            $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.FabricLoader,
            "Fabric",
            downloadProvider,
            i18n);
    }


    private static IReadOnlyList<FrontendInstallChoice> GetLegacyFabricLoaderChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        return ReadLoaderChoices(
            $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.LegacyFabricLoader,
            "Legacy Fabric",
            downloadProvider,
            i18n);
    }


    private static IReadOnlyList<FrontendInstallChoice> GetQuiltLoaderChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        var root = ReadJsonArray($"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}", downloadProvider);
        return SortInstallChoicesByVersionDescending(
            root
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
                    Summary: loader["maven"]?.GetValue<string>() ?? Text(i18n, "download.install.choices.summaries.quilt_installer", "Quilt Installer"),
                    Version: version,
                    Kind: FrontendInstallChoiceKind.QuiltLoader,
                    ManifestUrl: profileUrl);
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version)));
    }


    private static IReadOnlyList<FrontendInstallChoice> GetLabyModChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        var production = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json", downloadProvider);
        var snapshot = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json", downloadProvider);
        var choices = new List<FrontendInstallChoice>();

        AddLabyChoice(choices, production, minecraftVersion, "production", Text(i18n, "download.install.choices.channels.stable", "Stable"), i18n);
        AddLabyChoice(choices, snapshot, minecraftVersion, "snapshot", Text(i18n, "download.install.choices.channels.snapshot", "Snapshot"), i18n);
        return choices;
    }


    private static void AddLabyChoice(
        ICollection<FrontendInstallChoice> choices,
        JsonObject manifest,
        string minecraftVersion,
        string channel,
        string channelLabel,
        II18nService? i18n = null)
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
            Summary: Text(
                i18n,
                "download.install.choices.summaries.labymod_for_minecraft",
                "LabyMod 4 • {minecraft_version}",
                ("minecraft_version", minecraftVersion)),
            Version: versionLabel,
            Kind: FrontendInstallChoiceKind.LabyMod,
            ManifestUrl: versionEntry["customManifestUrl"]?.GetValue<string>()));
    }


    private static IReadOnlyList<FrontendInstallChoice> GetOptiFineChoices(string minecraftVersion, II18nService? i18n = null)
    {
        var root = ReadJsonArray("https://bmclapi2.bangbang93.com/optifine/versionList");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => string.Equals(node?["mcversion"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var type = node!["type"]?.GetValue<string>() ?? "HD_U";
                var patch = node["patch"]?.GetValue<string>() ?? string.Empty;
                var fileName = node["filename"]?.GetValue<string>() ?? string.Empty;
                var displayName = (minecraftVersion + type.Replace("HD_U", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal) + " " + patch)
                    .Replace(".0 ", " ", StringComparison.Ordinal);
                var nameVersion = minecraftVersion + "-OptiFine_" + (type + " " + patch)
                    .Replace(".0 ", " ", StringComparison.Ordinal)
                    .Replace(" ", "_", StringComparison.Ordinal)
                    .Replace(minecraftVersion + "_", string.Empty, StringComparison.Ordinal);
                var libraryVersion = fileName
                    .Replace("OptiFine_", string.Empty, StringComparison.Ordinal)
                    .Replace(".jar", string.Empty, StringComparison.Ordinal)
                    .Replace("preview_", string.Empty, StringComparison.Ordinal);
                var bmclVersion = minecraftVersion is "1.8" or "1.9" ? minecraftVersion + ".0" : minecraftVersion;
                var shortName = displayName.Replace(minecraftVersion + " ", string.Empty, StringComparison.Ordinal);
                var downloadUrl = patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    ? "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U_" + shortName.Replace(" ", "/", StringComparison.Ordinal)
                    : "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U/" + shortName;
                var requiredForge = node["forge"]?.GetValue<string>()
                    ?.Replace("Forge ", string.Empty, StringComparison.Ordinal)
                    .Replace("#", string.Empty, StringComparison.Ordinal);
                if (requiredForge?.Contains("N/A", StringComparison.OrdinalIgnoreCase) == true)
                {
                    requiredForge = null;
                }

                return new FrontendInstallChoice(
                    Id: $"optifine:{nameVersion}",
                    Title: shortName,
                    Summary: patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                        ? Text(i18n, "download.install.choices.summaries.preview", "Preview")
                        : Text(i18n, "download.install.choices.summaries.release", "Release"),
                    Version: shortName,
                    Kind: FrontendInstallChoiceKind.OptiFine,
                    DownloadUrl: downloadUrl,
                    FileName: fileName,
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["nameVersion"] = nameVersion,
                        ["libraryVersion"] = libraryVersion,
                        ["requiredForgeVersion"] = requiredForge,
                        ["isPreview"] = patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    });
            })
            .Cast<FrontendInstallChoice>());
    }


    private static IReadOnlyList<FrontendInstallChoice> GetLiteLoaderChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        var root = ReadJsonObject("https://dl.liteloader.com/versions/versions.json", downloadProvider);
        if (root["versions"] is not JsonObject versions
            || !versions.TryGetPropertyValue(minecraftVersion, out var versionNode)
            || versionNode is not JsonObject versionObject)
        {
            return [];
        }

        var source = versionObject["artefacts"] ?? versionObject["snapshots"];
        var token = source?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
        if (token is null)
        {
            return [];
        }

        var releaseTime = token["timestamp"]?.GetValue<string>() ?? string.Empty;
        var formattedReleaseTime = long.TryParse(releaseTime, out var rawTimestamp)
            ? DateTimeOffset.FromUnixTimeSeconds(rawTimestamp).LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            : Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable");

        return
        [
            new FrontendInstallChoice(
                Id: $"liteloader:{minecraftVersion}:{token["version"]?.GetValue<string>()}",
                Title: token["version"]?.GetValue<string>() ?? minecraftVersion,
                Summary: Text(
                    i18n,
                    "download.install.choices.summaries.status_with_time",
                    "{status} • {published_at}",
                    (
                        "status",
                        string.Equals(token["stream"]?.GetValue<string>(), "SNAPSHOT", StringComparison.OrdinalIgnoreCase)
                            ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                            : Text(i18n, "download.install.choices.summaries.stable", "Stable")),
                    ("published_at", formattedReleaseTime)),
                Version: token["version"]?.GetValue<string>() ?? minecraftVersion,
                Kind: FrontendInstallChoiceKind.LiteLoader,
                Metadata: new JsonObject
                {
                    ["minecraftVersion"] = minecraftVersion,
                    ["releaseTime"] = DateTimeOffset.FromUnixTimeSeconds(rawTimestamp).ToString("O"),
                    ["token"] = token.DeepClone()
                })
        ];
    }


    private static IReadOnlyList<FrontendInstallChoice> GetOptiFabricChoices(
        string minecraftVersion,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        if (minecraftVersion.StartsWith("1.14", StringComparison.OrdinalIgnoreCase)
            || minecraftVersion.StartsWith("1.15", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var root = ReadJsonObject("https://api.cfwidget.com/minecraft/mc-mods/optifabric", downloadProvider);
        if (root["files"] is not JsonArray files)
        {
            return [];
        }

        return files
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Where(node => CfWidgetFileMatchesVersion(node!, minecraftVersion))
            .OrderByDescending(node => node!["uploaded_at"]?.GetValue<string>() ?? string.Empty)
            .Select(node =>
            {
                var fileId = node!["id"]?.GetValue<int>() ?? 0;
                var fileName = node["name"]?.GetValue<string>() ?? string.Empty;
                var displayName = node["display"]?.GetValue<string>() ?? fileName;
                var type = node["type"]?.GetValue<string>() ?? string.Empty;
                var uploadedAt = node["uploaded_at"]?.GetValue<string>() ?? string.Empty;

                return new FrontendInstallChoice(
                    Id: $"optifabric:{fileId}",
                    Title: displayName.Replace("OptiFabric-v", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(),
                    Summary: Text(
                        i18n,
                        "download.install.choices.summaries.status_with_time",
                        "{status} • {published_at}",
                        ("status", NormalizeVersionType(i18n, type)),
                        ("published_at", FormatReleaseTime(i18n, uploadedAt))),
                    Version: displayName,
                    Kind: FrontendInstallChoiceKind.ModFile,
                    DownloadUrl: BuildCurseForgeMediaUrl(fileId, fileName),
                    FileName: fileName,
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["fileId"] = fileId
                    });
            })
            .ToArray();
    }


    private static IReadOnlyList<FrontendInstallChoice> GetModrinthFileChoices(
        string projectId,
        string minecraftVersion,
        IReadOnlyList<string>? loaders,
        bool allowVersionFallback = false,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        foreach (var candidateVersion in GetVersionCandidates(minecraftVersion, allowVersionFallback))
        {
            var url = BuildModrinthVersionUrl(projectId, candidateVersion, loaders);
            JsonArray root;
            try
            {
                root = ReadJsonArray(url, downloadProvider);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            var choices = root
                .Select(node => node as JsonObject)
                .Where(node => node is not null)
                .Select(node => ToModrinthChoice(node, i18n))
                .Where(choice => choice is not null)
                .Cast<FrontendInstallChoice>();
            var orderedChoices = SortInstallChoicesDescending(choices);
            if (orderedChoices.Count > 0)
            {
                return orderedChoices;
            }
        }

        return [];
    }


    private static FrontendInstallChoice? ToModrinthChoice(JsonObject? version, II18nService? i18n = null)
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
            Summary: Text(
                i18n,
                "download.install.choices.summaries.status_with_time",
                "{status} • {published_at}",
                ("status", NormalizeVersionType(i18n, versionType)),
                ("published_at", FormatReleaseTime(i18n, published))),
            Version: title,
            Kind: FrontendInstallChoiceKind.ModFile,
            DownloadUrl: primaryFile["url"]?.GetValue<string>(),
            FileName: primaryFile["filename"]?.GetValue<string>(),
            Metadata: new JsonObject
            {
                ["releaseTime"] = ParseCatalogReleaseTime(published)?.ToString("O")
            });
    }


    private static IReadOnlyList<FrontendInstallChoice> ReadLoaderChoices(
        string url,
        FrontendInstallChoiceKind kind,
        string prefix,
        FrontendDownloadProvider? downloadProvider = null,
        II18nService? i18n = null)
    {
        var root = ReadJsonArray(url, downloadProvider);
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => node?["loader"] is JsonObject)
            .Select(node =>
            {
                var loader = (JsonObject)node!["loader"]!;
                var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                return new FrontendInstallChoice(
                    Id: $"{kind}:{version}",
                    Title: version,
                    Summary: loader["stable"]?.GetValue<bool>() == true
                        ? Text(i18n, "download.install.choices.summaries.stable", "Stable")
                        : Text(
                            i18n,
                            "download.install.choices.summaries.loader_testing",
                            "{loader_name} testing build",
                            ("loader_name", prefix)),
                    Version: version,
                    Kind: kind,
                    ManifestUrl: $"{url.TrimEnd('/')}/{version}/profile/json");
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version)));
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


    private static string BuildCurseForgeMediaUrl(int fileId, string fileName)
    {
        return $"https://mediafiles.forgecdn.net/files/{fileId / 1000}/{fileId % 1000:D3}/{Uri.EscapeDataString(fileName)}";
    }


    private static bool CfWidgetFileMatchesVersion(JsonObject file, string minecraftVersion)
    {
        if (file["versions"] is not JsonArray versions)
        {
            return false;
        }

        var tokens = versions
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens.Contains("Fabric") && tokens.Contains(minecraftVersion);
    }


    private static DateTimeOffset? ParseCatalogReleaseTime(string? rawValue)
    {
        return DateTimeOffset.TryParse(rawValue, out var value) ? value : null;
    }


    private static string NormalizeMinecraftCatalogVersionId(string rawId)
    {
        return rawId switch
        {
            "2point0_blue" => "2.0_blue",
            "2point0_red" => "2.0_red",
            "2point0_purple" => "2.0_purple",
            "20w14infinite" => "20w14∞",
            _ => rawId
        };
    }


    private static string ResolveMinecraftCatalogGroup(string versionId, string? type, string? releaseTime)
    {
        var normalizedId = versionId.ToLowerInvariant();
        switch (type?.ToLowerInvariant())
        {
            case "release":
                return "release";
            case "snapshot":
            case "pending":
                if (IsAprilFoolsVersion(normalizedId, releaseTime))
                {
                    return "april_fools";
                }

                return LooksLikeMisclassifiedRelease(normalizedId) ? "release" : "preview";
            case "special":
                return "april_fools";
            default:
                return IsAprilFoolsVersion(normalizedId, releaseTime) ? "april_fools" : "legacy";
        }
    }


    private static bool LooksLikeMisclassifiedRelease(string normalizedId)
    {
        return normalizedId.StartsWith("1.", StringComparison.Ordinal)
               && !normalizedId.Contains("combat", StringComparison.Ordinal)
               && !normalizedId.Contains("rc", StringComparison.Ordinal)
               && !normalizedId.Contains("experimental", StringComparison.Ordinal)
               && !normalizedId.Equals("1.2", StringComparison.Ordinal)
               && !normalizedId.Contains("pre", StringComparison.Ordinal);
    }


    private static bool IsAprilFoolsVersion(string normalizedId, string? releaseTime)
    {
        if (ResolveMinecraftCatalogAprilFoolsLore(null, normalizedId).Length > 0)
        {
            return true;
        }

        var parsed = ParseCatalogReleaseTime(releaseTime);
        return parsed is not null
               && parsed.Value.ToUniversalTime().AddHours(2) is var adjusted
               && adjusted.Month == 4
               && adjusted.Day == 1;
    }


    private static string ResolveMinecraftCatalogIconName(string group)
    {
        return group switch
        {
            "preview" => "CommandBlock.png",
            "april_fools" => "GoldBlock.png",
            "legacy" => "CobbleStone.png",
            _ => "Grass.png"
        };
    }


    private static string ResolveMinecraftCatalogLore(II18nService? i18n, string versionId, string group, string? releaseTime)
    {
        var lore = ResolveMinecraftCatalogAprilFoolsLore(i18n, versionId.ToLowerInvariant());
        if (lore.Length > 0)
        {
            return lore;
        }

        if (!string.Equals(group, "april_fools", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var parsed = ParseCatalogReleaseTime(releaseTime);
        return parsed is null
            ? string.Empty
            : Text(
                i18n,
                "download.install.choices.summaries.april_fools_special",
                "April Fools special released on {published_at}",
                ("published_at", parsed.Value.LocalDateTime.ToString("yyyy/MM/dd HH:mm")));
    }


    private static string ResolveMinecraftCatalogAprilFoolsLore(II18nService? i18n, string normalizedId)
    {
        return normalizedId switch
        {
            var value when value.StartsWith("2.0", StringComparison.Ordinal) || value.StartsWith("2point0", StringComparison.Ordinal) =>
                value.EndsWith("red", StringComparison.Ordinal)
                    ? Text(i18n, "download.install.choices.summaries.april_fools.2013_red", "2013 | This secret update, planned for two years, took the game to a new level! (Red version)")
                    : value.EndsWith("blue", StringComparison.Ordinal)
                        ? Text(i18n, "download.install.choices.summaries.april_fools.2013_blue", "2013 | This secret update, planned for two years, took the game to a new level! (Blue version)")
                        : value.EndsWith("purple", StringComparison.Ordinal)
                            ? Text(i18n, "download.install.choices.summaries.april_fools.2013_purple", "2013 | This secret update, planned for two years, took the game to a new level! (Purple version)")
                            : Text(i18n, "download.install.choices.summaries.april_fools.2013", "2013 | This secret update, planned for two years, took the game to a new level!"),
            "15w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2015", "2015 | As a game for all ages, we need peace, love, and hugs."),
            "1.rv-pre1" => Text(i18n, "download.install.choices.summaries.april_fools.2016", "2016 | It's time to bring modern technology into Minecraft!"),
            "3d shareware v1.34" => Text(i18n, "download.install.choices.summaries.april_fools.2019", "2019 | We found this masterpiece from 1994 in the ruins of a basement!"),
            var value when value.StartsWith("20w14inf", StringComparison.Ordinal) || value == "20w14∞" => Text(i18n, "download.install.choices.summaries.april_fools.2020", "2020 | We added 2 billion new dimensions and turned infinite imagination into reality!"),
            "22w13oneblockatatime" => Text(i18n, "download.install.choices.summaries.april_fools.2022", "2022 | One block at a time! Meet new digging, crafting, and riding gameplay."),
            "23w13a_or_b" => Text(i18n, "download.install.choices.summaries.april_fools.2023", "2023 | Research shows players like making choices, and the more the better!"),
            "24w14potato" => Text(i18n, "download.install.choices.summaries.april_fools.2024", "2024 | Poisonous potatoes have always been ignored and underestimated, so we supercharged them!"),
            "25w14craftmine" => Text(i18n, "download.install.choices.summaries.april_fools.2025", "2025 | You can craft anything, including your world itself!"),
            "26w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2026", "2026 | Why do you need an inventory? Let the blocks follow you instead!"),
            _ => string.Empty
        };
    }


    private static string FormatMinecraftCatalogVersion(string versionId)
    {
        return versionId.Replace('_', ' ');
    }


    private static string BuildMinecraftCatalogTimestampSummary(II18nService? i18n, string? releaseTime, string normalizedId, string formattedTitle)
    {
        var published = FormatReleaseTime(i18n, releaseTime);
        return formattedTitle == normalizedId
            ? published
            : $"{published} | {normalizedId}";
    }


    private static string BuildMinecraftCatalogLoreSummary(string lore, string normalizedId, string formattedTitle)
    {
        return formattedTitle == normalizedId
            ? lore
            : $"{lore} | {normalizedId}";
    }


    private static string FormatReleaseTime(II18nService? i18n, string? rawValue)
    {
        return DateTimeOffset.TryParse(rawValue, out var value)
            ? value.LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            : Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable");
    }


    private static string NormalizeVersionType(II18nService? i18n, string rawValue)
    {
        return rawValue switch
        {
            "release" => Text(i18n, "download.install.choices.summaries.release", "Release"),
            "beta" => Text(i18n, "download.install.choices.summaries.testing", "Testing"),
            "alpha" => Text(i18n, "download.install.choices.summaries.preview", "Preview"),
            _ => rawValue
        };
    }

}

using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    internal static FrontendModpackPackage InspectPackage(
        string archivePath,
        int communitySourcePreference,
        HttpClient httpClient,
        CancellationToken cancelToken)
    {
        return InspectPackage(archivePath, communitySourcePreference, httpClient, cancelToken, i18n: null);
    }

    private static FrontendModpackPackage InspectPackage(
        string archivePath,
        int communitySourcePreference,
        HttpClient httpClient,
        CancellationToken cancelToken,
        II18nService? i18n)
    {
        using var archive = OpenArchiveRead(archivePath);
        var (kind, baseFolder) = DetectPackageKind(archive);
        return kind switch
        {
            FrontendModpackPackageKind.Modrinth => BuildModrinthPackage(archive, baseFolder, i18n),
            FrontendModpackPackageKind.CurseForge => BuildCurseForgePackage(archive, baseFolder, communitySourcePreference, httpClient, cancelToken, i18n),
            FrontendModpackPackageKind.Mcbbs => BuildMcbbsPackage(archive, baseFolder, i18n),
            FrontendModpackPackageKind.Mmc => BuildMmcPackage(archive, baseFolder, i18n),
            _ => throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.unsupported_package_kind"))
        };
    }

    private static (FrontendModpackPackageKind Kind, string BaseFolder) DetectPackageKind(ZipArchive archive)
    {
        if (archive.GetEntry("mmc-pack.json") is not null)
        {
            return (FrontendModpackPackageKind.Mmc, string.Empty);
        }

        if (archive.GetEntry("mcbbs.packmeta") is not null)
        {
            return (FrontendModpackPackageKind.Mcbbs, string.Empty);
        }

        if (archive.GetEntry("modrinth.index.json") is not null)
        {
            return (FrontendModpackPackageKind.Modrinth, string.Empty);
        }

        if (archive.GetEntry("manifest.json") is not null)
        {
            var rootManifest = ReadJsonObjectFromEntry(archive, "manifest.json");
            return rootManifest["addons"] is null
                ? (FrontendModpackPackageKind.CurseForge, string.Empty)
                : (FrontendModpackPackageKind.Mcbbs, string.Empty);
        }

        foreach (var entry in archive.Entries)
        {
            var parts = entry.FullName.Split('/', StringSplitOptions.None);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var baseFolder = parts[0] + "/";
            if (string.Equals(parts[1], "mmc-pack.json", StringComparison.OrdinalIgnoreCase))
            {
                return (FrontendModpackPackageKind.Mmc, baseFolder);
            }

            if (string.Equals(parts[1], "mcbbs.packmeta", StringComparison.OrdinalIgnoreCase))
            {
                return (FrontendModpackPackageKind.Mcbbs, baseFolder);
            }

            if (string.Equals(parts[1], "modrinth.index.json", StringComparison.OrdinalIgnoreCase))
            {
                return (FrontendModpackPackageKind.Modrinth, baseFolder);
            }

            if (!string.Equals(parts[1], "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifest = ReadJsonObjectFromEntry(archive, entry.FullName);
            return manifest["addons"] is null
                ? (FrontendModpackPackageKind.CurseForge, baseFolder)
                : (FrontendModpackPackageKind.Mcbbs, baseFolder);
        }

        return (FrontendModpackPackageKind.Unknown, string.Empty);
    }

    private static FrontendModpackPackage BuildModrinthPackage(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, baseFolder + "modrinth.index.json", i18n);
        if (root["dependencies"] is not JsonObject dependencies
            || string.IsNullOrWhiteSpace(dependencies["minecraft"]?.GetValue<string>()))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.modrinth_missing_minecraft_version"));
        }

        var files = new List<FrontendModpackFilePlan>();
        foreach (var node in root["files"] as JsonArray ?? [])
        {
            if (node is not JsonObject file)
            {
                continue;
            }

            var env = file["env"] as JsonObject;
            var clientSupport = env?["client"]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(clientSupport, "unsupported", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clientSupport, "optional", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = file["path"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var targetPath = BuildValidatedTargetPath(relativePath);
            var urls = (file["downloads"] as JsonArray ?? [])
                .Select(node => node?.GetValue<string>())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .ToArray();
            if (urls.Length == 0)
            {
                continue;
            }

            files.Add(new FrontendModpackFilePlan(
                targetPath,
                urls,
                file["fileSize"]?.GetValue<long?>(),
                file["hashes"]?["sha1"]?.GetValue<string>(),
                Path.GetFileName(targetPath)));
        }

        return new FrontendModpackPackage(
            FrontendModpackPackageKind.Modrinth,
            dependencies["minecraft"]?.GetValue<string>() ?? string.Empty,
            dependencies["forge"]?.GetValue<string>(),
            dependencies["neoforge"]?.GetValue<string>() ?? dependencies["neo-forge"]?.GetValue<string>(),
            null,
            dependencies["fabric-loader"]?.GetValue<string>(),
            null,
            dependencies["quilt-loader"]?.GetValue<string>(),
            null,
            null,
            null,
            root["versionId"]?.GetValue<string>(),
            null,
            null,
            [
                new FrontendModpackOverrideSource(baseFolder + "overrides"),
                new FrontendModpackOverrideSource(baseFolder + "client-overrides")
            ],
            files);
    }

    private static FrontendModpackPackage BuildMcbbsPackage(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, ResolveMcbbsMetadataEntryPath(archive, baseFolder, i18n), i18n);
        if (root["addons"] is not JsonArray addons)
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_addons"));
        }

        var addonVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in addons)
        {
            if (entry is not JsonObject addon)
            {
                continue;
            }

            var id = addon["id"]?.GetValue<string>()?.Trim();
            var version = addon["version"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            addonVersions[id] = version;
        }

        if (!addonVersions.TryGetValue("game", out var minecraftVersion) || string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_game_version"));
        }

        var launchInfo = root["launchInfo"] as JsonObject;
        return new FrontendModpackPackage(
            FrontendModpackPackageKind.Mcbbs,
            minecraftVersion,
            addonVersions.TryGetValue("forge", out var forgeVersion) ? forgeVersion : null,
            addonVersions.TryGetValue("neoforge", out var neoForgeVersion) ? neoForgeVersion : null,
            addonVersions.TryGetValue("cleanroom", out var cleanroomVersion) ? cleanroomVersion : null,
            addonVersions.TryGetValue("fabric", out var fabricVersion) ? fabricVersion : null,
            addonVersions.TryGetValue("legacyfabric", out var legacyFabricVersion) ? legacyFabricVersion : null,
            addonVersions.TryGetValue("quilt", out var quiltVersion) ? quiltVersion : null,
            addonVersions.TryGetValue("liteloader", out var liteLoaderVersion) ? liteLoaderVersion : null,
            addonVersions.TryGetValue("optifine", out var optiFineVersion) ? optiFineVersion : null,
            addonVersions.TryGetValue("labymod", out var labyModVersion) ? labyModVersion : null,
            root["version"]?.GetValue<string>(),
            ReadJoinedText(launchInfo?["javaArgument"]),
            ReadJoinedText(launchInfo?["launchArgument"]),
            [new FrontendModpackOverrideSource(baseFolder + "overrides")],
            []);
    }

    private static FrontendModpackPackage BuildMmcPackage(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, baseFolder + "mmc-pack.json", i18n);
        if (root["components"] is not JsonArray components)
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_game_version"));
        }

        var componentVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in components)
        {
            if (node is not JsonObject component)
            {
                continue;
            }

            var uid = component["uid"]?.GetValue<string>()?.Trim();
            var version = component["version"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            componentVersions[uid] = version;
        }

        if (!componentVersions.TryGetValue("net.minecraft", out var minecraftVersion) || string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_game_version"));
        }

        var instanceConfig = ParseMmcInstanceConfig(ReadEntryTextOrEmpty(archive, baseFolder + "instance.cfg"));
        var extraConfigValues = BuildMmcInstanceConfigValues(instanceConfig);
        var forgeVersion = componentVersions.TryGetValue("net.minecraftforge", out var forgeLikeVersion)
            ? forgeLikeVersion
            : null;
        var cleanroomVersion = !string.IsNullOrWhiteSpace(forgeVersion) && forgeVersion.StartsWith("0.", StringComparison.Ordinal)
            ? forgeVersion
            : null;
        if (!string.IsNullOrWhiteSpace(cleanroomVersion))
        {
            forgeVersion = null;
        }

        return new FrontendModpackPackage(
            FrontendModpackPackageKind.Mmc,
            minecraftVersion,
            forgeVersion,
            componentVersions.TryGetValue("net.neoforged", out var neoForgeVersion) ? neoForgeVersion : null,
            cleanroomVersion,
            componentVersions.TryGetValue("net.fabricmc.fabric-loader", out var fabricVersion) ? fabricVersion : null,
            null,
            componentVersions.TryGetValue("org.quiltmc.quilt-loader", out var quiltVersion) ? quiltVersion : null,
            componentVersions.TryGetValue("com.mumfrey.liteloader", out var liteLoaderVersion) ? liteLoaderVersion : null,
            null,
            null,
            root["formatVersion"]?.ToString(),
            extraConfigValues.TryGetValue("VersionAdvanceJvm", out var jvmArgs) ? Convert.ToString(jvmArgs, CultureInfo.InvariantCulture) : null,
            null,
            [
                new FrontendModpackOverrideSource(baseFolder + ".minecraft"),
                new FrontendModpackOverrideSource(baseFolder + "libraries", FrontendModpackOverrideTarget.LauncherRoot, "libraries")
            ],
            [],
            BuildMmcManifestPatch(archive, baseFolder, root),
            extraConfigValues);
    }

    private static string ResolveMcbbsMetadataEntryPath(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var packMetaPath = baseFolder + "mcbbs.packmeta";
        if (archive.GetEntry(packMetaPath) is not null)
        {
            return packMetaPath;
        }

        var manifestPath = baseFolder + "manifest.json";
        if (archive.GetEntry(manifestPath) is not null)
        {
            return manifestPath;
        }

        throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.missing_critical_file", ("path", packMetaPath)));
    }

    private static JsonObject ReadJsonObjectFromEntry(ZipArchive archive, string entryPath, II18nService? i18n = null)
    {
        using var stream = archive.GetEntry(entryPath)?.Open()
                           ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.missing_critical_file", ("path", entryPath)));
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var content = reader.ReadToEnd();
        return JsonNode.Parse(content)?.AsObject()
               ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.json_parse_failed", ("entry_path", entryPath)));
    }

    private static string ReadEntryTextOrEmpty(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry is null)
        {
            return string.Empty;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> ParseMmcInstanceConfig(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.StartsWith('#') || rawLine.StartsWith(';'))
            {
                continue;
            }

            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim().Replace("\\\"", "\"", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> BuildMmcInstanceConfigValues(IReadOnlyDictionary<string, string> config)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (ReadMmcBool(config, "OverrideCommands") &&
            config.TryGetValue("PreLaunchCommand", out var preLaunchCommand) &&
            !string.IsNullOrWhiteSpace(preLaunchCommand))
        {
            values["VersionAdvanceRun"] = NormalizeMmcCommand(preLaunchCommand);
        }

        if (ReadMmcBool(config, "JoinServerOnLaunch") &&
            config.TryGetValue("JoinServerOnLaunchAddress", out var serverAddress) &&
            !string.IsNullOrWhiteSpace(serverAddress))
        {
            values["VersionServerEnter"] = serverAddress;
        }

        if (ReadMmcBool(config, "IgnoreJavaCompatibility"))
        {
            values["VersionAdvanceJava"] = true;
        }

        if (config.TryGetValue("JvmArgs", out var jvmArgs) && !string.IsNullOrWhiteSpace(jvmArgs))
        {
            values["VersionAdvanceJvm"] = jvmArgs;
        }

        return values;
    }

    private static bool ReadMmcBool(IReadOnlyDictionary<string, string> config, string key)
    {
        return config.TryGetValue(key, out var value) &&
               (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.Ordinal));
    }

    private static string NormalizeMmcCommand(string command)
    {
        return command
            .Replace("$INST_JAVA", "{java}java", StringComparison.Ordinal)
            .Replace("$INST_MC_DIR\\", "{minecraft}", StringComparison.Ordinal)
            .Replace("$INST_MC_DIR/", "{minecraft}", StringComparison.Ordinal)
            .Replace("$INST_MC_DIR", "{minecraft}", StringComparison.Ordinal)
            .Replace("$INST_DIR\\", "{verpath}", StringComparison.Ordinal)
            .Replace("$INST_DIR/", "{verpath}", StringComparison.Ordinal)
            .Replace("$INST_DIR", "{verpath}", StringComparison.Ordinal)
            .Replace("$INST_ID", "{name}", StringComparison.Ordinal)
            .Replace("$INST_NAME", "{name}", StringComparison.Ordinal);
    }

    private static string? ReadJoinedText(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return null;
        }

        var values = array
            .Select(entry => entry?.GetValue<string>()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        return values.Length == 0 ? null : string.Join(" ", values);
    }
}

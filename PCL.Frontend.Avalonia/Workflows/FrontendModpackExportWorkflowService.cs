using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendModpackExportWorkflowService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    public static void CreateArchive(FrontendModpackExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LauncherDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Version);

        var profile = FrontendVersionManifestInspector.ReadProfile(request.LauncherDirectory, request.InstanceName);
        var archiveEntries = ExpandArchiveEntries(request.PackageKind, request.Sources);

        var archiveDirectory = Path.GetDirectoryName(request.ArchivePath);
        if (!string.IsNullOrWhiteSpace(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
        }

        using var archive = ZipFile.Open(request.ArchivePath, ZipArchiveMode.Create);
        switch (request.PackageKind)
        {
            case FrontendModpackExportPackageKind.Mcbbs:
                WriteTextEntry(archive, "mcbbs.packmeta", BuildMcbbsManifest(request, profile, archiveEntries).ToJsonString(JsonWriteOptions));
                WriteTextEntry(archive, "manifest.json", BuildCurseManifest(request, profile).ToJsonString(JsonWriteOptions));
                break;
            case FrontendModpackExportPackageKind.Modrinth:
                WriteTextEntry(archive, "modrinth.index.json", BuildModrinthManifest(request, profile).ToJsonString(JsonWriteOptions));
                break;
            default:
                throw new InvalidOperationException($"不支持的整合包导出格式：{request.PackageKind}");
        }

        foreach (var entry in archiveEntries)
        {
            archive.CreateEntryFromFile(entry.SourcePath, entry.ArchivePath);
        }
    }

    private static IReadOnlyList<FrontendModpackArchiveEntry> ExpandArchiveEntries(
        FrontendModpackExportPackageKind packageKind,
        IReadOnlyList<FrontendModpackExportSource> sources)
    {
        var results = new List<FrontendModpackArchiveEntry>();
        var seenArchivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.SourcePath) || string.IsNullOrWhiteSpace(source.ArchivePath))
            {
                continue;
            }

            if (File.Exists(source.SourcePath))
            {
                AddArchiveEntry(packageKind, source.SourcePath, source.ArchivePath, results, seenArchivePaths);
                continue;
            }

            if (!Directory.Exists(source.SourcePath))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(source.SourcePath, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(source.SourcePath, file);
                AddArchiveEntry(packageKind, file, Path.Combine(source.ArchivePath, relativePath), results, seenArchivePaths);
            }
        }

        return results;
    }

    private static void AddArchiveEntry(
        FrontendModpackExportPackageKind packageKind,
        string sourcePath,
        string archivePath,
        ICollection<FrontendModpackArchiveEntry> results,
        ISet<string> seenArchivePaths)
    {
        var normalizedArchivePath = NormalizeArchivePath(MapArchivePath(packageKind, archivePath));
        if (!seenArchivePaths.Add(normalizedArchivePath))
        {
            return;
        }

        results.Add(new FrontendModpackArchiveEntry(sourcePath, normalizedArchivePath));
    }

    private static string MapArchivePath(FrontendModpackExportPackageKind packageKind, string archivePath)
    {
        var normalizedArchivePath = NormalizeArchivePath(archivePath);
        if (packageKind != FrontendModpackExportPackageKind.Modrinth)
        {
            return normalizedArchivePath;
        }

        const string overridesPrefix = "overrides/";
        return normalizedArchivePath.StartsWith(overridesPrefix, StringComparison.OrdinalIgnoreCase)
            ? "client-overrides/" + normalizedArchivePath[overridesPrefix.Length..]
            : "client-overrides/" + normalizedArchivePath.TrimStart('/');
    }

    private static JsonObject BuildMcbbsManifest(
        FrontendModpackExportRequest request,
        FrontendVersionManifestProfile profile,
        IReadOnlyList<FrontendModpackArchiveEntry> archiveEntries)
    {
        var files = new JsonArray();
        foreach (var entry in archiveEntries
                     .Where(candidate => candidate.ArchivePath.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase)))
        {
            files.Add(new JsonObject
            {
                ["type"] = "addon",
                ["force"] = true,
                ["path"] = entry.ArchivePath["overrides/".Length..],
                ["hash"] = ComputeSha1(entry.SourcePath)
            });
        }

        return new JsonObject
        {
            ["manifestType"] = "minecraftModpack",
            ["manifestVersion"] = 2,
            ["name"] = request.Name,
            ["version"] = request.Version,
            ["author"] = string.Empty,
            ["description"] = string.Empty,
            ["origin"] = new JsonArray(),
            ["addons"] = BuildMcbbsAddonArray(profile),
            ["libraries"] = new JsonArray(),
            ["files"] = files,
            ["settings"] = new JsonObject
            {
                ["install_mods"] = true,
                ["install_resourcepack"] = true
            },
            ["launchInfo"] = new JsonObject
            {
                ["minMemory"] = 0,
                ["supportJava"] = new JsonArray(),
                ["launchArgument"] = ToJsonArray(TokenizeArguments(request.LaunchGameArguments)),
                ["javaArgument"] = ToJsonArray(TokenizeArguments(request.LaunchJvmArguments))
            }
        };
    }

    private static JsonArray BuildMcbbsAddonArray(FrontendVersionManifestProfile profile)
    {
        var addons = new JsonArray();
        AddAddon(addons, "game", profile.VanillaVersion);
        AddAddon(addons, "forge", profile.ForgeVersion);
        AddAddon(addons, "neoforge", profile.NeoForgeVersion);
        AddAddon(addons, "cleanroom", profile.CleanroomVersion);
        AddAddon(addons, "fabric", profile.FabricVersion);
        AddAddon(addons, "legacyfabric", profile.LegacyFabricVersion);
        AddAddon(addons, "quilt", profile.QuiltVersion);
        AddAddon(addons, "liteloader", profile.LiteLoaderVersion);
        AddAddon(addons, "optifine", profile.OptiFineVersion);
        AddAddon(addons, "labymod", profile.LabyModVersion);
        return addons;
    }

    private static void AddAddon(JsonArray addons, string id, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        addons.Add(new JsonObject
        {
            ["id"] = id,
            ["version"] = version
        });
    }

    private static JsonObject BuildCurseManifest(FrontendModpackExportRequest request, FrontendVersionManifestProfile profile)
    {
        var modLoaders = new JsonArray();
        AddCurseModLoader(modLoaders, "forge", profile.ForgeVersion);
        AddCurseModLoader(modLoaders, "neoforge", profile.NeoForgeVersion);
        AddCurseModLoader(modLoaders, "fabric", profile.FabricVersion);

        return new JsonObject
        {
            ["manifestType"] = "minecraftModpack",
            ["manifestVersion"] = 1,
            ["name"] = request.Name,
            ["version"] = request.Version,
            ["author"] = string.Empty,
            ["overrides"] = "overrides",
            ["minecraft"] = new JsonObject
            {
                ["version"] = profile.VanillaVersion,
                ["modLoaders"] = modLoaders
            },
            ["files"] = new JsonArray()
        };
    }

    private static void AddCurseModLoader(JsonArray modLoaders, string id, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        modLoaders.Add(new JsonObject
        {
            ["id"] = $"{id}-{version}",
            ["primary"] = true
        });
    }

    private static JsonObject BuildModrinthManifest(FrontendModpackExportRequest request, FrontendVersionManifestProfile profile)
    {
        var dependencies = new JsonObject
        {
            ["minecraft"] = profile.VanillaVersion
        };

        AddDependency(dependencies, "forge", profile.ForgeVersion);
        AddDependency(dependencies, "neoforge", profile.NeoForgeVersion);
        AddDependency(dependencies, "fabric-loader", profile.FabricVersion);
        AddDependency(dependencies, "quilt-loader", profile.QuiltVersion);

        return new JsonObject
        {
            ["game"] = "minecraft",
            ["formatVersion"] = 1,
            ["versionId"] = request.Version,
            ["name"] = request.Name,
            ["summary"] = string.Empty,
            ["files"] = new JsonArray(),
            ["dependencies"] = dependencies
        };
    }

    private static void AddDependency(JsonObject dependencies, string key, string? version)
    {
        if (!string.IsNullOrWhiteSpace(version))
        {
            dependencies[key] = version;
        }
    }

    private static JsonArray ToJsonArray(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    internal static IReadOnlyList<string> TokenizeArguments(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var parts = new List<string>();
        var current = new StringBuilder(value.Length);
        var hasValue = false;

        for (var index = 0; index < value.Length;)
        {
            var currentChar = value[index];
            if (currentChar == '\'')
            {
                hasValue = true;
                var end = value.IndexOf(currentChar, index + 1);
                if (end < 0)
                {
                    end = value.Length;
                }

                current.Append(value, index + 1, end - index - 1);
                index = end + 1;
                continue;
            }

            if (currentChar == '"')
            {
                hasValue = true;
                index += 1;
                while (index < value.Length)
                {
                    currentChar = value[index++];
                    if (currentChar == '"')
                    {
                        break;
                    }

                    if (currentChar == '`' && index < value.Length)
                    {
                        current.Append(MapQuotedEscape(value[index++]));
                        continue;
                    }

                    current.Append(currentChar);
                }

                continue;
            }

            if (char.IsWhiteSpace(currentChar))
            {
                if (hasValue)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    hasValue = false;
                }

                index += 1;
                continue;
            }

            hasValue = true;
            current.Append(currentChar);
            index += 1;
        }

        if (hasValue)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    private static char MapQuotedEscape(char value)
    {
        return value switch
        {
            'a' => '\u0007',
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'v' => '\u000b',
            _ => value
        };
    }

    private static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }

    private static void WriteTextEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Utf8NoBom);
        writer.Write(content);
    }

    private static string NormalizeArchivePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

internal sealed record FrontendModpackExportRequest(
    string ArchivePath,
    FrontendModpackExportPackageKind PackageKind,
    string LauncherDirectory,
    string InstanceName,
    string Name,
    string Version,
    IReadOnlyList<FrontendModpackExportSource> Sources,
    string? LaunchJvmArguments = null,
    string? LaunchGameArguments = null);

internal sealed record FrontendModpackExportSource(
    string SourcePath,
    string ArchivePath);

internal enum FrontendModpackExportPackageKind
{
    Mcbbs = 0,
    Modrinth = 1
}

internal sealed record FrontendModpackArchiveEntry(
    string SourcePath,
    string ArchivePath);

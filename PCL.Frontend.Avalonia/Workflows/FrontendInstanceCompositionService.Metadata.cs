using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstanceCompositionService
{
    private static RecognizedModMetadata? TryReadLocalModMetadata(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return TryReadFabricModMetadata(archive)
                ?? TryReadQuiltModMetadata(archive)
                ?? TryReadForgeModMetadata(archive, neoforge: true)
                ?? TryReadForgeModMetadata(archive, neoforge: false)
                ?? TryReadForgeLegacyMetadata(archive)
                ?? TryReadLiteLoaderMetadata(archive);
        }
        catch
        {
            return null;
        }
    }

    private static RecognizedModMetadata? TryReadFabricModMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "fabric.mod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        if (root is null)
        {
            return null;
        }

        var id = GetString(root, "id");
        var contact = root["contact"] as JsonObject;
        return BuildRecognizedModMetadata(
            Identity: id,
            Title: GetString(root, "name") ?? id,
            Description: GetString(root, "description"),
            Authors: JoinAuthorArray(root["authors"] as JsonArray),
            Version: GetString(root, "version"),
            Website: GetString(contact, "homepage"),
            Loader: "Fabric",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadIconReference(root["icon"])));
    }

    private static RecognizedModMetadata? TryReadQuiltModMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "quilt.mod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        var loader = root?["quilt_loader"] as JsonObject;
        var metadata = loader?["metadata"] as JsonObject;
        var contributors = metadata?["contributors"] as JsonObject;
        var contact = metadata?["contact"] as JsonObject;
        if (loader is null)
        {
            return null;
        }

        return BuildRecognizedModMetadata(
            Identity: GetString(loader, "id"),
            Title: GetString(metadata, "name") ?? GetString(loader, "id"),
            Description: GetString(metadata, "description"),
            Authors: JoinContributors(contributors),
            Version: GetString(loader, "version"),
            Website: GetString(contact, "homepage"),
            Loader: "Quilt",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadIconReference(metadata?["icon"])));
    }

    private static RecognizedModMetadata? TryReadForgeLegacyMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "mcmod.info");
        if (entry is null)
        {
            return null;
        }

        JsonObject? metadata = null;
        try
        {
            var node = JsonNode.Parse(ReadArchiveEntryText(entry));
            metadata = node switch
            {
                JsonArray array when array.Count > 0 => array[0] as JsonObject,
                JsonObject obj when obj["modList"] is JsonArray list && list.Count > 0 => list[0] as JsonObject,
                JsonObject obj => obj,
                _ => null
            };
        }
        catch
        {
            metadata = null;
        }

        if (metadata is null)
        {
            return null;
        }

        var authors = FirstNonEmpty(
            GetString(metadata, "author"),
            JoinJsonArray(metadata["authors"] as JsonArray),
            JoinJsonArray(metadata["authorList"] as JsonArray),
            GetString(metadata, "credits"));

        return BuildRecognizedModMetadata(
            Identity: GetString(metadata, "modid"),
            Title: GetString(metadata, "name") ?? GetString(metadata, "modid"),
            Description: GetString(metadata, "description"),
            Authors: authors,
            Version: GetString(metadata, "version"),
            Website: FirstNonEmpty(GetString(metadata, "url"), GetString(metadata, "updateUrl")),
            Loader: "Forge",
            IconBytes: TryReadEmbeddedIconBytes(archive, GetString(metadata, "logoFile")));
    }

    private static RecognizedModMetadata? TryReadForgeModMetadata(ZipArchive archive, bool neoforge)
    {
        var entry = FindArchiveEntry(archive, neoforge ? "META-INF/neoforge.mods.toml" : "META-INF/mods.toml");
        if (entry is null)
        {
            return null;
        }

        var content = ReadArchiveEntryText(entry);
        var modsBlock = ReadFirstModsTomlBlock(content);
        if (string.IsNullOrWhiteSpace(modsBlock))
        {
            return null;
        }

        return BuildRecognizedModMetadata(
            Identity: ReadTomlValue(modsBlock, "modId"),
            Title: FirstNonEmpty(ReadTomlValue(modsBlock, "displayName"), ReadTomlValue(modsBlock, "modId")),
            Description: ReadTomlValue(modsBlock, "description"),
            Authors: ReadTomlArrayOrString(content, "authors") ?? ReadTomlArrayOrString(modsBlock, "authors"),
            Version: ReadTomlValue(modsBlock, "version"),
            Website: ReadTomlValue(modsBlock, "displayURL"),
            Loader: neoforge ? "NeoForge" : "Forge",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadTomlValue(content, "logoFile")));
    }

    private static RecognizedModMetadata? TryReadLiteLoaderMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "litemod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        if (root is null)
        {
            return null;
        }

        var name = GetString(root, "name");
        return BuildRecognizedModMetadata(
            Identity: name,
            Title: name,
            Description: GetString(root, "description"),
            Authors: GetString(root, "author"),
            Version: GetString(root, "version"),
            Website: FirstNonEmpty(GetString(root, "updateURI"), GetString(root, "checkUpdateUrl")),
            Loader: "LiteLoader",
            IconBytes: null);
    }

    private static RecognizedModMetadata? BuildRecognizedModMetadata(
        string? Identity,
        string? Title,
        string? Description,
        string? Authors,
        string? Version,
        string? Website,
        string? Loader,
        byte[]? IconBytes)
    {
        if (string.IsNullOrWhiteSpace(Title)
            && string.IsNullOrWhiteSpace(Description)
            && string.IsNullOrWhiteSpace(Version)
            && string.IsNullOrWhiteSpace(Loader))
        {
            return null;
        }

        return new RecognizedModMetadata(
            Identity: Identity?.Trim() ?? string.Empty,
            Title: Title?.Trim() ?? string.Empty,
            Description: Description?.Trim() ?? string.Empty,
            Authors: Authors?.Trim() ?? string.Empty,
            Version: Version?.Trim() ?? string.Empty,
            Website: Website?.Trim() ?? string.Empty,
            Loader: Loader?.Trim() ?? string.Empty,
            IconBytes: IconBytes);
    }

    private static string BuildModSummary(FileInfo file, RecognizedModMetadata? metadata, II18nService? i18n)
    {
        var segments = new List<string>();
        AddIfNotEmpty(segments, metadata?.Authors);
        AddIfNotEmpty(segments, GetWebsiteLabel(metadata?.Website));

        if (segments.Count == 0)
        {
            segments.Add(Text(i18n, "instance.content.resource.summary.archive", "{modified_at} • {file_size}", ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))));
        }

        return string.Join(" • ", segments);
    }

    private static string BuildModMeta(FileInfo file, RecognizedModMetadata? metadata, II18nService? i18n)
    {
        var segments = new List<string>();
        AddIfNotEmpty(segments, metadata?.Loader);
        AddIfNotEmpty(segments, metadata?.Version);

        if (segments.Count == 0)
        {
            var extension = GetModContainerExtension(file.Name);
            AddIfNotEmpty(segments, string.IsNullOrWhiteSpace(extension) ? Text(i18n, "instance.content.resource.kind.mod", "Mod") : extension.ToUpperInvariant());
        }

        return string.Join(" • ", segments);
    }

    private static string DetermineModIconName(string? loader, string fallback)
    {
        return loader switch
        {
            "Fabric" => "Fabric.png",
            "Quilt" => "Quilt.png",
            "NeoForge" => "NeoForge.png",
            "Forge" => "Anvil.png",
            _ => fallback
        };
    }

    private static string GetFallbackModTitle(string fileName)
    {
        var normalizedName = RemoveTrailingSuffix(fileName, ".disabled");
        normalizedName = RemoveTrailingSuffix(normalizedName, ".old");
        return Path.GetFileNameWithoutExtension(normalizedName);
    }

    private static string GetModContainerExtension(string fileName)
    {
        var normalizedName = RemoveTrailingSuffix(fileName, ".disabled");
        normalizedName = RemoveTrailingSuffix(normalizedName, ".old");
        return Path.GetExtension(normalizedName).TrimStart('.');
    }

    private static string RemoveTrailingSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string GetWebsiteLabel(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return string.Empty;
        }

        return Uri.TryCreate(website, UriKind.Absolute, out var uri)
            ? uri.Host
            : website;
    }

    private static string NormalizeInlineText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static JsonObject? ReadJsonObject(ZipArchiveEntry entry)
    {
        try
        {
            return JsonNode.Parse(ReadArchiveEntryText(entry)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadArchiveEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? GetString(JsonObject? root, string key)
    {
        return root?[key]?.GetValue<string>();
    }

    private static string JoinAuthorArray(JsonArray? authors)
    {
        if (authors is null || authors.Count == 0)
        {
            return string.Empty;
        }

        var values = authors
            .Select(node => node switch
            {
                JsonValue value => value.TryGetValue<string>(out var text) ? text : string.Empty,
                JsonObject obj => GetString(obj, "name") ?? string.Empty,
                _ => string.Empty
            })
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(", ", values);
    }

    private static string JoinJsonArray(JsonArray? values)
    {
        if (values is null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", values
            .Select(node => node?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string JoinContributors(JsonObject? contributors)
    {
        if (contributors is null || contributors.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", contributors
            .Select(pair => string.IsNullOrWhiteSpace(pair.Value?.ToString())
                ? pair.Key
                : $"{pair.Key} ({pair.Value})"));
    }

    private static string? ReadIconReference(JsonNode? node)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var icon) => icon,
            JsonObject objectValue => objectValue
                .OrderByDescending(pair => int.TryParse(pair.Key, out var size) ? size : 0)
                .Select(pair => pair.Value?.ToString())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    private static byte[]? TryReadEmbeddedIconBytes(ZipArchive archive, string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        var iconEntry = FindArchiveEntry(archive, entryPath);
        if (iconEntry is null)
        {
            return null;
        }

        if (iconEntry.Length <= 0 || iconEntry.Length > MaxEmbeddedIconBytes)
        {
            return null;
        }

        try
        {
            using var stream = iconEntry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static ZipArchiveEntry? FindArchiveEntry(ZipArchive archive, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var direct = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct;
        }

        var fileName = Path.GetFileName(normalized);
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(Path.GetFileName(entry.FullName), fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadFirstModsTomlBlock(string content)
    {
        const string sectionHeader = "[[mods]]";
        var sectionStart = content.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
        if (sectionStart < 0)
        {
            return string.Empty;
        }

        var nextSectionMatch = Regex.Match(content[(sectionStart + sectionHeader.Length)..], @"(?m)^\s*\[");
        var sectionEnd = nextSectionMatch.Success
            ? sectionStart + sectionHeader.Length + nextSectionMatch.Index
            : content.Length;
        return content[sectionStart..sectionEnd];
    }

    private static string? ReadTomlValue(string content, string key)
    {
        var raw = ReadTomlRawValue(content, key);
        return string.IsNullOrWhiteSpace(raw) ? null : ParseTomlScalar(raw);
    }

    private static string? ReadTomlArrayOrString(string content, string key)
    {
        var raw = ReadTomlRawValue(content, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        if (raw[0] != '[')
        {
            return ParseTomlScalar(raw);
        }

        var values = new List<string>();
        foreach (Match match in TomlQuotedValueRegex.Matches(raw))
        {
            values.Add(UnescapeTomlString(match.Groups["value"].Value));
        }

        foreach (Match match in TomlSingleQuotedValueRegex.Matches(raw))
        {
            values.Add(match.Groups["value"].Value);
        }

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string? ReadTomlRawValue(string content, string key)
    {
        var matcher = Regex.Match(content, $@"(?m)^\s*{Regex.Escape(key)}\s*=");
        if (!matcher.Success)
        {
            return null;
        }

        var start = matcher.Index + matcher.Length;
        while (start < content.Length && (content[start] == ' ' || content[start] == '\t'))
        {
            start++;
        }

        if (start >= content.Length)
        {
            return null;
        }

        if (content.AsSpan(start).StartsWith("\"\"\"".AsSpan(), StringComparison.Ordinal))
        {
            var endIndex = content.IndexOf("\"\"\"", start + 3, StringComparison.Ordinal);
            return endIndex < 0 ? content[start..] : content[start..(endIndex + 3)];
        }

        if (content.AsSpan(start).StartsWith("'''".AsSpan(), StringComparison.Ordinal))
        {
            var endIndex = content.IndexOf("'''", start + 3, StringComparison.Ordinal);
            return endIndex < 0 ? content[start..] : content[start..(endIndex + 3)];
        }

        var lineEnd = content.IndexOfAny(['\r', '\n'], start);
        return lineEnd < 0 ? content[start..] : content[start..lineEnd];
    }

    private static string? ParseTomlScalar(string raw)
    {
        raw = raw.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.StartsWith("\"\"\"", StringComparison.Ordinal))
        {
            var endIndex = raw.LastIndexOf("\"\"\"", StringComparison.Ordinal);
            if (endIndex > 2)
            {
                return raw[3..endIndex];
            }
        }

        if (raw.StartsWith("'''", StringComparison.Ordinal))
        {
            var endIndex = raw.LastIndexOf("'''", StringComparison.Ordinal);
            if (endIndex > 2)
            {
                return raw[3..endIndex];
            }
        }

        var quoted = TomlQuotedValueRegex.Match(raw);
        if (quoted.Success)
        {
            return UnescapeTomlString(quoted.Groups["value"].Value);
        }

        var singleQuoted = TomlSingleQuotedValueRegex.Match(raw);
        if (singleQuoted.Success)
        {
            return singleQuoted.Groups["value"].Value;
        }

        var commentIndex = raw.IndexOf('#');
        if (commentIndex >= 0)
        {
            raw = raw[..commentIndex];
        }

        return raw.Trim().TrimEnd(',');
    }

    private static string UnescapeTomlString(string value)
    {
        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static FrontendVersionManifestSummary ReadManifestSummary(string launcherFolder, string selectedInstanceName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var profile = FrontendVersionManifestInspector.ReadProfile(launcherFolder, selectedInstanceName);
        return new FrontendVersionManifestSummary(
            VanillaVersion: profile.VanillaVersion,
            VersionType: profile.VersionType,
            HasForge: profile.HasForge,
            ForgeVersion: profile.ForgeVersion,
            NeoForgeVersion: profile.NeoForgeVersion,
            CleanroomVersion: profile.CleanroomVersion,
            FabricVersion: profile.FabricVersion,
            LegacyFabricVersion: profile.LegacyFabricVersion,
            QuiltVersion: profile.QuiltVersion,
            OptiFineVersion: profile.OptiFineVersion,
            HasLiteLoader: profile.HasLiteLoader,
            LiteLoaderVersion: profile.LiteLoaderVersion,
            LabyModVersion: profile.LabyModVersion,
            HasLabyMod: profile.HasLabyMod,
            HasFabricApi: profile.HasFabricApi,
            FabricApiVersion: profile.FabricApiVersion,
            HasQsl: profile.HasQsl,
            QslVersion: profile.QslVersion,
            HasOptiFabric: profile.HasOptiFabric,
            OptiFabricVersion: profile.OptiFabricVersion,
            LibraryNames: profile.LibraryNames);
    }

    private static bool ResolveIsolationEnabled(
        YamlFileProvider localConfig,
        YamlFileProvider instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return ReadValue(instanceConfig, "VersionArgumentIndieV2", false);
        }

        var globalMode = ReadValue(localConfig, "LaunchArgumentIndieV2", 4);
        return FrontendIsolationPolicyService.ShouldIsolateByGlobalMode(
            globalMode,
            IsModable(manifestSummary),
            FrontendIsolationPolicyService.IsNonReleaseVersionType(manifestSummary.VersionType));
    }

    private static int ResolveInstanceIsolationIndex(YamlFileProvider instanceConfig)
    {
        if (!instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return 0;
        }

        return ReadValue(instanceConfig, "VersionArgumentIndieV2", false)
            ? 1
            : 2;
    }

}

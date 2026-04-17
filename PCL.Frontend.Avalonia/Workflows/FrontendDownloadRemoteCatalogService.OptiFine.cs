using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendDownloadRemoteCatalogService
{
    private static FrontendDownloadCatalogState BuildOptiFineCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadOptiFine, i18n);
        var payload = FetchOptiFineEntries(versionSourceIndex, i18n);
        var groupedSections = BuildGroupedInstallerSections(
            OrderOptiFineEntries(payload.Value),
            GetOptiFineSectionKey,
            group => group.Key == SnapshotSectionKey ? Text(i18n, "download.catalog.remote.groups.snapshot", "Snapshots") : group.Key,
            entry => CreateOptiFineCatalogEntry(entry, i18n),
            i18n);

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            groupedSections.Count > 0
                ? groupedSections
                : [new FrontendDownloadCatalogSection("remote_catalog", EnsureEntries([], Text(i18n, "download.catalog.remote.empty.optifine", "There are no available OptiFine remote entries right now."), i18n))]);
    }
    private static RemotePayload<List<OptiFineCatalogEntry>> FetchOptiFineEntries(int versionSourceIndex, II18nService? i18n = null)
    {
        var payload = FetchString(CreateOptiFineSources(versionSourceIndex), versionSourceIndex);
        return payload.Source.IsOfficial
            ? new RemotePayload<List<OptiFineCatalogEntry>>(payload.Source, ParseOptiFineOfficialEntries(payload.Value, i18n))
            : new RemotePayload<List<OptiFineCatalogEntry>>(payload.Source, ParseOptiFineMirrorEntries(JsonNode.Parse(payload.Value)?.AsArray()
                                                                                                      ?? throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.optifine_mirror_parse_failed", "Unable to parse the OptiFine mirror catalog.")), i18n));
    }

    private static List<OptiFineCatalogEntry> ParseOptiFineOfficialEntries(string html, II18nService? i18n = null)
    {
        var forgeMatches = Regex.Matches(html, "(?<=colForge'>)[^<]*");
        var dateMatches = Regex.Matches(html, "(?<=colDate'>)[^<]+");
        var nameMatches = Regex.Matches(html, "(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")");
        if (nameMatches.Count == 0 || nameMatches.Count != dateMatches.Count || nameMatches.Count != forgeMatches.Count)
        {
            throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.optifine_format_invalid", "The OptiFine official catalog format is not valid."));
        }

        var entries = new List<OptiFineCatalogEntry>();
        for (var index = 0; index < nameMatches.Count; index++)
        {
            var rawName = nameMatches[index].Value.Replace('_', ' ');
            var displayName = rawName.Replace("HD U ", string.Empty, StringComparison.Ordinal).Replace(".0 ", " ", StringComparison.Ordinal);
            var minecraftVersion = rawName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
            var requiredForge = NormalizeOptiFineRequiredForgeVersion(forgeMatches[index].Value);
            var isPreview = rawName.Contains("pre", StringComparison.OrdinalIgnoreCase);
            entries.Add(new OptiFineCatalogEntry(
                minecraftVersion,
                displayName,
                isPreview,
                FormatDdMmYyyy(i18n, dateMatches[index].Value),
                requiredForge,
                BuildOptiFineDownloadUrl(minecraftVersion, displayName, isPreview),
                CreateOptiFineSuggestedFileName(displayName, isPreview)));
        }

        return entries;
    }

    private static List<OptiFineCatalogEntry> ParseOptiFineMirrorEntries(JsonArray root, II18nService? i18n = null)
    {
        return root
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node =>
            {
                var minecraftVersion = node!["mcversion"]?.GetValue<string>() ?? Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
                var patch = node["patch"]?.GetValue<string>() ?? string.Empty;
                var type = node["type"]?.GetValue<string>() ?? "HD_U";
                var rawDisplayName = (minecraftVersion + " " + type.Replace("HD_U", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal) + " " + patch).Trim();
                var displayName = rawDisplayName.Replace(".0 ", " ", StringComparison.Ordinal).Trim();
                var isPreview = patch.Contains("pre", StringComparison.OrdinalIgnoreCase);
                return new OptiFineCatalogEntry(
                    minecraftVersion,
                    displayName,
                    isPreview,
                    string.Empty,
                    NormalizeOptiFineRequiredForgeVersion(node["forge"]?.GetValue<string>()),
                    BuildOptiFineDownloadUrl(minecraftVersion, displayName, isPreview),
                    node["filename"]?.GetValue<string>() ?? CreateOptiFineSuggestedFileName(displayName, isPreview));
            })
            .ToList();
    }
    private static FrontendDownloadCatalogEntry CreateOptiFineCatalogEntry(OptiFineCatalogEntry entry, II18nService? i18n = null)
    {
        return CreateInstallerDownloadEntry(
            entry.DisplayName,
            BuildOptiFineInfo(entry, i18n),
            entry.TargetUrl,
            entry.SuggestedFileName);
    }

    private static IEnumerable<OptiFineCatalogEntry> OrderOptiFineEntries(IEnumerable<OptiFineCatalogEntry> entries)
    {
        return entries
            .OrderBy(entry => GetOptiFineSectionKey(entry) == SnapshotSectionKey ? 0 : 1)
            .ThenByDescending(entry => GetOptiFineGroupVersion(entry) ?? string.Empty, VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.DisplayName, VersionTextComparer.Instance);
    }

    private static string GetOptiFineSectionKey(OptiFineCatalogEntry entry)
    {
        return GetOptiFineGroupVersion(entry) ?? SnapshotSectionKey;
    }

    private static string? GetOptiFineGroupVersion(OptiFineCatalogEntry entry)
    {
        var segments = entry.MinecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return entry.MinecraftVersion.StartsWith("1.", StringComparison.OrdinalIgnoreCase) && segments.Length >= 2
            ? $"1.{segments[1]}"
            : null;
    }

    private static string BuildOptiFineInfo(OptiFineCatalogEntry entry, II18nService? i18n = null)
    {
        var parts = new List<string>
        {
            entry.IsPreview
                ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                : Text(i18n, "download.install.choices.summaries.release", "Release")
        };
        if (!string.IsNullOrWhiteSpace(entry.ReleaseTime))
        {
            parts.Add(Text(i18n, "download.catalog.remote.labels.published_at", "Published at {published_at}", ("published_at", entry.ReleaseTime)));
        }

        if (entry.RequiredForgeVersion is null)
        {
            parts.Add(Text(i18n, "download.catalog.remote.labels.forge_incompatible", "Not compatible with Forge"));
        }
        else if (!string.IsNullOrWhiteSpace(entry.RequiredForgeVersion))
        {
            parts.Add(Text(i18n, "download.catalog.remote.labels.forge_compatible", "Compatible with Forge {forge_version}", ("forge_version", entry.RequiredForgeVersion)));
        }

        return string.Join(Text(i18n, "download.catalog.remote.labels.separator", ", "), parts);
    }

    private static string? NormalizeOptiFineRequiredForgeVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var value = rawValue
            .Replace("Forge ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("#", string.Empty, StringComparison.Ordinal)
            .Trim();
        return value.Contains("N/A", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static string BuildOptiFineDownloadUrl(string minecraftVersion, string displayName, bool isPreview)
    {
        var suffix = displayName.StartsWith(minecraftVersion + " ", StringComparison.OrdinalIgnoreCase)
            ? displayName[(minecraftVersion.Length + 1)..]
            : displayName;
        var normalizedMinecraftVersion = minecraftVersion is "1.8" or "1.9" ? minecraftVersion + ".0" : minecraftVersion;
        if (isPreview)
        {
            var previewSegments = suffix
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Uri.EscapeDataString);
            return $"https://bmclapi2.bangbang93.com/optifine/{normalizedMinecraftVersion}/HD_U_{string.Join("/", previewSegments)}";
        }

        return $"https://bmclapi2.bangbang93.com/optifine/{normalizedMinecraftVersion}/HD_U/{Uri.EscapeDataString(suffix)}";
    }

    private static string CreateOptiFineSuggestedFileName(string displayName, bool isPreview)
    {
        return (isPreview ? "preview_" : string.Empty) + "OptiFine_" + displayName.Replace(" ", "_", StringComparison.Ordinal) + ".jar";
    }
}

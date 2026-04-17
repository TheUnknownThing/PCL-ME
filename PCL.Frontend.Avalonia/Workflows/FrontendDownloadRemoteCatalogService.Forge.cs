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
    private static FrontendDownloadCatalogState BuildForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadForge, i18n);
        var sources = CreateForgeListSources(versionSourceIndex);
        var payload = FetchString(sources, versionSourceIndex);
        var minecraftVersions = payload.Source.IsOfficial
            ? ParseForgeMinecraftVersionsFromHtml(payload.Value)
            : ParseForgeMinecraftVersionsFromPlainText(payload.Value);

        if (minecraftVersions.Count == 0)
        {
            throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.forge_versions_missing", "The Forge remote catalog did not return any Minecraft versions."));
        }

        var sections = minecraftVersions
            .OrderByDescending(version => version, VersionTextComparer.Instance)
            .Select(version => new FrontendDownloadCatalogSection(
                version.Replace("_p", " P", StringComparison.Ordinal),
                [],
                IsCollapsible: true,
                IsInitiallyExpanded: false,
                LazyLoadToken: version))
            .ToArray();

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections.Length > 0
                ? sections
                : [new FrontendDownloadCatalogSection("remote_catalog", EnsureEntries([], Text(i18n, "download.catalog.remote.empty.forge_versions", "The Forge remote catalog did not return any Minecraft versions."), i18n))]);
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> LoadForgeCatalogSectionEntries(
        int versionSourceIndex,
        string minecraftVersion,
        II18nService? i18n = null)
    {
        var payload = FetchForgeVersionEntries(versionSourceIndex, minecraftVersion, i18n);
        var orderedEntries = payload.Value
            .OrderByDescending(entry => entry.VersionName, VersionTextComparer.Instance)
            .ToArray();
        if (orderedEntries.Length == 0)
        {
            return EnsureEntries([], Text(i18n, "download.catalog.remote.empty.forge_version_entries", "There are no available Forge entries for {minecraft_version}.", ("minecraft_version", minecraftVersion)), i18n);
        }

        var latestEntry = orderedEntries[0];
        return orderedEntries
            .Select(entry => CreateForgeCatalogEntry(entry, ReferenceEquals(entry, latestEntry), i18n))
            .ToArray();
    }
    private static RemotePayload<List<ForgeVersionCatalogEntry>> FetchForgeVersionEntries(
        int versionSourceIndex,
        string minecraftVersion,
        II18nService? i18n = null)
    {
        var payload = FetchString(CreateForgeVersionSources(versionSourceIndex, minecraftVersion), versionSourceIndex);
        var entries = payload.Source.IsOfficial
            ? ParseForgeOfficialVersionEntries(payload.Value, minecraftVersion, payload.Source.IsOfficial)
            : ParseForgeMirrorVersionEntries(JsonNode.Parse(payload.Value)?.AsArray()
                                             ?? throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.forge_mirror_parse_failed", "Unable to parse the Forge mirror build catalog.")), minecraftVersion, payload.Source.IsOfficial);
        return new RemotePayload<List<ForgeVersionCatalogEntry>>(payload.Source, entries);
    }

    private static List<ForgeVersionCatalogEntry> ParseForgeOfficialVersionEntries(string html, string minecraftVersion, bool isOfficial)
    {
        var blocks = html.Split("<td class=\"download-version", StringSplitOptions.RemoveEmptyEntries);
        var entries = new List<ForgeVersionCatalogEntry>();
        foreach (var block in blocks.Skip(1))
        {
            var versionName = Regex.Match(block, "(?<=[^(0-9)]+)[0-9.]+").Value;
            if (string.IsNullOrWhiteSpace(versionName))
            {
                continue;
            }

            var branch = Regex.Match(
                    block,
                    $@"(?<=-{Regex.Escape(versionName)}-)[^-""]+(?=-[a-z]+\.[a-z]{{3}})")
                .Value;
            var normalizedBranch = NormalizeForgeBranch(versionName, branch, minecraftVersion);
            var category = ResolveForgeFileCategory(block);
            if (category is null)
            {
                continue;
            }

            var fileVersion = BuildForgeFileVersion(versionName, normalizedBranch);
            var fileExtension = category == "installer" ? "jar" : "zip";
            var targetUrl = BuildForgeInstallerDownloadUrl(isOfficial, minecraftVersion, fileVersion, category, fileExtension);
            entries.Add(new ForgeVersionCatalogEntry(
                minecraftVersion,
                versionName,
                fileVersion,
                category,
                fileExtension,
                block.Contains("promo-recommended", StringComparison.OrdinalIgnoreCase),
                string.IsNullOrWhiteSpace(Regex.Match(block, "(?<=download-time\" title=\")[^\"]+").Value)
                    ? string.Empty
                    : FormatReleaseTime(null, Regex.Match(block, "(?<=download-time\" title=\")[^\"]+").Value),
                targetUrl,
                Path.GetFileName(targetUrl)));
        }

        return entries
            .OrderByDescending(entry => entry.VersionName, VersionTextComparer.Instance)
            .ToList();
    }

    private static List<ForgeVersionCatalogEntry> ParseForgeMirrorVersionEntries(JsonArray root, string minecraftVersion, bool isOfficial)
    {
        var entries = new List<ForgeVersionCatalogEntry>();
        foreach (var node in root.Select(item => item as JsonObject).Where(item => item is not null))
        {
            var versionName = node!["version"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(versionName))
            {
                continue;
            }

            var selectedFile = SelectForgeMirrorFile(node["files"] as JsonArray);
            if (selectedFile is null)
            {
                continue;
            }

            var normalizedBranch = NormalizeForgeBranch(versionName, node["branch"]?.GetValue<string>(), minecraftVersion);
            var fileVersion = BuildForgeFileVersion(versionName, normalizedBranch);
            var category = selectedFile["category"]?.GetValue<string>() ?? "installer";
            var fileExtension = selectedFile["format"]?.GetValue<string>() ?? (category == "installer" ? "jar" : "zip");
            var targetUrl = BuildForgeInstallerDownloadUrl(isOfficial, minecraftVersion, fileVersion, category, fileExtension);
            entries.Add(new ForgeVersionCatalogEntry(
                minecraftVersion,
                versionName,
                fileVersion,
                category,
                fileExtension,
                false,
                FormatReleaseTime(null, node["modified"]?.GetValue<string>()),
                targetUrl,
                Path.GetFileName(targetUrl)));
        }

        return entries
            .OrderByDescending(entry => entry.VersionName, VersionTextComparer.Instance)
            .ToList();
    }
    private static FrontendDownloadCatalogEntry CreateForgeCatalogEntry(ForgeVersionCatalogEntry entry, bool isLatest, II18nService? i18n = null)
    {
        var infoParts = new List<string>();
        if (entry.IsRecommended)
        {
            infoParts.Add(Text(i18n, "download.catalog.remote.labels.recommended", "Recommended"));
        }
        else if (isLatest)
        {
            infoParts.Add(Text(i18n, "download.catalog.remote.labels.latest", "Latest"));
        }

        if (!string.IsNullOrWhiteSpace(entry.ReleaseTime))
        {
            infoParts.Add(Text(i18n, "download.catalog.remote.labels.published_at", "Published at {published_at}", ("published_at", entry.ReleaseTime)));
        }

        return CreateInstallerDownloadEntry(
            entry.VersionName,
            string.Join(Text(i18n, "download.catalog.remote.labels.separator", ", "), infoParts),
            entry.TargetUrl,
            entry.SuggestedFileName);
    }
    private static string? ResolveForgeFileCategory(string block)
    {
        if (block.Contains("classifier-installer\"", StringComparison.OrdinalIgnoreCase))
        {
            return "installer";
        }

        if (block.Contains("classifier-universal\"", StringComparison.OrdinalIgnoreCase))
        {
            return "universal";
        }

        return block.Contains("client.zip", StringComparison.OrdinalIgnoreCase) ? "client" : null;
    }

    private static JsonObject? SelectForgeMirrorFile(JsonArray? files)
    {
        if (files is null)
        {
            return null;
        }

        JsonObject? selected = null;
        var bestPriority = -1;
        foreach (var file in files.Select(node => node as JsonObject).Where(node => node is not null))
        {
            var category = file!["category"]?.GetValue<string>();
            var format = file["format"]?.GetValue<string>();
            var priority = category switch
            {
                "installer" when format == "jar" => 2,
                "universal" when format == "zip" => 1,
                "client" when format == "zip" => 0,
                _ => -1
            };
            if (priority > bestPriority)
            {
                selected = file;
                bestPriority = priority;
            }
        }

        return selected;
    }

    private static string? NormalizeForgeBranch(string versionName, string? branch, string minecraftVersion)
    {
        if (versionName is "11.15.1.2318" or "11.15.1.1902" or "11.15.1.1890")
        {
            return "1.8.9";
        }

        if (string.IsNullOrWhiteSpace(branch)
            && string.Equals(minecraftVersion, "1.7.10", StringComparison.OrdinalIgnoreCase)
            && TryReadForgeBuild(versionName, out var build)
            && build >= 1300)
        {
            return "1.7.10";
        }

        return string.IsNullOrWhiteSpace(branch) ? null : branch.Trim();
    }

    private static bool TryReadForgeBuild(string versionName, out int build)
    {
        build = 0;
        var parts = versionName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 && int.TryParse(parts[3], out build);
    }

    private static string BuildForgeFileVersion(string versionName, string? branch)
    {
        return string.IsNullOrWhiteSpace(branch) ? versionName : $"{versionName}-{branch}";
    }

    private static string BuildForgeInstallerDownloadUrl(
        bool isOfficial,
        string minecraftVersion,
        string fileVersion,
        string category,
        string fileExtension)
    {
        var normalizedMinecraftVersion = minecraftVersion.Replace("-", "_", StringComparison.Ordinal);
        var fileName = $"forge-{normalizedMinecraftVersion}-{fileVersion}-{category}.{fileExtension}";
        return isOfficial
            ? $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{normalizedMinecraftVersion}-{fileVersion}/{fileName}"
            : $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{normalizedMinecraftVersion}-{fileVersion}/{fileName}";
    }
    private static IReadOnlyList<string> ParseForgeMinecraftVersionsFromHtml(string html)
    {
        var matches = Regex.Matches(html, "(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)");
        return matches.Select(match => match.Value).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> ParseForgeMinecraftVersionsFromPlainText(string text)
    {
        return Regex.Matches(text, "[0-9.]+(_pre[0-9]?)?")
            .Select(match => match.Value)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

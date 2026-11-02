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
    private static FrontendDownloadCatalogState BuildLiteLoaderCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadLiteLoader, i18n);
        var payload = FetchJsonObject(CreateLiteLoaderSources(versionSourceIndex), versionSourceIndex);
        var versions = payload.Value["versions"] as JsonObject
                       ?? throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.liteloader_versions_missing", "LiteLoader catalog is missing the versions field."));
        var entries = new List<LiteLoaderCatalogEntry>();
        foreach (var property in versions)
        {
            if (property.Value is not JsonObject versionObject)
            {
                continue;
            }

            if (property.Key.StartsWith("1.6", StringComparison.OrdinalIgnoreCase)
                || property.Key.StartsWith("1.5", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var source = versionObject["artefacts"] as JsonObject ?? versionObject["snapshots"] as JsonObject;
            var latest = source?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
            if (latest is null)
            {
                continue;
            }

            var isPreview = string.Equals(latest["stream"]?.GetValue<string>(), "SNAPSHOT", StringComparison.OrdinalIgnoreCase);
            var timestamp = ReadInt64(latest["timestamp"]);
            entries.Add(new LiteLoaderCatalogEntry(
                property.Key,
                CreateLiteLoaderSuggestedFileName(property.Key),
                isPreview,
                IsLiteLoaderLegacy(property.Key),
                timestamp,
                BuildLiteLoaderDownloadUrl(property.Key, CreateLiteLoaderSuggestedFileName(property.Key), IsLiteLoaderLegacy(property.Key))));
        }

        var sections = BuildGroupedInstallerSections(
            OrderLiteLoaderEntries(entries),
            GetLiteLoaderSectionKey,
            group => group.Key,
            entry => CreateInstallerDownloadEntry(
                entry.MinecraftVersion,
                BuildLiteLoaderInfo(entry, i18n),
                entry.TargetUrl,
                entry.FileName),
            i18n);

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("remote_catalog", EnsureEntries([], Text(i18n, "download.catalog.remote.empty.liteloader", "There are no available LiteLoader remote entries right now."), i18n))]);
    }
    private static IEnumerable<LiteLoaderCatalogEntry> OrderLiteLoaderEntries(IEnumerable<LiteLoaderCatalogEntry> entries)
    {
        return entries
            .OrderByDescending(entry => GetLiteLoaderSectionKey(entry), VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.MinecraftVersion, VersionTextComparer.Instance);
    }
    private static string GetLiteLoaderSectionKey(LiteLoaderCatalogEntry entry)
    {
        var segments = entry.MinecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return entry.MinecraftVersion.StartsWith("1.", StringComparison.OrdinalIgnoreCase) && segments.Length >= 2
            ? $"1.{segments[1]}"
            : "unknown_version";
    }

    private static string BuildLiteLoaderInfo(LiteLoaderCatalogEntry entry, II18nService? i18n = null)
    {
        var info = entry.IsPreview
            ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
            : Text(i18n, "download.install.choices.summaries.stable", "Stable");
        if (entry.Timestamp > 0)
        {
            info += Text(i18n, "download.catalog.remote.labels.separator", ", ")
                + Text(i18n, "download.catalog.remote.labels.published_at", "Published at {published_at}", ("published_at", FormatUnixTime(i18n, entry.Timestamp)));
        }

        return info;
    }

    private static bool IsLiteLoaderLegacy(string minecraftVersion)
    {
        var segments = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && int.TryParse(segments[1], out var minor) && minor < 8;
    }

    private static string CreateLiteLoaderSuggestedFileName(string minecraftVersion)
    {
        return "liteloader-installer-" + minecraftVersion + (minecraftVersion is "1.8" or "1.9" ? ".0" : string.Empty) + "-00-SNAPSHOT.jar";
    }

    private static string BuildLiteLoaderDownloadUrl(string minecraftVersion, string fileName, bool isLegacy)
    {
        if (isLegacy)
        {
            return minecraftVersion switch
            {
                "1.7.10" => "https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar",
                "1.7.2" => "https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar",
                "1.6.4" => "https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar",
                "1.6.2" => "https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar",
                "1.5.2" => "https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar",
                _ => string.Empty
            };
        }

        var artifactFolder = minecraftVersion == "1.8" ? "ant/dist/" : "build/libs/";
        return $"http://jenkins.liteloader.com/job/LiteLoaderInstaller%20{minecraftVersion}/lastSuccessfulBuild/artifact/{artifactFolder}{fileName}";
    }
}

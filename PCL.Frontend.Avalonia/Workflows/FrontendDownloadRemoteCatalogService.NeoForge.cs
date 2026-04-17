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
    private static FrontendDownloadCatalogState BuildNeoForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadNeoForge, i18n);
        var payload = FetchNeoForgeEntries(versionSourceIndex);
        var sections = BuildGroupedInstallerSections(
            payload.Value,
            entry => entry.MinecraftVersion,
            group => group.Key,
            entry => CreateInstallerDownloadEntry(
                entry.Title,
                entry.IsPreview
                    ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                    : Text(i18n, "download.install.choices.summaries.stable", "Stable"),
                entry.TargetUrl,
                Path.GetFileName(entry.TargetUrl)),
            i18n);

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("remote_catalog", EnsureEntries([], Text(i18n, "download.catalog.remote.empty.neoforge", "There are no available NeoForge remote entries right now."), i18n))]);
    }
    private static RemotePayload<List<NeoForgeCatalogEntry>> FetchNeoForgeEntries(int versionSourceIndex)
    {
        var latestPayload = FetchJsonObject(CreateNeoForgeLatestSources(versionSourceIndex), versionSourceIndex);
        var legacyPayload = FetchJsonObject(CreateNeoForgeLegacySources(versionSourceIndex), versionSourceIndex);
        var versions = new List<string>();
        if (latestPayload.Value["versions"] is JsonArray latestVersions)
        {
            versions.AddRange(latestVersions.Select(node => node?.GetValue<string>()).OfType<string>());
        }

        if (legacyPayload.Value["versions"] is JsonArray legacyVersions)
        {
            versions.AddRange(legacyVersions.Select(node => node?.GetValue<string>()).OfType<string>());
        }

        var baseUrl = latestPayload.Source.IsOfficial
            ? "https://maven.neoforged.net/releases/net/neoforged"
            : "https://bmclapi2.bangbang93.com/maven/net/neoforged";
        var entries = versions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(version => !string.Equals(version, "47.1.82", StringComparison.OrdinalIgnoreCase))
            .Select(version => CreateNeoForgeCatalogEntry(version, baseUrl))
            .OrderByDescending(entry => entry.MinecraftVersion, VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.Title, VersionTextComparer.Instance)
            .ToList();

        return new RemotePayload<List<NeoForgeCatalogEntry>>(latestPayload.Source, entries);
    }

    private static NeoForgeCatalogEntry CreateNeoForgeCatalogEntry(string apiName, string baseUrl)
    {
        string minecraftVersion;
        string packageName;
        if (apiName.Contains("1.20.1-", StringComparison.Ordinal))
        {
            minecraftVersion = "1.20.1";
            packageName = "forge";
            apiName = apiName.Trim();
        }
        else if (apiName.StartsWith("0.", StringComparison.Ordinal))
        {
            minecraftVersion = apiName.Split('.', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? apiName;
            packageName = "neoforge";
        }
        else
        {
            var versionCore = apiName.Split('-', 2)[0];
            var segments = versionCore.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var major = segments.Length > 0 && int.TryParse(segments[0], out var parsedMajor) ? parsedMajor : 0;
            var minor = segments.Length > 1 && int.TryParse(segments[1], out var parsedMinor) ? parsedMinor : 0;
            minecraftVersion = major >= 24
                ? versionCore.TrimEnd('0').TrimEnd('.')
                : "1." + major + (minor > 0 ? "." + minor : string.Empty);
            if (apiName.Contains('+', StringComparison.Ordinal))
            {
                minecraftVersion += "-" + apiName.Split('+', 2)[1];
            }

            packageName = "neoforge";
        }

        return new NeoForgeCatalogEntry(
            minecraftVersion,
            apiName,
            apiName.Contains("beta", StringComparison.OrdinalIgnoreCase)
            || apiName.Contains("alpha", StringComparison.OrdinalIgnoreCase),
            $"{baseUrl}/{packageName}/{apiName}/{packageName}-{apiName}-installer.jar");
    }
}

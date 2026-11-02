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
    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> FetchCatalogStates(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        return new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>
        {
            [LauncherFrontendSubpageKey.DownloadClient] = BuildClientCatalogState(versionSourceIndex, i18n),
            [LauncherFrontendSubpageKey.DownloadOptiFine] = BuildOptiFineCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            [LauncherFrontendSubpageKey.DownloadForge] = BuildForgeCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            [LauncherFrontendSubpageKey.DownloadNeoForge] = BuildNeoForgeCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            [LauncherFrontendSubpageKey.DownloadCleanroom] = BuildCleanroomCatalogState(preferredMinecraftVersion, i18n),
            [LauncherFrontendSubpageKey.DownloadFabric] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadFabric,
                CreateFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateFabricLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            [LauncherFrontendSubpageKey.DownloadLegacyFabric] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadLegacyFabric,
                CreateLegacyFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateLegacyFabricLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            [LauncherFrontendSubpageKey.DownloadQuilt] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadQuilt,
                CreateQuiltRootSources(versionSourceIndex),
                minecraftVersion => CreateQuiltLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            [LauncherFrontendSubpageKey.DownloadLiteLoader] = BuildLiteLoaderCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            [LauncherFrontendSubpageKey.DownloadLabyMod] = BuildLabyModCatalogState(preferredMinecraftVersion, i18n)
        };
    }

    private static FrontendDownloadCatalogState FetchCatalogState(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => BuildClientCatalogState(versionSourceIndex, i18n),
            LauncherFrontendSubpageKey.DownloadOptiFine => BuildOptiFineCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            LauncherFrontendSubpageKey.DownloadForge => BuildForgeCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            LauncherFrontendSubpageKey.DownloadNeoForge => BuildNeoForgeCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            LauncherFrontendSubpageKey.DownloadCleanroom => BuildCleanroomCatalogState(preferredMinecraftVersion, i18n),
            LauncherFrontendSubpageKey.DownloadFabric => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadFabric,
                CreateFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateFabricLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadLegacyFabric,
                CreateLegacyFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateLegacyFabricLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            LauncherFrontendSubpageKey.DownloadQuilt => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadQuilt,
                CreateQuiltRootSources(versionSourceIndex),
                minecraftVersion => CreateQuiltLoaderSources(versionSourceIndex, minecraftVersion),
                i18n),
            LauncherFrontendSubpageKey.DownloadLiteLoader => BuildLiteLoaderCatalogState(versionSourceIndex, preferredMinecraftVersion, i18n),
            LauncherFrontendSubpageKey.DownloadLabyMod => BuildLabyModCatalogState(preferredMinecraftVersion, i18n),
            _ => BuildFailureState(route, "Remote catalog is not supported for this page.", i18n)
        };
    }
    private static FrontendDownloadCatalogState BuildClientCatalogState(int versionSourceIndex, II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadClient, i18n);
        var payload = FetchJsonObject(CreateClientSources(versionSourceIndex), versionSourceIndex);
        var versions = payload.Value["versions"] as JsonArray
                       ?? throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.client_versions_missing", "Minecraft version manifest is missing the versions field."));

        var groupedVersions = new Dictionary<string, List<JsonObject>>
        {
            ["release"] = [],
            ["preview"] = [],
            ["legacy"] = [],
            ["april_fools"] = []
        };

        foreach (var node in versions.Select(item => item as JsonObject).Where(item => item is not null))
        {
            groupedVersions[ClassifyClientCategory(node!)].Add(node!);
        }

        foreach (var group in groupedVersions.Values)
        {
            group.Sort(static (left, right) =>
                ParseReleaseMoment(right["releaseTime"]?.GetValue<string>())
                    .CompareTo(ParseReleaseMoment(left["releaseTime"]?.GetValue<string>())));
        }

        var latest = new List<FrontendDownloadCatalogEntry>();
        if (groupedVersions["release"].Count > 0)
        {
            var latestRelease = groupedVersions["release"][0];
            latest.Add(CreateClientCatalogEntry(
                latestRelease,
                Text(
                    i18n,
                    "download.catalog.remote.latest.release_published",
                    "Latest release, published at {published_at}",
                    ("published_at", FormatReleaseTime(i18n, latestRelease["releaseTime"]?.GetValue<string>()))),
                payload.Source.DisplayName,
                i18n));
        }

        if (groupedVersions["preview"].Count > 0
            && groupedVersions["release"].Count > 0
            && ParseReleaseMoment(groupedVersions["release"][0]["releaseTime"]?.GetValue<string>())
            < ParseReleaseMoment(groupedVersions["preview"][0]["releaseTime"]?.GetValue<string>()))
        {
            var latestPreview = groupedVersions["preview"][0];
            latest.Add(CreateClientCatalogEntry(
                latestPreview,
                Text(
                    i18n,
                    "download.catalog.remote.latest.preview_published",
                    "Latest preview, published at {published_at}",
                    ("published_at", FormatReleaseTime(i18n, latestPreview["releaseTime"]?.GetValue<string>()))),
                payload.Source.DisplayName,
                i18n));
        }

        var sections = new List<FrontendDownloadCatalogSection>
        {
            new("latest_versions", EnsureEntries(latest.ToArray(), Text(i18n, "download.catalog.remote.empty.latest_versions", "The remote version manifest did not return any latest versions."), i18n))
        };
        foreach (var category in new[] { "release", "preview", "legacy", "april_fools" })
        {
            var entries = groupedVersions[category]
                .Select(node => CreateClientCatalogEntry(node, BuildClientVersionInfo(node, i18n), payload.Source.DisplayName, i18n))
                .ToArray();
            if (entries.Length == 0)
            {
                continue;
            }

            sections.Add(new FrontendDownloadCatalogSection($"{category} ({entries.Length})", entries));
        }

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections);
    }

    private static FrontendDownloadCatalogEntry CreateClientCatalogEntry(JsonObject node, string info, string sourceName, II18nService? i18n = null)
    {
        return new FrontendDownloadCatalogEntry(
            NormalizeClientVersionTitle(node["id"]?.GetValue<string>(), i18n),
            info,
            sourceName,
            "view_details",
            node["url"]?.GetValue<string>());
    }
    private static IReadOnlyList<FrontendDownloadCatalogEntry> FetchCatalogSectionEntries(
        LauncherFrontendSubpageKey route,
        string lazyLoadToken,
        int versionSourceIndex,
        string preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadForge => LoadForgeCatalogSectionEntries(versionSourceIndex, lazyLoadToken, i18n),
            _ => throw new InvalidOperationException(Text(i18n, "download.catalog.remote.errors.lazy_route_unsupported", "The current page does not support lazy-loaded remote catalogs."))
        };
    }

}

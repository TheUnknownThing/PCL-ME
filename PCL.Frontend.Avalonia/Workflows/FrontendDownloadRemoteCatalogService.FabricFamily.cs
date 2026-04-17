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
    private static FrontendDownloadCatalogState BuildFabricFamilyCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        LauncherFrontendSubpageKey route,
        IReadOnlyList<RemoteSource> rootSources,
        Func<string, IReadOnlyList<RemoteSource>> loaderSourceFactory,
        II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(route, i18n);
        var rootPayload = FetchJsonObject(rootSources, versionSourceIndex);
        var installerEntries = (rootPayload.Value["installer"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["version"]?.GetValue<string>()))
            .Select(node => CreateFabricFamilyCatalogEntry(route, node!, i18n))
            .ToArray() ?? [];

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [new FrontendDownloadCatalogSection($"version_list ({installerEntries.Length})", EnsureEntries(installerEntries, Text(i18n, "download.catalog.remote.empty.installer_versions", "There are no available installer versions right now."), i18n))]);
    }
    private static FrontendDownloadCatalogEntry CreateFabricFamilyCatalogEntry(LauncherFrontendSubpageKey route, JsonObject node, II18nService? i18n = null)
    {
        var version = node["version"]?.GetValue<string>() ?? Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
        var title = route == LauncherFrontendSubpageKey.DownloadFabric
            ? version.Replace("+build", string.Empty, StringComparison.Ordinal)
            : version;
        var info = route switch
        {
            LauncherFrontendSubpageKey.DownloadQuilt => Text(i18n, "download.catalog.remote.labels.installer", "Installer"),
            _ => node["stable"]?.GetValue<bool>() == true
                ? Text(i18n, "download.install.choices.summaries.stable", "Stable")
                : Text(i18n, "download.install.choices.summaries.testing", "Testing")
        };
        var targetUrl = node["url"]?.GetValue<string>();
        return CreateInstallerDownloadEntry(
            title,
            info,
            targetUrl,
            DeriveFileNameFromUrl(targetUrl) ?? title + ".jar");
    }
}

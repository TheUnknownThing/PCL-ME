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
    private static FrontendDownloadCatalogState BuildCleanroomCatalogState(string preferredMinecraftVersion, II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadCleanroom, i18n);
        var payload = FetchJsonArray(
            [new RemoteSource("Cleanroom GitHub Releases", "https://api.github.com/repos/CleanroomMC/Cleanroom/releases", true)],
            1);
        var entries = payload.Value
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["tag_name"]?.GetValue<string>()))
            .Select(node => CreateCleanroomCatalogEntry(node!, i18n))
            .OrderByDescending(entry => entry.Title, VersionTextComparer.Instance)
            .ToArray();

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [
                new FrontendDownloadCatalogSection(
                    $"1.12.2 ({entries.Length})",
                    EnsureEntries(entries, Text(i18n, "download.catalog.remote.empty.cleanroom", "There are no available Cleanroom remote entries right now."), i18n),
                    IsCollapsible: true,
                    IsInitiallyExpanded: false)
            ]);
    }
    private static FrontendDownloadCatalogState BuildLabyModCatalogState(string preferredMinecraftVersion, II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadLabyMod, i18n);
        var production = FetchJsonObject(
            [new RemoteSource("LabyMod Production", "https://releases.r2.labymod.net/api/v1/manifest/production/latest.json", true)],
            1);
        var snapshot = FetchJsonObject(
            [new RemoteSource("LabyMod Snapshot", "https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json", true)],
            1);

        var channelEntries = new[]
        {
            CreateLabyModEntry("production", Text(i18n, "download.install.choices.channels.stable", "Stable"), production.Value, i18n),
            CreateLabyModEntry("snapshot", Text(i18n, "download.install.choices.channels.snapshot", "Snapshot"), snapshot.Value, i18n)
        };

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [new FrontendDownloadCatalogSection($"version_list ({channelEntries.Length})", EnsureEntries(channelEntries, Text(i18n, "download.catalog.remote.empty.labymod_channels", "There are no available LabyMod channels right now."), i18n))]);
    }

    private static FrontendDownloadCatalogEntry CreateLabyModEntry(
        string channel,
        string channelLabel,
        JsonObject manifest,
        II18nService? i18n = null)
    {
        var version = manifest["labyModVersion"]?.GetValue<string>() ?? Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
        return CreateInstallerDownloadEntry(
            $"{version} {channelLabel}",
            channel == "snapshot"
                ? Text(i18n, "download.install.choices.channels.snapshot", "Snapshot")
                : Text(i18n, "download.install.choices.channels.stable", "Stable"),
            $"https://releases.labymod.net/api/v1/installer/{channel}/java",
            channel == "snapshot" ? "LabyMod4SnapshotInstaller.jar" : "LabyMod4ProductionInstaller.jar");
    }
    private static FrontendDownloadCatalogEntry CreateCleanroomCatalogEntry(JsonObject node, II18nService? i18n = null)
    {
        var tag = node["tag_name"]?.GetValue<string>() ?? Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
        var installerAsset = FindGitHubAssetDownloadUrl(node, "-installer.jar");
        return CreateInstallerDownloadEntry(
            tag,
            IsPreReleaseTag(tag)
                ? Text(i18n, "download.install.choices.summaries.testing", "Testing")
                : Text(i18n, "download.install.choices.summaries.stable", "Stable"),
            installerAsset ?? $"https://github.com/CleanroomMC/Cleanroom/releases/download/{Uri.EscapeDataString(tag)}/cleanroom-{tag}-installer.jar",
            installerAsset is null ? $"cleanroom-{tag}-installer.jar" : Path.GetFileName(installerAsset));
    }
    private static string? FindGitHubAssetDownloadUrl(JsonObject release, string assetNameSuffix)
    {
        return (release["assets"] as JsonArray)?
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => node?["name"]?.GetValue<string>()?.EndsWith(assetNameSuffix, StringComparison.OrdinalIgnoreCase) == true)?["browser_download_url"]
            ?.GetValue<string>();
    }

    private static bool IsPreReleaseTag(string value)
    {
        return value.Contains("alpha", StringComparison.OrdinalIgnoreCase)
               || value.Contains("beta", StringComparison.OrdinalIgnoreCase)
               || value.Contains("pre", StringComparison.OrdinalIgnoreCase)
               || value.Contains("rc", StringComparison.OrdinalIgnoreCase);
    }
}

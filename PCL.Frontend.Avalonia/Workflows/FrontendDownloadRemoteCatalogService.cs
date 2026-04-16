using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDownloadRemoteCatalogService
{
    private const string SnapshotSectionKey = "snapshot_versions";
    private static readonly HttpClient HttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));
    private static readonly object CacheSync = new();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<RemoteCatalogCacheKey, RemoteCatalogCacheEntry> Cache = [];
    private static readonly Dictionary<RouteCatalogCacheKey, RouteCatalogCacheEntry> RouteCache = [];
    private static readonly LauncherFrontendSubpageKey[] CatalogRoutes =
    [
        LauncherFrontendSubpageKey.DownloadClient,
        LauncherFrontendSubpageKey.DownloadOptiFine,
        LauncherFrontendSubpageKey.DownloadForge,
        LauncherFrontendSubpageKey.DownloadNeoForge,
        LauncherFrontendSubpageKey.DownloadCleanroom,
        LauncherFrontendSubpageKey.DownloadFabric,
        LauncherFrontendSubpageKey.DownloadLegacyFabric,
        LauncherFrontendSubpageKey.DownloadQuilt,
        LauncherFrontendSubpageKey.DownloadLiteLoader,
        LauncherFrontendSubpageKey.DownloadLabyMod
    ];

    public static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildCatalogStates(
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var cacheKey = new RemoteCatalogCacheKey(versionSourceIndex, normalizedVersion);
        RemoteCatalogCacheEntry? staleEntry;

        lock (CacheSync)
        {
            if (Cache.TryGetValue(cacheKey, out var cachedEntry)
                && DateTimeOffset.UtcNow - cachedEntry.FetchedAtUtc <= CacheLifetime)
            {
                return cachedEntry.States;
            }

            Cache.TryGetValue(cacheKey, out staleEntry);
        }

        try
        {
            var states = FetchCatalogStates(versionSourceIndex, normalizedVersion, i18n);
            lock (CacheSync)
            {
                Cache[cacheKey] = new RemoteCatalogCacheEntry(DateTimeOffset.UtcNow, states);
            }

            return states;
        }
        catch (Exception ex)
        {
            if (staleEntry is not null)
            {
                return staleEntry.States.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value with { StaleError = ex.Message, LoadError = null });
            }

            return BuildFailureStates(ex.Message, i18n);
        }
    }

    public static Task<FrontendDownloadCatalogState> LoadCatalogStateAsync(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => BuildCatalogState(route, versionSourceIndex, preferredMinecraftVersion, i18n),
            cancellationToken);
    }

    public static Task<IReadOnlyList<FrontendDownloadCatalogEntry>> LoadCatalogSectionEntriesAsync(
        LauncherFrontendSubpageKey route,
        string lazyLoadToken,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => FetchCatalogSectionEntries(route, lazyLoadToken, versionSourceIndex, NormalizeMinecraftVersion(preferredMinecraftVersion), i18n),
            cancellationToken);
    }

    public static string GetLoadingText(LauncherFrontendSubpageKey route, II18nService? i18n = null)
    {
        return GetGoldCatalogDescriptor(route, i18n).LoadingText;
    }

    public static FrontendDownloadCatalogState BuildCatalogState(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        II18nService? i18n = null)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var cacheKey = new RouteCatalogCacheKey(route, versionSourceIndex, normalizedVersion);
        RouteCatalogCacheEntry? staleEntry;

        lock (CacheSync)
        {
            if (RouteCache.TryGetValue(cacheKey, out var cachedEntry)
                && DateTimeOffset.UtcNow - cachedEntry.FetchedAtUtc <= CacheLifetime)
            {
                return cachedEntry.State;
            }

            RouteCache.TryGetValue(cacheKey, out staleEntry);
        }

        try
        {
            var state = FetchCatalogState(route, versionSourceIndex, normalizedVersion, i18n);
            lock (CacheSync)
            {
                RouteCache[cacheKey] = new RouteCatalogCacheEntry(DateTimeOffset.UtcNow, state);
            }

            return state;
        }
        catch (Exception ex)
        {
            if (staleEntry is not null)
            {
                return staleEntry.State with { StaleError = ex.Message, LoadError = null };
            }

            return BuildFailureState(route, ex.Message, i18n);
        }
    }

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

    private static IReadOnlyList<FrontendDownloadCatalogSection> BuildGroupedInstallerSections<T>(
        IEnumerable<T> items,
        Func<T, string> groupKeySelector,
        Func<IGrouping<string, T>, string> groupTitleSelector,
        Func<T, FrontendDownloadCatalogEntry> entrySelector,
        II18nService? i18n = null)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(groupKeySelector(item)))
            .GroupBy(groupKeySelector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupEntries = group.Select(entrySelector).ToArray();
                return new FrontendDownloadCatalogSection(
                    $"{groupTitleSelector(group)} ({groupEntries.Length})",
                    EnsureEntries(groupEntries, Text(i18n, "download.catalog.remote.empty.group_entries", "There are no available entries for {group_name}.", ("group_name", group.Key)), i18n),
                    IsCollapsible: true,
                    IsInitiallyExpanded: false);
            })
            .ToArray();
    }

    private static FrontendDownloadCatalogEntry CreateInstallerDownloadEntry(
        string title,
        string info,
        string? targetUrl,
        string? suggestedFileName = null)
    {
        return new FrontendDownloadCatalogEntry(
            title,
            info,
            string.Empty,
            "save_installer",
            targetUrl,
            FrontendDownloadCatalogEntryActionKind.DownloadFile,
            suggestedFileName);
    }

    private static FrontendDownloadCatalogEntry CreateOptiFineCatalogEntry(OptiFineCatalogEntry entry, II18nService? i18n = null)
    {
        return CreateInstallerDownloadEntry(
            entry.DisplayName,
            BuildOptiFineInfo(entry, i18n),
            entry.TargetUrl,
            entry.SuggestedFileName);
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

    private static IEnumerable<OptiFineCatalogEntry> OrderOptiFineEntries(IEnumerable<OptiFineCatalogEntry> entries)
    {
        return entries
            .OrderBy(entry => GetOptiFineSectionKey(entry) == SnapshotSectionKey ? 0 : 1)
            .ThenByDescending(entry => GetOptiFineGroupVersion(entry) ?? string.Empty, VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.DisplayName, VersionTextComparer.Instance);
    }

    private static IEnumerable<LiteLoaderCatalogEntry> OrderLiteLoaderEntries(IEnumerable<LiteLoaderCatalogEntry> entries)
    {
        return entries
            .OrderByDescending(entry => GetLiteLoaderSectionKey(entry), VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.MinecraftVersion, VersionTextComparer.Instance);
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

    private static string? DeriveFileNameFromUrl(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return null;
        }

        return Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.LocalPath)
            : null;
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildFailureStates(string error, II18nService? i18n = null)
    {
        return CatalogRoutes.ToDictionary(
            route => route,
            route => BuildFailureState(route, error, i18n));
    }

    private static FrontendDownloadCatalogState BuildFailureState(LauncherFrontendSubpageKey route, string error, II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(route, i18n);
        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [],
            LoadError: error);
    }

    private static string ClassifyClientCategory(JsonObject version)
    {
        var type = version["type"]?.GetValue<string>() ?? string.Empty;
        var id = version["id"]?.GetValue<string>() ?? string.Empty;
        switch (type.ToLowerInvariant())
        {
            case "release":
                return "release";
            case "snapshot":
            case "pending":
                if (id.StartsWith("1.", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("combat", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("rc", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("experimental", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(id, "1.2", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("pre", StringComparison.OrdinalIgnoreCase))
                {
                    return "release";
                }

                return IsAprilFoolsVersion(id, version["releaseTime"]?.GetValue<string>()) ? "april_fools" : "preview";
            case "special":
                return "april_fools";
            default:
                return "legacy";
        }
    }

    private static string BuildClientVersionInfo(JsonObject version, II18nService? i18n = null)
    {
        var id = NormalizeClientVersionTitle(version["id"]?.GetValue<string>(), i18n);
        var foolName = GetClientAprilFoolsName(id, i18n);
        if (!string.IsNullOrWhiteSpace(foolName))
        {
            return foolName;
        }

        return FormatReleaseTime(i18n, version["releaseTime"]?.GetValue<string>());
    }

    private static bool IsAprilFoolsVersion(string id, string? releaseTime)
    {
        if (!string.IsNullOrWhiteSpace(GetClientAprilFoolsName(id, null)))
        {
            return true;
        }

        var releaseMoment = ParseReleaseMoment(releaseTime);
        return releaseMoment != DateTimeOffset.MinValue
               && releaseMoment.UtcDateTime.AddHours(2).Month == 4
               && releaseMoment.UtcDateTime.AddHours(2).Day == 1;
    }

    private static string NormalizeClientVersionTitle(string? id, II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
        }

        return id switch
        {
            "2point0_blue" => "2.0_blue",
            "2point0_red" => "2.0_red",
            "2point0_purple" => "2.0_purple",
            "20w14infinite" => "20w14∞",
            _ => id
        };
    }

    private static string GetClientAprilFoolsName(string? name, II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.ToLowerInvariant();
        if (normalized.StartsWith("2.0", StringComparison.Ordinal) || normalized.StartsWith("2point0", StringComparison.Ordinal))
        {
            var tag = normalized.EndsWith("red", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_red", " (Red variant)")
                : normalized.EndsWith("blue", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_blue", " (Blue variant)")
                : normalized.EndsWith("purple", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_purple", " (Purple variant)")
                : string.Empty;
            return Text(i18n, "download.install.choices.summaries.april_fools.2013", "2013 | This secret update, planned for two years, took the game to a new level!") + tag;
        }

        return normalized switch
        {
            "15w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2015", "2015 | As a game for all ages, we need peace, love, and hugs."),
            "1.rv-pre1" => Text(i18n, "download.install.choices.summaries.april_fools.2016", "2016 | It's time to bring modern technology into Minecraft!"),
            "3d shareware v1.34" => Text(i18n, "download.install.choices.summaries.april_fools.2019", "2019 | We found this masterpiece from 1994 in the ruins of a basement!"),
            "20w14infinite" or "20w14∞" => Text(i18n, "download.install.choices.summaries.april_fools.2020", "2020 | We added 2 billion new dimensions and turned infinite imagination into reality!"),
            "22w13oneblockatatime" => Text(i18n, "download.install.choices.summaries.april_fools.2022", "2022 | One block at a time! Meet new digging, crafting, and riding gameplay."),
            "23w13a_or_b" => Text(i18n, "download.install.choices.summaries.april_fools.2023", "2023 | Research shows players like making choices, and the more the better!"),
            "24w14potato" => Text(i18n, "download.install.choices.summaries.april_fools.2024", "2024 | Poisonous potatoes have always been ignored and underestimated, so we supercharged them!"),
            "25w14craftmine" => Text(i18n, "download.install.choices.summaries.april_fools.2025", "2025 | You can craft anything, including your world itself!"),
            "26w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2026", "2026 | Why do you need an inventory? Let the blocks follow you instead!"),
            _ => string.Empty
        };
    }

    private static GoldCatalogDescriptor GetGoldCatalogDescriptor(LauncherFrontendSubpageKey route, II18nService? i18n = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => new GoldCatalogDescriptor(string.Empty, string.Empty, "fetch_versions", []),
            LauncherFrontendSubpageKey.DownloadForge => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://files.minecraftforge.net", true))),
            LauncherFrontendSubpageKey.DownloadNeoForge => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://neoforged.net/", true))),
            LauncherFrontendSubpageKey.DownloadFabric => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.fabricmc.net", true))),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://legacyfabric.net/", true))),
            LauncherFrontendSubpageKey.DownloadQuilt => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://quiltmc.org", true))),
            LauncherFrontendSubpageKey.DownloadOptiFine => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.optifine.net/", true))),
            LauncherFrontendSubpageKey.DownloadLiteLoader => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.liteloader.com", true))),
            LauncherFrontendSubpageKey.DownloadLabyMod => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://labymod.net", true))),
            LauncherFrontendSubpageKey.DownloadCleanroom => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://cleanroommc.com/", true))),
            _ => new GoldCatalogDescriptor(string.Empty, string.Empty, "fetch_list", [])
        };
    }

    private static IReadOnlyList<FrontendDownloadCatalogAction> CreateActions(params FrontendDownloadCatalogAction[] actions)
    {
        return actions.Where(action => !string.IsNullOrWhiteSpace(action.Target)).ToArray();
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> EnsureEntries(
        IReadOnlyList<FrontendDownloadCatalogEntry> entries,
        string emptyMessage,
        II18nService? i18n = null)
    {
        return entries.Count > 0
            ? entries
            : [new FrontendDownloadCatalogEntry(Text(i18n, "download.catalog.remote.labels.no_display_data", "Nothing to display"), emptyMessage, string.Empty, "view_details", null)];
    }

    private static string Text(
        II18nService? i18n,
        string key,
        string fallback,
        params (string Key, object? Value)[] args)
    {
        if (i18n is null)
        {
            return ApplyFallbackArgs(fallback, args);
        }

        if (args.Length == 0)
        {
            return i18n.T(key);
        }

        return i18n.T(
            key,
            args.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }

    private static string ApplyFallbackArgs(string fallback, IReadOnlyList<(string Key, object? Value)> args)
    {
        var result = fallback;
        foreach (var (key, value) in args)
        {
            result = result.Replace("{" + key + "}", value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
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

    private static RemotePayload<JsonObject> FetchJsonObject(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<JsonObject>(
                    sources[index],
                    JsonNode.Parse(FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)))?.AsObject()
                    ?? throw new InvalidOperationException($"Unable to parse JSON object: {sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static RemotePayload<JsonArray> FetchJsonArray(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<JsonArray>(
                    sources[index],
                    JsonNode.Parse(FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)))?.AsArray()
                    ?? throw new InvalidOperationException($"Unable to parse JSON array: {sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static RemotePayload<string> FetchString(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<string>(
                    sources[index],
                    FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static string FetchStringContent(RemoteSource source, TimeSpan timeout)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
        request.Headers.UserAgent.ParseAdd("PCL-ME-Frontend");
        using var cts = new CancellationTokenSource(timeout);
        using var response = HttpClient.Send(request, cts.Token);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
    }

    private static TimeSpan GetRequestTimeout(int versionSourceIndex, int sourceAttemptIndex)
    {
        return versionSourceIndex switch
        {
            0 => sourceAttemptIndex == 0 ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(10),
            1 => sourceAttemptIndex == 0 ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(15)
        };
    }

    private static IReadOnlyList<RemoteSource> CreateClientSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Mojang Official", "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", false));
    }

    private static IReadOnlyList<RemoteSource> CreateOptiFineSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("OptiFine Official", "https://optifine.net/downloads", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/optifine/versionList", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeListSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge Official", "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/forge/minecraft", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeVersionSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = minecraftVersion.Replace("-", "_", StringComparison.Ordinal);
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge Official", $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{normalizedVersion}.html", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/forge/minecraft/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLatestSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge Official", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLegacySources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge Official", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricRootSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric Official", "https://meta.fabricmc.net/v2/versions", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric Official", $"https://meta.fabricmc.net/v2/versions/loader/{normalizedVersion}", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Legacy Fabric Official", "https://meta.legacyfabric.net/v2/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "1.12.2" : minecraftVersion;
        return
        [
            new RemoteSource("Legacy Fabric Official", $"https://meta.legacyfabric.net/v2/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Quilt Official", "https://meta.quiltmc.org/v3/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return
        [
            new RemoteSource("Quilt Official", $"https://meta.quiltmc.org/v3/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLiteLoaderSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("LiteLoader Official", "https://dl.liteloader.com/versions/versions.json", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json", false));
    }

    private static IReadOnlyList<RemoteSource> CreateSourceSequence(
        int versionSourceIndex,
        RemoteSource officialSource,
        RemoteSource? mirrorSource)
    {
        if (mirrorSource is null)
        {
            return [officialSource];
        }

        return versionSourceIndex == 0
            ? [mirrorSource, officialSource]
            : [officialSource, mirrorSource];
    }

    private static string NormalizeMinecraftVersion(string? preferredMinecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredMinecraftVersion))
        {
            return string.Empty;
        }

        var value = preferredMinecraftVersion.Trim();
        return value.Any(char.IsDigit) ? value : string.Empty;
    }

    private static string FormatReleaseTime(II18nService? i18n, string? value)
    {
        var moment = ParseReleaseMoment(value);
        return moment == DateTimeOffset.MinValue
            ? Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable")
            : moment.LocalDateTime.ToString("yyyy/MM/dd HH:mm");
    }

    private static DateTimeOffset ParseReleaseMoment(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string FormatDdMmYyyy(II18nService? i18n, string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return string.IsNullOrWhiteSpace(value) ? Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable") : value;
        }

        return $"{parts[2]}/{parts[1]}/{parts[0]}";
    }

    private static string FormatUnixTime(II18nService? i18n, long seconds)
    {
        if (seconds <= 0)
        {
            return Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
    }

    private static long ReadInt64(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return 0;
    }

    private static string GetRouteTitle(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => "Minecraft",
            LauncherFrontendSubpageKey.DownloadOptiFine => "OptiFine",
            LauncherFrontendSubpageKey.DownloadForge => "Forge",
            LauncherFrontendSubpageKey.DownloadNeoForge => "NeoForge",
            LauncherFrontendSubpageKey.DownloadCleanroom => "Cleanroom",
            LauncherFrontendSubpageKey.DownloadFabric => "Fabric",
            LauncherFrontendSubpageKey.DownloadLegacyFabric => "Legacy Fabric",
            LauncherFrontendSubpageKey.DownloadQuilt => "Quilt",
            LauncherFrontendSubpageKey.DownloadLiteLoader => "LiteLoader",
            LauncherFrontendSubpageKey.DownloadLabyMod => "LabyMod",
            _ => route.ToString()
        };
    }

    private sealed record RemoteCatalogCacheKey(int SourceIndex, string PreferredMinecraftVersion);

    private sealed record RouteCatalogCacheKey(
        LauncherFrontendSubpageKey Route,
        int SourceIndex,
        string PreferredMinecraftVersion);

    private sealed record RemoteCatalogCacheEntry(
        DateTimeOffset FetchedAtUtc,
        IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> States);

    private sealed record RouteCatalogCacheEntry(
        DateTimeOffset FetchedAtUtc,
        FrontendDownloadCatalogState State);

    private sealed record GoldCatalogDescriptor(
        string IntroTitle,
        string IntroBody,
        string LoadingText,
        IReadOnlyList<FrontendDownloadCatalogAction> Actions);

    private sealed record RemoteSource(string DisplayName, string Url, bool IsOfficial);

    private sealed record RemotePayload<T>(RemoteSource Source, T Value);

    private sealed record OptiFineCatalogEntry(
        string MinecraftVersion,
        string DisplayName,
        bool IsPreview,
        string ReleaseTime,
        string? RequiredForgeVersion,
        string TargetUrl,
        string SuggestedFileName);

    private sealed record ForgeVersionCatalogEntry(
        string MinecraftVersion,
        string VersionName,
        string FileVersion,
        string Category,
        string FileExtension,
        bool IsRecommended,
        string ReleaseTime,
        string TargetUrl,
        string SuggestedFileName);

    private sealed record NeoForgeCatalogEntry(
        string MinecraftVersion,
        string Title,
        bool IsPreview,
        string TargetUrl);

    private sealed record LiteLoaderCatalogEntry(
        string MinecraftVersion,
        string FileName,
        bool IsPreview,
        bool IsLegacy,
        long Timestamp,
        string TargetUrl);

    private sealed class VersionTextComparer : IComparer<string>
    {
        public static VersionTextComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var leftTokens = Regex.Matches(left, @"\d+|[^\d]+").Select(match => match.Value).ToArray();
            var rightTokens = Regex.Matches(right, @"\d+|[^\d]+").Select(match => match.Value).ToArray();
            var tokenCount = Math.Min(leftTokens.Length, rightTokens.Length);
            for (var index = 0; index < tokenCount; index++)
            {
                var leftToken = leftTokens[index];
                var rightToken = rightTokens[index];
                var leftIsNumber = int.TryParse(leftToken, out var leftNumber);
                var rightIsNumber = int.TryParse(rightToken, out var rightNumber);
                var comparison = leftIsNumber && rightIsNumber
                    ? leftNumber.CompareTo(rightNumber)
                    : string.Compare(leftToken, rightToken, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftTokens.Length.CompareTo(rightTokens.Length);
        }
    }
}

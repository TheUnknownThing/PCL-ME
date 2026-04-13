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
    private static readonly HttpClient HttpClient = new();
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
        string? preferredMinecraftVersion)
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
            var states = FetchCatalogStates(versionSourceIndex, normalizedVersion);
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
                    pair => pair.Value with
                    {
                        IntroBody = $"{pair.Value.IntroBody} 刷新远程目录失败，因此暂时保留上次成功同步的结果。错误：{ex.Message}"
                    });
            }

            return BuildFailureStates(ex.Message);
        }
    }

    public static Task<FrontendDownloadCatalogState> LoadCatalogStateAsync(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => BuildCatalogState(route, versionSourceIndex, preferredMinecraftVersion),
            cancellationToken);
    }

    public static Task<IReadOnlyList<FrontendDownloadCatalogEntry>> LoadCatalogSectionEntriesAsync(
        LauncherFrontendSubpageKey route,
        string lazyLoadToken,
        int versionSourceIndex,
        string? preferredMinecraftVersion,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => FetchCatalogSectionEntries(route, lazyLoadToken, versionSourceIndex, NormalizeMinecraftVersion(preferredMinecraftVersion)),
            cancellationToken);
    }

    public static string GetLoadingText(LauncherFrontendSubpageKey route)
    {
        return GetGoldCatalogDescriptor(route).LoadingText;
    }

    public static FrontendDownloadCatalogState BuildCatalogState(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string? preferredMinecraftVersion)
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
            var state = FetchCatalogState(route, versionSourceIndex, normalizedVersion);
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
                return staleEntry.State with
                {
                    IntroBody = $"{staleEntry.State.IntroBody} 刷新远程目录失败，因此暂时保留上次成功同步的结果。错误：{ex.Message}"
                };
            }

            return BuildFailureState(route, ex.Message);
        }
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> FetchCatalogStates(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        return new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState>
        {
            [LauncherFrontendSubpageKey.DownloadClient] = BuildClientCatalogState(versionSourceIndex),
            [LauncherFrontendSubpageKey.DownloadOptiFine] = BuildOptiFineCatalogState(versionSourceIndex, preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadForge] = BuildForgeCatalogState(versionSourceIndex, preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadNeoForge] = BuildNeoForgeCatalogState(versionSourceIndex, preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadCleanroom] = BuildCleanroomCatalogState(preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadFabric] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadFabric,
                CreateFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadLegacyFabric] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadLegacyFabric,
                CreateLegacyFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateLegacyFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadQuilt] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadQuilt,
                CreateQuiltRootSources(versionSourceIndex),
                minecraftVersion => CreateQuiltLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadLiteLoader] = BuildLiteLoaderCatalogState(versionSourceIndex, preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadLabyMod] = BuildLabyModCatalogState(preferredMinecraftVersion)
        };
    }

    private static FrontendDownloadCatalogState FetchCatalogState(
        LauncherFrontendSubpageKey route,
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => BuildClientCatalogState(versionSourceIndex),
            LauncherFrontendSubpageKey.DownloadOptiFine => BuildOptiFineCatalogState(versionSourceIndex, preferredMinecraftVersion),
            LauncherFrontendSubpageKey.DownloadForge => BuildForgeCatalogState(versionSourceIndex, preferredMinecraftVersion),
            LauncherFrontendSubpageKey.DownloadNeoForge => BuildNeoForgeCatalogState(versionSourceIndex, preferredMinecraftVersion),
            LauncherFrontendSubpageKey.DownloadCleanroom => BuildCleanroomCatalogState(preferredMinecraftVersion),
            LauncherFrontendSubpageKey.DownloadFabric => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadFabric,
                CreateFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadLegacyFabric,
                CreateLegacyFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateLegacyFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            LauncherFrontendSubpageKey.DownloadQuilt => BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                LauncherFrontendSubpageKey.DownloadQuilt,
                CreateQuiltRootSources(versionSourceIndex),
                minecraftVersion => CreateQuiltLoaderSources(versionSourceIndex, minecraftVersion)),
            LauncherFrontendSubpageKey.DownloadLiteLoader => BuildLiteLoaderCatalogState(versionSourceIndex, preferredMinecraftVersion),
            LauncherFrontendSubpageKey.DownloadLabyMod => BuildLabyModCatalogState(preferredMinecraftVersion),
            _ => BuildFailureState(route, "当前页面暂不支持远程目录。")
        };
    }

    private static FrontendDownloadCatalogState BuildClientCatalogState(int versionSourceIndex)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadClient);
        var payload = FetchJsonObject(CreateClientSources(versionSourceIndex), versionSourceIndex);
        var versions = payload.Value["versions"] as JsonArray
                       ?? throw new InvalidOperationException("Minecraft 版本清单缺少 versions 字段。");

        var groupedVersions = new Dictionary<string, List<JsonObject>>
        {
            ["正式版"] = [],
            ["预览版"] = [],
            ["远古版"] = [],
            ["愚人节版"] = []
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
        if (groupedVersions["正式版"].Count > 0)
        {
            var latestRelease = groupedVersions["正式版"][0];
            latest.Add(CreateClientCatalogEntry(
                latestRelease,
                $"最新正式版，发布于 {FormatReleaseTime(latestRelease["releaseTime"]?.GetValue<string>())}",
                payload.Source.DisplayName));
        }

        if (groupedVersions["预览版"].Count > 0
            && groupedVersions["正式版"].Count > 0
            && ParseReleaseMoment(groupedVersions["正式版"][0]["releaseTime"]?.GetValue<string>())
            < ParseReleaseMoment(groupedVersions["预览版"][0]["releaseTime"]?.GetValue<string>()))
        {
            var latestPreview = groupedVersions["预览版"][0];
            latest.Add(CreateClientCatalogEntry(
                latestPreview,
                $"最新预览版，发布于 {FormatReleaseTime(latestPreview["releaseTime"]?.GetValue<string>())}",
                payload.Source.DisplayName));
        }

        var sections = new List<FrontendDownloadCatalogSection>
        {
            new("最新版本", EnsureEntries(latest.ToArray(), "当前远程版本清单没有返回最新版本。"))
        };
        foreach (var category in new[] { "正式版", "预览版", "远古版", "愚人节版" })
        {
            var entries = groupedVersions[category]
                .Select(node => CreateClientCatalogEntry(node, BuildClientVersionInfo(node), payload.Source.DisplayName))
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

    private static FrontendDownloadCatalogEntry CreateClientCatalogEntry(JsonObject node, string info, string sourceName)
    {
        return new FrontendDownloadCatalogEntry(
            NormalizeClientVersionTitle(node["id"]?.GetValue<string>()),
            info,
            sourceName,
            "查看清单",
            node["url"]?.GetValue<string>());
    }

    private static FrontendDownloadCatalogState BuildOptiFineCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadOptiFine);
        var payload = FetchOptiFineEntries(versionSourceIndex);
        var groupedSections = BuildGroupedInstallerSections(
            OrderOptiFineEntries(payload.Value),
            GetOptiFineSectionKey,
            static group => group.Key == "快照版本" ? "快照版本" : group.Key,
            CreateOptiFineCatalogEntry);

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            groupedSections.Count > 0
                ? groupedSections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 OptiFine 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadForge);
        var sources = CreateForgeListSources(versionSourceIndex);
        var payload = FetchString(sources, versionSourceIndex);
        var minecraftVersions = payload.Source.IsOfficial
            ? ParseForgeMinecraftVersionsFromHtml(payload.Value)
            : ParseForgeMinecraftVersionsFromPlainText(payload.Value);

        if (minecraftVersions.Count == 0)
        {
            throw new InvalidOperationException("Forge 远程目录没有返回任何 Minecraft 版本。");
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
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前 Forge 远程目录没有返回任何 Minecraft 版本。"))]);
    }

    private static FrontendDownloadCatalogState BuildNeoForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadNeoForge);
        var payload = FetchNeoForgeEntries(versionSourceIndex);
        var sections = BuildGroupedInstallerSections(
            payload.Value,
            entry => entry.MinecraftVersion,
            group => group.Key,
            entry => CreateInstallerDownloadEntry(
                entry.Title,
                entry.IsPreview ? "测试版" : "稳定版",
                entry.TargetUrl,
                Path.GetFileName(entry.TargetUrl)));

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 NeoForge 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildCleanroomCatalogState(string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadCleanroom);
        var payload = FetchJsonArray(
            [new RemoteSource("Cleanroom GitHub Releases", "https://api.github.com/repos/CleanroomMC/Cleanroom/releases", true)],
            1);
        var entries = payload.Value
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["tag_name"]?.GetValue<string>()))
            .Select(node => CreateCleanroomCatalogEntry(node!))
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
                    EnsureEntries(entries, "当前没有可用的 Cleanroom 远程条目。"),
                    IsCollapsible: true,
                    IsInitiallyExpanded: false)
            ]);
    }

    private static FrontendDownloadCatalogState BuildFabricFamilyCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        LauncherFrontendSubpageKey route,
        IReadOnlyList<RemoteSource> rootSources,
        Func<string, IReadOnlyList<RemoteSource>> loaderSourceFactory)
    {
        var descriptor = GetGoldCatalogDescriptor(route);
        var rootPayload = FetchJsonObject(rootSources, versionSourceIndex);
        var installerEntries = (rootPayload.Value["installer"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["version"]?.GetValue<string>()))
            .Select(node => CreateFabricFamilyCatalogEntry(route, node!))
            .ToArray() ?? [];

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [new FrontendDownloadCatalogSection($"版本列表 ({installerEntries.Length})", EnsureEntries(installerEntries, "当前没有可用的安装器版本。"))]);
    }

    private static FrontendDownloadCatalogState BuildLiteLoaderCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadLiteLoader);
        var payload = FetchJsonObject(CreateLiteLoaderSources(versionSourceIndex), versionSourceIndex);
        var versions = payload.Value["versions"] as JsonObject
                       ?? throw new InvalidOperationException("LiteLoader 目录缺少 versions 字段。");
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
                BuildLiteLoaderInfo(entry),
                entry.TargetUrl,
                entry.FileName));

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 LiteLoader 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildLabyModCatalogState(string preferredMinecraftVersion)
    {
        var descriptor = GetGoldCatalogDescriptor(LauncherFrontendSubpageKey.DownloadLabyMod);
        var production = FetchJsonObject(
            [new RemoteSource("LabyMod Production", "https://releases.r2.labymod.net/api/v1/manifest/production/latest.json", true)],
            1);
        var snapshot = FetchJsonObject(
            [new RemoteSource("LabyMod Snapshot", "https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json", true)],
            1);

        var channelEntries = new[]
        {
            CreateLabyModEntry("production", "稳定版", production.Value),
            CreateLabyModEntry("snapshot", "快照版", snapshot.Value)
        };

        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [new FrontendDownloadCatalogSection($"版本列表 ({channelEntries.Length})", EnsureEntries(channelEntries, "当前没有可用的 LabyMod 频道。"))]);
    }

    private static FrontendDownloadCatalogEntry CreateLabyModEntry(
        string channel,
        string channelLabel,
        JsonObject manifest)
    {
        var version = manifest["labyModVersion"]?.GetValue<string>() ?? "未知版本";
        return CreateInstallerDownloadEntry(
            $"{version} {channelLabel}",
            channel == "snapshot" ? "快照版" : "稳定版",
            $"https://releases.labymod.net/api/v1/installer/{channel}/java",
            channel == "snapshot" ? "LabyMod4SnapshotInstaller.jar" : "LabyMod4ProductionInstaller.jar");
    }

    private static RemotePayload<List<OptiFineCatalogEntry>> FetchOptiFineEntries(int versionSourceIndex)
    {
        var payload = FetchString(CreateOptiFineSources(versionSourceIndex), versionSourceIndex);
        return payload.Source.IsOfficial
            ? new RemotePayload<List<OptiFineCatalogEntry>>(payload.Source, ParseOptiFineOfficialEntries(payload.Value))
            : new RemotePayload<List<OptiFineCatalogEntry>>(payload.Source, ParseOptiFineMirrorEntries(JsonNode.Parse(payload.Value)?.AsArray()
                                                                                                      ?? throw new InvalidOperationException("无法解析 OptiFine 镜像目录。")));
    }

    private static List<OptiFineCatalogEntry> ParseOptiFineOfficialEntries(string html)
    {
        var forgeMatches = Regex.Matches(html, "(?<=colForge'>)[^<]*");
        var dateMatches = Regex.Matches(html, "(?<=colDate'>)[^<]+");
        var nameMatches = Regex.Matches(html, "(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")");
        if (nameMatches.Count == 0 || nameMatches.Count != dateMatches.Count || nameMatches.Count != forgeMatches.Count)
        {
            throw new InvalidOperationException("OptiFine 官方目录格式不符合预期。");
        }

        var entries = new List<OptiFineCatalogEntry>();
        for (var index = 0; index < nameMatches.Count; index++)
        {
            var rawName = nameMatches[index].Value.Replace('_', ' ');
            var displayName = rawName.Replace("HD U ", string.Empty, StringComparison.Ordinal).Replace(".0 ", " ", StringComparison.Ordinal);
            var minecraftVersion = rawName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "未知版本";
            var requiredForge = NormalizeOptiFineRequiredForgeVersion(forgeMatches[index].Value);
            var isPreview = rawName.Contains("pre", StringComparison.OrdinalIgnoreCase);
            entries.Add(new OptiFineCatalogEntry(
                minecraftVersion,
                displayName,
                isPreview,
                FormatDdMmYyyy(dateMatches[index].Value),
                requiredForge,
                BuildOptiFineDownloadUrl(minecraftVersion, displayName, isPreview),
                CreateOptiFineSuggestedFileName(displayName, isPreview)));
        }

        return entries;
    }

    private static List<OptiFineCatalogEntry> ParseOptiFineMirrorEntries(JsonArray root)
    {
        return root
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node =>
            {
                var minecraftVersion = node!["mcversion"]?.GetValue<string>() ?? "未知版本";
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
        string preferredMinecraftVersion)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadForge => LoadForgeCatalogSectionEntries(versionSourceIndex, lazyLoadToken),
            _ => throw new InvalidOperationException("当前页面不支持延迟加载目录。")
        };
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> LoadForgeCatalogSectionEntries(
        int versionSourceIndex,
        string minecraftVersion)
    {
        var payload = FetchForgeVersionEntries(versionSourceIndex, minecraftVersion);
        var orderedEntries = payload.Value
            .OrderByDescending(entry => entry.VersionName, VersionTextComparer.Instance)
            .ToArray();
        if (orderedEntries.Length == 0)
        {
            return EnsureEntries([], $"当前没有可用的 {minecraftVersion} Forge 条目。");
        }

        var latestEntry = orderedEntries[0];
        return orderedEntries
            .Select(entry => CreateForgeCatalogEntry(entry, ReferenceEquals(entry, latestEntry)))
            .ToArray();
    }

    private static RemotePayload<List<ForgeVersionCatalogEntry>> FetchForgeVersionEntries(
        int versionSourceIndex,
        string minecraftVersion)
    {
        var payload = FetchString(CreateForgeVersionSources(versionSourceIndex, minecraftVersion), versionSourceIndex);
        var entries = payload.Source.IsOfficial
            ? ParseForgeOfficialVersionEntries(payload.Value, minecraftVersion, payload.Source.IsOfficial)
            : ParseForgeMirrorVersionEntries(JsonNode.Parse(payload.Value)?.AsArray()
                                             ?? throw new InvalidOperationException("无法解析 Forge 镜像构建目录。"), minecraftVersion, payload.Source.IsOfficial);
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
                    : FormatReleaseTime(Regex.Match(block, "(?<=download-time\" title=\")[^\"]+").Value),
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
                FormatReleaseTime(node["modified"]?.GetValue<string>()),
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
        Func<T, FrontendDownloadCatalogEntry> entrySelector)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(groupKeySelector(item)))
            .GroupBy(groupKeySelector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupEntries = group.Select(entrySelector).ToArray();
                return new FrontendDownloadCatalogSection(
                    $"{groupTitleSelector(group)} ({groupEntries.Length})",
                    EnsureEntries(groupEntries, $"当前没有可用的 {group.Key} 条目。"),
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
            "保存安装器",
            targetUrl,
            FrontendDownloadCatalogEntryActionKind.DownloadFile,
            suggestedFileName);
    }

    private static FrontendDownloadCatalogEntry CreateOptiFineCatalogEntry(OptiFineCatalogEntry entry)
    {
        return CreateInstallerDownloadEntry(
            entry.DisplayName,
            BuildOptiFineInfo(entry),
            entry.TargetUrl,
            entry.SuggestedFileName);
    }

    private static FrontendDownloadCatalogEntry CreateForgeCatalogEntry(ForgeVersionCatalogEntry entry, bool isLatest)
    {
        var infoParts = new List<string>();
        if (entry.IsRecommended)
        {
            infoParts.Add("推荐版");
        }
        else if (isLatest)
        {
            infoParts.Add("最新版");
        }

        if (!string.IsNullOrWhiteSpace(entry.ReleaseTime))
        {
            infoParts.Add("发布于 " + entry.ReleaseTime);
        }

        return CreateInstallerDownloadEntry(
            entry.VersionName,
            string.Join("，", infoParts),
            entry.TargetUrl,
            entry.SuggestedFileName);
    }

    private static FrontendDownloadCatalogEntry CreateCleanroomCatalogEntry(JsonObject node)
    {
        var tag = node["tag_name"]?.GetValue<string>() ?? "未知版本";
        var installerAsset = FindGitHubAssetDownloadUrl(node, "-installer.jar");
        return CreateInstallerDownloadEntry(
            tag,
            IsPreReleaseTag(tag) ? "测试版" : "稳定版",
            installerAsset ?? $"https://github.com/CleanroomMC/Cleanroom/releases/download/{Uri.EscapeDataString(tag)}/cleanroom-{tag}-installer.jar",
            installerAsset is null ? $"cleanroom-{tag}-installer.jar" : Path.GetFileName(installerAsset));
    }

    private static FrontendDownloadCatalogEntry CreateFabricFamilyCatalogEntry(LauncherFrontendSubpageKey route, JsonObject node)
    {
        var version = node["version"]?.GetValue<string>() ?? "未知版本";
        var title = route == LauncherFrontendSubpageKey.DownloadFabric
            ? version.Replace("+build", string.Empty, StringComparison.Ordinal)
            : version;
        var info = route switch
        {
            LauncherFrontendSubpageKey.DownloadQuilt => "安装器",
            _ => node["stable"]?.GetValue<bool>() == true ? "稳定版" : "测试版"
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
            .OrderBy(entry => GetOptiFineSectionKey(entry) == "快照版本" ? 0 : 1)
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
        return GetOptiFineGroupVersion(entry) ?? "快照版本";
    }

    private static string? GetOptiFineGroupVersion(OptiFineCatalogEntry entry)
    {
        var segments = entry.MinecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return entry.MinecraftVersion.StartsWith("1.", StringComparison.OrdinalIgnoreCase) && segments.Length >= 2
            ? $"1.{segments[1]}"
            : null;
    }

    private static string BuildOptiFineInfo(OptiFineCatalogEntry entry)
    {
        var parts = new List<string>
        {
            entry.IsPreview ? "测试版" : "正式版"
        };
        if (!string.IsNullOrWhiteSpace(entry.ReleaseTime))
        {
            parts.Add("发布于 " + entry.ReleaseTime);
        }

        if (entry.RequiredForgeVersion is null)
        {
            parts.Add("不兼容 Forge");
        }
        else if (!string.IsNullOrWhiteSpace(entry.RequiredForgeVersion))
        {
            parts.Add("兼容 Forge " + entry.RequiredForgeVersion);
        }

        return string.Join("，", parts);
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
            : "未知版本";
    }

    private static string BuildLiteLoaderInfo(LiteLoaderCatalogEntry entry)
    {
        var info = entry.IsPreview ? "测试版" : "稳定版";
        if (entry.Timestamp > 0)
        {
            info += "，发布于 " + FormatUnixTime(entry.Timestamp);
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

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildFailureStates(string error)
    {
        return CatalogRoutes.ToDictionary(
            route => route,
            route => BuildFailureState(route, error));
    }

    private static FrontendDownloadCatalogState BuildFailureState(LauncherFrontendSubpageKey route, string error)
    {
        var descriptor = GetGoldCatalogDescriptor(route);
        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            $"读取远程目录失败：{error}",
            descriptor.LoadingText,
            descriptor.Actions,
            [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前无法读取远程目录，请稍后重试。"))]);
    }

    private static string ClassifyClientCategory(JsonObject version)
    {
        var type = version["type"]?.GetValue<string>() ?? string.Empty;
        var id = version["id"]?.GetValue<string>() ?? string.Empty;
        switch (type.ToLowerInvariant())
        {
            case "release":
                return "正式版";
            case "snapshot":
            case "pending":
                if (id.StartsWith("1.", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("combat", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("rc", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("experimental", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(id, "1.2", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("pre", StringComparison.OrdinalIgnoreCase))
                {
                    return "正式版";
                }

                return IsAprilFoolsVersion(id, version["releaseTime"]?.GetValue<string>()) ? "愚人节版" : "预览版";
            case "special":
                return "愚人节版";
            default:
                return "远古版";
        }
    }

    private static string BuildClientVersionInfo(JsonObject version)
    {
        var id = NormalizeClientVersionTitle(version["id"]?.GetValue<string>());
        var foolName = GetClientAprilFoolsName(id);
        if (!string.IsNullOrWhiteSpace(foolName))
        {
            return foolName;
        }

        return FormatReleaseTime(version["releaseTime"]?.GetValue<string>());
    }

    private static bool IsAprilFoolsVersion(string id, string? releaseTime)
    {
        if (!string.IsNullOrWhiteSpace(GetClientAprilFoolsName(id)))
        {
            return true;
        }

        var releaseMoment = ParseReleaseMoment(releaseTime);
        return releaseMoment != DateTimeOffset.MinValue
               && releaseMoment.UtcDateTime.AddHours(2).Month == 4
               && releaseMoment.UtcDateTime.AddHours(2).Day == 1;
    }

    private static string NormalizeClientVersionTitle(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "未知版本";
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

    private static string GetClientAprilFoolsName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.ToLowerInvariant();
        if (normalized.StartsWith("2.0", StringComparison.Ordinal) || normalized.StartsWith("2point0", StringComparison.Ordinal))
        {
            var tag = normalized.EndsWith("red", StringComparison.Ordinal) ? "（红色版本）"
                : normalized.EndsWith("blue", StringComparison.Ordinal) ? "（蓝色版本）"
                : normalized.EndsWith("purple", StringComparison.Ordinal) ? "（紫色版本）"
                : string.Empty;
            return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！" + tag;
        }

        return normalized switch
        {
            "15w14a" => "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。",
            "1.rv-pre1" => "2016 | 是时候将现代科技带入 Minecraft 了！",
            "3d shareware v1.34" => "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！",
            "20w14infinite" or "20w14∞" => "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！",
            "22w13oneblockatatime" => "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！",
            "23w13a_or_b" => "2023 | 研究表明：玩家喜欢作出选择，越多越好！",
            "24w14potato" => "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！",
            "25w14craftmine" => "2025 | 你可以合成任何东西，包括合成你的世界！",
            "26w14a" => "2026 | 为什么需要物品栏？让方块们跟着你走吧！",
            _ => string.Empty
        };
    }

    private static GoldCatalogDescriptor GetGoldCatalogDescriptor(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => new GoldCatalogDescriptor(string.Empty, string.Empty, "正在获取版本列表", []),
            LauncherFrontendSubpageKey.DownloadForge => new GoldCatalogDescriptor(
                "Forge 简介",
                "Forge 是一个 Mod 加载器，你需要先安装 Forge 才能安装各种 Forge 模组。",
                "正在获取 Forge 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://files.minecraftforge.net", true))),
            LauncherFrontendSubpageKey.DownloadNeoForge => new GoldCatalogDescriptor(
                "NeoForge 简介",
                "NeoForge 是 Minecraft 1.20.1+ 的 Mod 加载器，你需要先安装它才能安装各种 NeoForge 模组，它也兼容一些 Forge 模组。\n本页面提供 NeoForge 安装器下载，在下载后你需要手动打开安装器进行安装。",
                "正在获取 NeoForge 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://neoforged.net/", true))),
            LauncherFrontendSubpageKey.DownloadFabric => new GoldCatalogDescriptor(
                "Fabric 简介",
                "Fabric Loader 是新版 Minecraft 下的轻量化 Mod 加载器，你需要先安装它才能安装各种 Fabric 模组。\n本页面提供 Fabric 安装器下载，在下载后你需要手动打开安装器进行安装。",
                "正在获取 Fabric 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://www.fabricmc.net", true))),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => new GoldCatalogDescriptor(
                "Legacy Fabric 简介",
                "Legacy Fabric 是 Fabric 的旧版本移植，你需要先安装它才能安装各种 Legacy Fabric 模组。\n本页面提供 Legacy Fabric 安装器下载，在下载后你需要手动打开安装器进行安装。",
                "正在获取 Legacy Fabric 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://legacyfabric.net/", true))),
            LauncherFrontendSubpageKey.DownloadQuilt => new GoldCatalogDescriptor(
                "Quilt 简介",
                "Quilt Loader 是新版 Minecraft 下的轻量模块化 Mod 加载器，你需要先安装它才能安装各种 Quilt 模组。\n本页面提供 Quilt 安装器下载，在下载后你需要手动打开安装器进行安装。",
                "正在获取 Quilt 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://quiltmc.org", true))),
            LauncherFrontendSubpageKey.DownloadOptiFine => new GoldCatalogDescriptor(
                "OptiFine 简介",
                "OptiFine 又称为高清修复，以允许安装光影、使用高清材质、提高游戏性能，但与 Mod 的兼容性不佳。",
                "正在获取 OptiFine 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://www.optifine.net/", true))),
            LauncherFrontendSubpageKey.DownloadLiteLoader => new GoldCatalogDescriptor(
                "LiteLoader 简介",
                "与 Forge 类似，LiteLoader 可以用于加载老版本 Minecraft 中的 LiteLoader 模组。",
                "正在获取 LiteLoader 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://www.liteloader.com", true))),
            LauncherFrontendSubpageKey.DownloadLabyMod => new GoldCatalogDescriptor(
                "LabyMod 简介",
                "LabyMod 是 Minecraft 下的优化客户端。\n本页面提供 LabyMod 安装器下载，在下载后你需要手动打开安装器进行安装。",
                "正在获取 LabyMod 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://labymod.net", true))),
            LauncherFrontendSubpageKey.DownloadCleanroom => new GoldCatalogDescriptor(
                "Cleanroom 简介",
                "Cleanroom 是针对 1.12.2 基于 Forge 二次开发的 Mod 加载器，理论上与 99% 的 Forge Mod 兼容。",
                "正在获取 Cleanroom 列表",
                CreateActions(new FrontendDownloadCatalogAction("打开官网", "https://cleanroommc.com/zh/", true))),
            _ => new GoldCatalogDescriptor($"{GetRouteTitle(route)} 简介", string.Empty, $"正在获取 {GetRouteTitle(route)} 列表", [])
        };
    }

    private static IReadOnlyList<FrontendDownloadCatalogAction> CreateActions(params FrontendDownloadCatalogAction[] actions)
    {
        return actions.Where(action => !string.IsNullOrWhiteSpace(action.Target)).ToArray();
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> EnsureEntries(
        IReadOnlyList<FrontendDownloadCatalogEntry> entries,
        string emptyMessage)
    {
        return entries.Count > 0
            ? entries
            : [new FrontendDownloadCatalogEntry("暂无可显示数据", emptyMessage, string.Empty, "查看详情", null)];
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
                    ?? throw new InvalidOperationException($"无法解析 JSON 对象：{sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"无法读取远程目录：{sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
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
                    ?? throw new InvalidOperationException($"无法解析 JSON 数组：{sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"无法读取远程目录：{sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
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

        throw new InvalidOperationException($"无法读取远程目录：{sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static string FetchStringContent(RemoteSource source, TimeSpan timeout)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
        request.Headers.UserAgent.ParseAdd("PCL-CE-Frontend");
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
            new RemoteSource("Mojang 官方源", "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", false));
    }

    private static IReadOnlyList<RemoteSource> CreateOptiFineSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("OptiFine 官方源", "https://optifine.net/downloads", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/optifine/versionList", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeListSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge 官方源", "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/forge/minecraft", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeVersionSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = minecraftVersion.Replace("-", "_", StringComparison.Ordinal);
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge 官方源", $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{normalizedVersion}.html", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/forge/minecraft/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLatestSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge 官方源", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLegacySources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge 官方源", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricRootSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric 官方源", "https://meta.fabricmc.net/v2/versions", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric 官方源", $"https://meta.fabricmc.net/v2/versions/loader/{normalizedVersion}", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Legacy Fabric 官方源", "https://meta.legacyfabric.net/v2/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "1.12.2" : minecraftVersion;
        return
        [
            new RemoteSource("Legacy Fabric 官方源", $"https://meta.legacyfabric.net/v2/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Quilt 官方源", "https://meta.quiltmc.org/v3/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return
        [
            new RemoteSource("Quilt 官方源", $"https://meta.quiltmc.org/v3/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLiteLoaderSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("LiteLoader 官方源", "https://dl.liteloader.com/versions/versions.json", true),
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

    private static string FormatReleaseTime(string? value)
    {
        var moment = ParseReleaseMoment(value);
        return moment == DateTimeOffset.MinValue
            ? "未记录发布时间"
            : moment.LocalDateTime.ToString("yyyy/MM/dd HH:mm");
    }

    private static DateTimeOffset ParseReleaseMoment(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string FormatDdMmYyyy(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return string.IsNullOrWhiteSpace(value) ? "未记录发布时间" : value;
        }

        return $"{parts[2]}/{parts[1]}/{parts[0]}";
    }

    private static string FormatUnixTime(long seconds)
    {
        if (seconds <= 0)
        {
            return "未记录发布时间";
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

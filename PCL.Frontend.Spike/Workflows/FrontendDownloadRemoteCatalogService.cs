using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendDownloadRemoteCatalogService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly object CacheSync = new();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<RemoteCatalogCacheKey, RemoteCatalogCacheEntry> Cache = [];
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
                "Fabric",
                "https://fabricmc.net/",
                CreateFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadLegacyFabric] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                "Legacy Fabric",
                "https://legacyfabric.net/",
                CreateLegacyFabricRootSources(versionSourceIndex),
                minecraftVersion => CreateLegacyFabricLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadQuilt] = BuildFabricFamilyCatalogState(
                versionSourceIndex,
                preferredMinecraftVersion,
                "Quilt",
                "https://quiltmc.org/",
                CreateQuiltRootSources(versionSourceIndex),
                minecraftVersion => CreateQuiltLoaderSources(versionSourceIndex, minecraftVersion)),
            [LauncherFrontendSubpageKey.DownloadLiteLoader] = BuildLiteLoaderCatalogState(versionSourceIndex, preferredMinecraftVersion),
            [LauncherFrontendSubpageKey.DownloadLabyMod] = BuildLabyModCatalogState(preferredMinecraftVersion)
        };
    }

    private static FrontendDownloadCatalogState BuildClientCatalogState(int versionSourceIndex)
    {
        var payload = FetchJsonObject(CreateClientSources(versionSourceIndex), versionSourceIndex);
        var versions = payload.Value["versions"] as JsonArray
                       ?? throw new InvalidOperationException("Minecraft 版本清单缺少 versions 字段。");

        var releaseEntries = versions
            .Select(node => node as JsonObject)
            .Where(node => string.Equals(node?["type"]?.GetValue<string>(), "release", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(node => ParseReleaseMoment(node?["releaseTime"]?.GetValue<string>()))
            .Take(12)
            .Select(node => CreateClientCatalogEntry(node!, payload.Source.DisplayName))
            .ToArray();

        var previewEntries = versions
            .Select(node => node as JsonObject)
            .Where(node =>
            {
                var type = node?["type"]?.GetValue<string>();
                return string.Equals(type, "snapshot", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(type, "pending", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(node => ParseReleaseMoment(node?["releaseTime"]?.GetValue<string>()))
            .Take(12)
            .Select(node => CreateClientCatalogEntry(node!, payload.Source.DisplayName))
            .ToArray();

        var latest = new List<FrontendDownloadCatalogEntry>();
        if (payload.Value["latest"] is JsonObject latestNode)
        {
            AddLatestClientEntry(latest, versions, latestNode["release"]?.GetValue<string>(), "最新正式版", payload.Source.DisplayName);
            AddLatestClientEntry(latest, versions, latestNode["snapshot"]?.GetValue<string>(), "最新快照版", payload.Source.DisplayName);
        }

        return new FrontendDownloadCatalogState(
            "Minecraft 版本目录",
            $"当前目录直接来自 {payload.Source.DisplayName}，页面显示会随着远程版本清单变动而更新。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://www.minecraft.net/zh-hans", true),
                new FrontendDownloadCatalogAction("查看原始清单", payload.Source.Url, false)),
            [
                new FrontendDownloadCatalogSection("最新版本", EnsureEntries(latest, "当前远程版本清单没有返回最新版本。")),
                new FrontendDownloadCatalogSection("正式版", EnsureEntries(releaseEntries, "当前远程版本清单没有返回正式版条目。")),
                new FrontendDownloadCatalogSection("快照与预览", EnsureEntries(previewEntries, "当前远程版本清单没有返回快照或预览条目。"))
            ]);
    }

    private static void AddLatestClientEntry(
        ICollection<FrontendDownloadCatalogEntry> latest,
        JsonArray versions,
        string? versionId,
        string label,
        string sourceName)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return;
        }

        var node = versions
            .Select(item => item as JsonObject)
            .FirstOrDefault(item => string.Equals(item?["id"]?.GetValue<string>(), versionId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return;
        }

        latest.Add(new FrontendDownloadCatalogEntry(
            node["id"]?.GetValue<string>() ?? versionId,
            label,
            $"{FormatClientType(node["type"]?.GetValue<string>())} • {FormatReleaseTime(node["releaseTime"]?.GetValue<string>())} • {sourceName}",
            "查看清单",
            node["url"]?.GetValue<string>()));
    }

    private static FrontendDownloadCatalogEntry CreateClientCatalogEntry(JsonObject node, string sourceName)
    {
        return new FrontendDownloadCatalogEntry(
            node["id"]?.GetValue<string>() ?? "未知版本",
            FormatReleaseTime(node["releaseTime"]?.GetValue<string>()),
            $"{FormatClientType(node["type"]?.GetValue<string>())} • {sourceName}",
            "查看清单",
            node["url"]?.GetValue<string>());
    }

    private static FrontendDownloadCatalogState BuildOptiFineCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var payload = FetchOptiFineEntries(versionSourceIndex);
        var groupedSections = BuildGroupedSections(
            payload.Value,
            entry => entry.MinecraftVersion,
            preferredMinecraftVersion,
            4,
            8,
            entry => new FrontendDownloadCatalogEntry(
                entry.Title,
                entry.IsPreview ? "预览版" : "稳定版",
                $"{entry.MinecraftVersion} • {entry.SourceSummary}",
                "打开目录",
                entry.TargetUrl));

        return new FrontendDownloadCatalogState(
            "OptiFine 远程目录",
            $"当前条目直接来自 {payload.Source.DisplayName}，不再回退到本地版本清单。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://optifine.net/", true),
                new FrontendDownloadCatalogAction("查看原始目录", payload.Source.Url, false)),
            groupedSections.Count > 0
                ? groupedSections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 OptiFine 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var sources = CreateForgeListSources(versionSourceIndex);
        var payload = FetchString(sources, versionSourceIndex);
        var minecraftVersions = payload.Source.IsOfficial
            ? ParseForgeMinecraftVersionsFromHtml(payload.Value)
            : ParseForgeMinecraftVersionsFromPlainText(payload.Value);

        if (minecraftVersions.Count == 0)
        {
            throw new InvalidOperationException("Forge 远程目录没有返回任何 Minecraft 版本。");
        }

        var activeVersion = ResolvePreferredForgeMinecraftVersion(preferredMinecraftVersion, minecraftVersions);
        var buildEntries = TryBuildForgeVersionEntries(versionSourceIndex, activeVersion);
        var versionEntries = minecraftVersions
            .Take(18)
            .Select(version => new FrontendDownloadCatalogEntry(
                version.Replace("_pre", " pre", StringComparison.Ordinal),
                string.Equals(version, activeVersion, StringComparison.OrdinalIgnoreCase) ? "当前目标版本" : "支持的 Minecraft 版本",
                payload.Source.DisplayName,
                "打开目录",
                BuildForgeVersionCatalogUrl(payload.Source.IsOfficial, version)))
            .ToArray();

        return new FrontendDownloadCatalogState(
            "Forge 远程目录",
            $"Minecraft 版本目录来自 {payload.Source.DisplayName}，并会优先补充当前目标 {activeVersion} 的实际 Forge 构建。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://files.minecraftforge.net/", true),
                new FrontendDownloadCatalogAction("查看原始目录", payload.Source.Url, false)),
            [
                new FrontendDownloadCatalogSection(
                    $"{activeVersion} 可用构建",
                    EnsureEntries(buildEntries, $"当前没有读取到 {activeVersion} 的 Forge 构建。")),
                new FrontendDownloadCatalogSection(
                    "支持的 Minecraft 版本",
                    EnsureEntries(versionEntries, "当前 Forge 远程目录没有返回任何 Minecraft 版本。"))
            ]);
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> TryBuildForgeVersionEntries(
        int versionSourceIndex,
        string minecraftVersion)
    {
        try
        {
            var payload = FetchForgeVersionEntries(versionSourceIndex, minecraftVersion);
            return payload.Value
                .Take(12)
                .Select(entry => new FrontendDownloadCatalogEntry(
                    entry.Title,
                    entry.Info,
                    $"{entry.Meta} • {payload.Source.DisplayName}",
                    "打开目录",
                    entry.Target))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static FrontendDownloadCatalogState BuildNeoForgeCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
        var payload = FetchNeoForgeEntries(versionSourceIndex);
        var sections = BuildGroupedSections(
            payload.Value,
            entry => entry.MinecraftVersion,
            preferredMinecraftVersion,
            4,
            8,
            entry => new FrontendDownloadCatalogEntry(
                entry.Title,
                entry.IsPreview ? "测试构建" : "稳定构建",
                $"{entry.SourceSummary}",
                "查看安装器",
                entry.TargetUrl));

        return new FrontendDownloadCatalogState(
            "NeoForge 远程目录",
            $"页面现在直接读取 {payload.Source.DisplayName} 的 NeoForge 发行目录。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://neoforged.net/", true),
                new FrontendDownloadCatalogAction("查看原始目录", payload.Source.Url, false)),
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 NeoForge 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildCleanroomCatalogState(string preferredMinecraftVersion)
    {
        var payload = FetchJsonArray(
            [new RemoteSource("Cleanroom GitHub Releases", "https://api.github.com/repos/CleanroomMC/Cleanroom/releases", true)],
            1);
        var entries = payload.Value
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["tag_name"]?.GetValue<string>()))
            .Select(node =>
            {
                var tag = node!["tag_name"]!.GetValue<string>();
                return new FrontendDownloadCatalogEntry(
                    tag,
                    string.Equals(preferredMinecraftVersion, "1.12.2", StringComparison.OrdinalIgnoreCase) ? "适配当前目标 1.12.2" : "固定适配 1.12.2",
                    $"{(tag.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? "测试构建" : "稳定构建")} • GitHub Releases",
                    "查看发布页",
                    node["html_url"]?.GetValue<string>() ?? $"https://github.com/CleanroomMC/Cleanroom/releases/tag/{Uri.EscapeDataString(tag)}");
            })
            .Take(18)
            .ToArray();

        return new FrontendDownloadCatalogState(
            "Cleanroom 远程目录",
            "当前页面直接读取 Cleanroom 的 GitHub Releases，而不是本地识别结果。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://github.com/CleanroomMC/Cleanroom", true),
                new FrontendDownloadCatalogAction("查看原始目录", payload.Source.Url, false)),
            [new FrontendDownloadCatalogSection("1.12.2 构建", EnsureEntries(entries, "当前没有可用的 Cleanroom 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildFabricFamilyCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion,
        string title,
        string officialSite,
        IReadOnlyList<RemoteSource> rootSources,
        Func<string, IReadOnlyList<RemoteSource>> loaderSourceFactory)
    {
        var rootPayload = FetchJsonObject(rootSources, versionSourceIndex);
        var sections = new List<FrontendDownloadCatalogSection>();
        var effectivePreferredVersion = ResolvePreferredGameVersion(preferredMinecraftVersion, rootPayload.Value["game"] as JsonArray);

        if (!string.IsNullOrWhiteSpace(effectivePreferredVersion))
        {
            try
            {
                var loaderSources = loaderSourceFactory(effectivePreferredVersion);
                var loaderPayload = FetchJsonArray(loaderSources, versionSourceIndex);
                var loaderEntries = loaderPayload.Value
                    .Select(node => node as JsonObject)
                    .Where(node => node?["loader"] is JsonObject)
                    .Select(node =>
                    {
                        var loader = (JsonObject)node!["loader"]!;
                        var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                        return new FrontendDownloadCatalogEntry(
                            version,
                            loader["stable"]?.GetValue<bool>() == true ? "稳定构建" : "测试构建",
                            $"{effectivePreferredVersion} • {loaderPayload.Source.DisplayName}",
                            "查看清单",
                            loaderSources.First().Url.TrimEnd('/') + "/" + Uri.EscapeDataString(version) + "/profile/json");
                    })
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
                    .Take(12)
                    .ToArray();

                sections.Add(new FrontendDownloadCatalogSection(
                    $"{effectivePreferredVersion} 可用加载器",
                    EnsureEntries(loaderEntries, $"当前没有读取到 {effectivePreferredVersion} 的 {title} 加载器条目。")));
            }
            catch
            {
                // Keep the root catalog usable even if the version-specific loader endpoint is temporarily unavailable.
            }
        }

        var installerEntries = (rootPayload.Value["installer"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["version"]?.GetValue<string>()))
            .Take(12)
            .Select(node => new FrontendDownloadCatalogEntry(
                node!["version"]!.GetValue<string>(),
                node["stable"]?.GetValue<bool>() == true ? "稳定安装器" : "测试安装器",
                rootPayload.Source.DisplayName,
                "查看目录",
                rootPayload.Source.Url))
            .ToArray() ?? [];
        sections.Add(new FrontendDownloadCatalogSection("安装器版本", EnsureEntries(installerEntries, "当前没有可用的安装器版本。")));

        var gameEntries = (rootPayload.Value["game"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["version"]?.GetValue<string>()))
            .Take(16)
            .Select(node => new FrontendDownloadCatalogEntry(
                node!["version"]!.GetValue<string>(),
                node["stable"]?.GetValue<bool>() == true ? "稳定游戏版本" : "测试游戏版本",
                rootPayload.Source.DisplayName,
                "查看目录",
                rootPayload.Source.Url))
            .ToArray() ?? [];
        sections.Add(new FrontendDownloadCatalogSection("支持的游戏版本", EnsureEntries(gameEntries, "当前没有可用的游戏版本条目。")));

        return new FrontendDownloadCatalogState(
            $"{title} 远程目录",
            $"当前页面直接读取 {rootPayload.Source.DisplayName} 的 {title} 目录数据。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", officialSite, true),
                new FrontendDownloadCatalogAction("查看原始目录", rootPayload.Source.Url, false)),
            sections);
    }

    private static FrontendDownloadCatalogState BuildLiteLoaderCatalogState(
        int versionSourceIndex,
        string preferredMinecraftVersion)
    {
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

            var source = versionObject["artefacts"] as JsonObject ?? versionObject["snapshots"] as JsonObject;
            var latest = source?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
            if (latest is null)
            {
                continue;
            }

            var version = latest["version"]?.GetValue<string>() ?? property.Key;
            var isPreview = string.Equals(latest["stream"]?.GetValue<string>(), "SNAPSHOT", StringComparison.OrdinalIgnoreCase);
            var timestamp = ReadInt64(latest["timestamp"]);
            entries.Add(new LiteLoaderCatalogEntry(
                property.Key,
                version,
                isPreview,
                timestamp,
                $"{payload.Source.DisplayName}",
                payload.Source.Url));
        }

        var sections = BuildGroupedSections(
            entries.OrderByDescending(entry => entry.Timestamp),
            entry => entry.MinecraftVersion,
            preferredMinecraftVersion,
            4,
            4,
            entry => new FrontendDownloadCatalogEntry(
                entry.Title,
                entry.IsPreview ? "测试构建" : "稳定构建",
                $"{FormatUnixTime(entry.Timestamp)} • {entry.SourceSummary}",
                "查看目录",
                entry.TargetUrl));

        return new FrontendDownloadCatalogState(
            "LiteLoader 远程目录",
            $"当前页面直接读取 {payload.Source.DisplayName} 的 LiteLoader 版本目录。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://www.liteloader.com/", true),
                new FrontendDownloadCatalogAction("查看原始目录", payload.Source.Url, false)),
            sections.Count > 0
                ? sections
                : [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前没有可用的 LiteLoader 远程条目。"))]);
    }

    private static FrontendDownloadCatalogState BuildLabyModCatalogState(string preferredMinecraftVersion)
    {
        var production = FetchJsonObject(
            [new RemoteSource("LabyMod Production", "https://releases.r2.labymod.net/api/v1/manifest/production/latest.json", true)],
            1);
        var snapshot = FetchJsonObject(
            [new RemoteSource("LabyMod Snapshot", "https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json", true)],
            1);

        var channelEntries = new[]
        {
            CreateLabyModEntry("稳定版", production.Value, preferredMinecraftVersion, production.Source.Url),
            CreateLabyModEntry("快照版", snapshot.Value, preferredMinecraftVersion, snapshot.Source.Url)
        };

        var supportedVersions = new List<FrontendDownloadCatalogEntry>();
        AddSupportedLabyVersions(supportedVersions, production.Value, "稳定版");
        AddSupportedLabyVersions(supportedVersions, snapshot.Value, "快照版");

        return new FrontendDownloadCatalogState(
            "LabyMod 远程目录",
            "当前页面直接读取 LabyMod 官方 manifest，而不是本地实例识别结果。",
            CreateActions(
                new FrontendDownloadCatalogAction("打开官网", "https://www.labymod.net/", true),
                new FrontendDownloadCatalogAction("查看稳定版清单", production.Source.Url, false),
                new FrontendDownloadCatalogAction("查看快照版清单", snapshot.Source.Url, false)),
            [
                new FrontendDownloadCatalogSection("可用频道", EnsureEntries(channelEntries, "当前没有可用的 LabyMod 频道。")),
                new FrontendDownloadCatalogSection("支持的 Minecraft 版本", EnsureEntries(supportedVersions.Take(18).ToArray(), "当前没有返回可支持的 Minecraft 版本。"))
            ]);
    }

    private static FrontendDownloadCatalogEntry CreateLabyModEntry(
        string channelLabel,
        JsonObject manifest,
        string preferredMinecraftVersion,
        string targetUrl)
    {
        var version = manifest["labyModVersion"]?.GetValue<string>() ?? "未知版本";
        var supportsPreferred = (manifest["minecraftVersions"] as JsonArray)?
            .Select(node => node as JsonObject)
            .Any(node => string.Equals(node?["version"]?.GetValue<string>(), preferredMinecraftVersion, StringComparison.OrdinalIgnoreCase))
            == true;
        var releaseTime = ReadInt64(manifest["releaseTime"]);

        return new FrontendDownloadCatalogEntry(
            $"{version} {channelLabel}",
            supportsPreferred
                ? $"支持当前目标 {preferredMinecraftVersion}"
                : string.IsNullOrWhiteSpace(preferredMinecraftVersion)
                    ? "查看该频道的官方 manifest"
                    : $"当前目标 {preferredMinecraftVersion} 不在该频道支持列表中",
            releaseTime > 0 ? $"{FormatUnixMilliseconds(releaseTime)} • LabyMod 4" : "LabyMod 4",
            "查看清单",
            targetUrl);
    }

    private static void AddSupportedLabyVersions(
        ICollection<FrontendDownloadCatalogEntry> entries,
        JsonObject manifest,
        string channelLabel)
    {
        if (manifest["minecraftVersions"] is not JsonArray versions)
        {
            return;
        }

        foreach (var version in versions.Select(node => node as JsonObject).Where(node => node is not null))
        {
            var minecraftVersion = version!["version"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(minecraftVersion))
            {
                continue;
            }

            entries.Add(new FrontendDownloadCatalogEntry(
                minecraftVersion,
                $"{channelLabel} 支持版本",
                manifest["labyModVersion"]?.GetValue<string>() ?? "LabyMod 4",
                "查看清单",
                version["customManifestUrl"]?.GetValue<string>()));
        }
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
            var name = nameMatches[index].Value.Replace('_', ' ');
            var minecraftVersion = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "未知版本";
            var shortName = name.Replace(minecraftVersion + " ", string.Empty, StringComparison.Ordinal);
            var requiredForge = forgeMatches[index].Value
                .Replace("Forge ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("#", string.Empty, StringComparison.Ordinal);
            if (requiredForge.Contains("N/A", StringComparison.OrdinalIgnoreCase))
            {
                requiredForge = string.Empty;
            }

            entries.Add(new OptiFineCatalogEntry(
                minecraftVersion,
                shortName.Replace("HD U ", string.Empty, StringComparison.Ordinal).Replace(".0 ", " ", StringComparison.Ordinal),
                shortName.Contains("pre", StringComparison.OrdinalIgnoreCase),
                FormatDdMmYyyy(forgeMatches[index].Value.Length >= 0 ? dateMatches[index].Value : string.Empty),
                string.IsNullOrWhiteSpace(requiredForge) ? "官方源" : $"需要 Forge {requiredForge}",
                "https://optifine.net/downloads"));
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
                var shortName = (type.Replace("HD_U", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal) + " " + patch).Trim();
                var bmclVersion = minecraftVersion is "1.8" or "1.9" ? minecraftVersion + ".0" : minecraftVersion;
                var displayShortName = shortName.Replace(".0 ", " ", StringComparison.Ordinal).Trim();
                var targetUrl = patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    ? "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U_" + displayShortName.Replace(" ", "/", StringComparison.Ordinal)
                    : "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U/" + displayShortName;
                var forge = node["forge"]?.GetValue<string>()
                    ?.Replace("Forge ", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("#", string.Empty, StringComparison.Ordinal);
                if (forge?.Contains("N/A", StringComparison.OrdinalIgnoreCase) == true)
                {
                    forge = string.Empty;
                }

                return new OptiFineCatalogEntry(
                    minecraftVersion,
                    displayShortName,
                    patch.Contains("pre", StringComparison.OrdinalIgnoreCase),
                    string.Empty,
                    string.IsNullOrWhiteSpace(forge) ? "BMCLAPI" : $"需要 Forge {forge}",
                    targetUrl);
            })
            .ToList();
    }

    private static RemotePayload<List<FrontendDownloadCatalogEntry>> FetchForgeVersionEntries(
        int versionSourceIndex,
        string minecraftVersion)
    {
        var payload = FetchString(CreateForgeVersionSources(versionSourceIndex, minecraftVersion), versionSourceIndex);
        var entries = payload.Source.IsOfficial
            ? ParseForgeOfficialVersionEntries(payload.Value, minecraftVersion)
            : ParseForgeMirrorVersionEntries(JsonNode.Parse(payload.Value)?.AsArray()
                                             ?? throw new InvalidOperationException("无法解析 Forge 镜像构建目录。"), minecraftVersion);
        return new RemotePayload<List<FrontendDownloadCatalogEntry>>(payload.Source, entries);
    }

    private static List<FrontendDownloadCatalogEntry> ParseForgeOfficialVersionEntries(string html, string minecraftVersion)
    {
        var blocks = html.Split("<td class=\"download-version", StringSplitOptions.RemoveEmptyEntries);
        var entries = new List<FrontendDownloadCatalogEntry>();
        foreach (var block in blocks.Skip(1))
        {
            var versionName = Regex.Match(block, "(?<=[^(0-9)]+)[0-9.]+").Value;
            if (string.IsNullOrWhiteSpace(versionName))
            {
                continue;
            }

            var releaseTime = Regex.Match(block, "(?<=download-time\" title=\")[^\"]+").Value;
            var info = string.IsNullOrWhiteSpace(releaseTime) ? "官方构建" : FormatReleaseTime(releaseTime);
            var category = block.Contains("classifier-installer\"", StringComparison.OrdinalIgnoreCase)
                ? "installer"
                : block.Contains("classifier-universal\"", StringComparison.OrdinalIgnoreCase)
                    ? "universal"
                    : block.Contains("client.zip", StringComparison.OrdinalIgnoreCase)
                        ? "client"
                        : "构建";
            var isRecommended = block.Contains("promo-recommended", StringComparison.OrdinalIgnoreCase);

            entries.Add(new FrontendDownloadCatalogEntry(
                isRecommended ? $"{versionName} (推荐)" : versionName,
                info,
                category,
                "打开目录",
                BuildForgeVersionCatalogUrl(true, minecraftVersion)));
        }

        return entries
            .OrderByDescending(entry => entry.Title, VersionTextComparer.Instance)
            .Take(12)
            .ToList();
    }

    private static List<FrontendDownloadCatalogEntry> ParseForgeMirrorVersionEntries(JsonArray root, string minecraftVersion)
    {
        return root
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["version"]?.GetValue<string>()))
            .Select(node =>
            {
                var recommended = node!["branch"]?.GetValue<string>();
                return new FrontendDownloadCatalogEntry(
                    node["version"]?.GetValue<string>() ?? "未知构建",
                    FormatReleaseTime(node["modified"]?.GetValue<string>()),
                    string.IsNullOrWhiteSpace(recommended) ? "BMCLAPI" : $"分支 {recommended}",
                    "查看目录",
                    BuildForgeVersionCatalogUrl(false, minecraftVersion));
            })
            .OrderByDescending(entry => entry.Title, VersionTextComparer.Instance)
            .Take(12)
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

        var sourceName = latestPayload.Source.DisplayName;
        var baseUrl = latestPayload.Source.IsOfficial
            ? "https://maven.neoforged.net/releases/net/neoforged"
            : "https://bmclapi2.bangbang93.com/maven/net/neoforged";
        var entries = versions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(version => !string.Equals(version, "47.1.82", StringComparison.OrdinalIgnoreCase))
            .Select(version => CreateNeoForgeCatalogEntry(version, sourceName, baseUrl))
            .OrderByDescending(entry => entry.MinecraftVersion, VersionTextComparer.Instance)
            .ThenByDescending(entry => entry.Title, VersionTextComparer.Instance)
            .ToList();

        return new RemotePayload<List<NeoForgeCatalogEntry>>(latestPayload.Source, entries);
    }

    private static NeoForgeCatalogEntry CreateNeoForgeCatalogEntry(string apiName, string sourceName, string baseUrl)
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
            $"{minecraftVersion} • {sourceName}",
            $"{baseUrl}/{packageName}/{apiName}/{packageName}-{apiName}-installer.jar");
    }

    private static IReadOnlyList<FrontendDownloadCatalogSection> BuildGroupedSections<T>(
        IEnumerable<T> items,
        Func<T, string> groupKeySelector,
        string preferredKey,
        int maxGroups,
        int maxEntriesPerGroup,
        Func<T, FrontendDownloadCatalogEntry> entrySelector)
    {
        var grouped = items
            .Where(item => !string.IsNullOrWhiteSpace(groupKeySelector(item)))
            .GroupBy(groupKeySelector, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Key, VersionTextComparer.Instance)
            .ToList();
        var sections = new List<FrontendDownloadCatalogSection>();

        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            var preferredGroup = grouped.FirstOrDefault(group => string.Equals(group.Key, preferredKey, StringComparison.OrdinalIgnoreCase));
            if (preferredGroup is not null)
            {
                sections.Add(new FrontendDownloadCatalogSection(
                    $"{preferredGroup.Key} 当前目标",
                    EnsureEntries(preferredGroup.Take(maxEntriesPerGroup).Select(entrySelector).ToArray(), $"当前没有可用的 {preferredGroup.Key} 条目。")));
                grouped.Remove(preferredGroup);
            }
        }

        foreach (var group in grouped.Take(maxGroups))
        {
            sections.Add(new FrontendDownloadCatalogSection(
                group.Key,
                EnsureEntries(group.Take(maxEntriesPerGroup).Select(entrySelector).ToArray(), $"当前没有可用的 {group.Key} 条目。")));
        }

        return sections;
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildFailureStates(string error)
    {
        return CatalogRoutes.ToDictionary(
            route => route,
            route => new FrontendDownloadCatalogState(
                $"{GetRouteTitle(route)} 远程目录",
                $"读取远程目录失败：{error}",
                [],
                [new FrontendDownloadCatalogSection("远程目录", EnsureEntries([], "当前无法读取远程目录，请稍后重试。"))]));
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

    private static string ResolvePreferredGameVersion(string preferredMinecraftVersion, JsonArray? gameArray)
    {
        if (!string.IsNullOrWhiteSpace(preferredMinecraftVersion))
        {
            return preferredMinecraftVersion;
        }

        return gameArray?
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => node?["stable"]?.GetValue<bool>() == true)?["version"]?.GetValue<string>()
            ?? string.Empty;
    }

    private static string ResolvePreferredForgeMinecraftVersion(string preferredMinecraftVersion, IReadOnlyList<string> versions)
    {
        if (!string.IsNullOrWhiteSpace(preferredMinecraftVersion))
        {
            var exactMatch = versions.FirstOrDefault(version => string.Equals(version, preferredMinecraftVersion, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }

            var normalized = preferredMinecraftVersion.Replace("-", "_", StringComparison.Ordinal);
            exactMatch = versions.FirstOrDefault(version => string.Equals(version, normalized, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }
        }

        return versions[0];
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

    private static string BuildForgeVersionCatalogUrl(bool isOfficial, string minecraftVersion)
    {
        var normalizedVersion = minecraftVersion.Replace("-", "_", StringComparison.Ordinal);
        return isOfficial
            ? $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{normalizedVersion}.html"
            : $"https://bmclapi2.bangbang93.com/forge/minecraft/{normalizedVersion}";
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

    private static string FormatClientType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "release" => "正式版",
            "snapshot" => "快照版",
            "pending" => "预览版",
            "special" => "特别版",
            null or "" => "未知类型",
            _ => type
        };
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

    private static string FormatUnixMilliseconds(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "未记录发布时间";
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
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

    private sealed record RemoteCatalogCacheEntry(
        DateTimeOffset FetchedAtUtc,
        IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> States);

    private sealed record RemoteSource(string DisplayName, string Url, bool IsOfficial);

    private sealed record RemotePayload<T>(RemoteSource Source, T Value);

    private sealed record OptiFineCatalogEntry(
        string MinecraftVersion,
        string Title,
        bool IsPreview,
        string ReleaseTime,
        string SourceSummary,
        string TargetUrl);

    private sealed record NeoForgeCatalogEntry(
        string MinecraftVersion,
        string Title,
        bool IsPreview,
        string SourceSummary,
        string TargetUrl);

    private sealed record LiteLoaderCatalogEntry(
        string MinecraftVersion,
        string Title,
        bool IsPreview,
        long Timestamp,
        string SourceSummary,
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

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendCommunityResourceCatalogService
{
    private const int SearchPageSize = 12;
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);
    private static readonly IReadOnlyList<RouteConfig> RouteConfigs =
    [
        new RouteConfig(LauncherFrontendSubpageKey.DownloadMod, "Mod", "CommandBlock.png", false, "mod", null, 6, "mc-mods"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadPack, "整合包", "CommandBlock.png", false, "modpack", null, 4471, "modpacks"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadDataPack, "数据包", "RedstoneLampOn.png", false, "mod", "datapack", 6945, "data-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadResourcePack, "资源包", "Grass.png", false, "resourcepack", null, 12, "texture-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadShader, "光影包", "GoldBlock.png", true, "shader", null, 6552, "shaders"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadWorld, "世界", "GrassPath.png", false, null, null, 17, "worlds")
    ];

    public static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState> BuildResourceStates(
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference)
    {
        var preferredVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var routeStates = new Dictionary<LauncherFrontendSubpageKey, FrontendDownloadResourceState>();

        foreach (var config in RouteConfigs)
        {
            routeStates[config.Route] = GetOrCreateState(config, preferredVersion, communitySourcePreference);
        }

        return routeStates;
    }

    private static FrontendDownloadResourceState GetOrCreateState(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference)
    {
        var cacheKey = $"{config.Route}|{preferredVersion ?? "*"}|{communitySourcePreference}";
        if (Cache.TryGetValue(cacheKey, out var cacheEntry)
            && DateTimeOffset.UtcNow - cacheEntry.CreatedAt < TimeSpan.FromMinutes(10))
        {
            return cacheEntry.State;
        }

        var state = BuildState(config, preferredVersion, communitySourcePreference);
        Cache[cacheKey] = new CacheEntry(state, DateTimeOffset.UtcNow);
        return state;
    }

    private static FrontendDownloadResourceState BuildState(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference)
    {
        var versionAwareResult = FetchEntries(config, preferredVersion, communitySourcePreference);
        var usedVersionFallback = false;

        if (versionAwareResult.Entries.Count == 0 && !string.IsNullOrWhiteSpace(preferredVersion))
        {
            versionAwareResult = FetchEntries(config, null, communitySourcePreference);
            usedVersionFallback = true;
        }

        var entries = versionAwareResult.Entries
            .OrderByDescending(entry => entry.DownloadCount)
            .ThenByDescending(entry => entry.FollowCount)
            .ThenByDescending(entry => entry.UpdateRank)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(SearchPageSize * 2)
            .ToArray();

        return new FrontendDownloadResourceState(
            $"{config.Title} 列表",
            config.ModrinthProjectType is not null && config.CurseForgeClassId is not null,
            false,
            config.UseShaderLoaderOptions,
            BuildHintText(config, preferredVersion, versionAwareResult.SourceErrors, entries.Length > 0, usedVersionFallback),
            BuildTagOptions(entries),
            entries);
    }

    private static FetchResult FetchEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference)
    {
        var tasks = new List<Task<SourceFetchResult>>(2);

        if (!string.IsNullOrWhiteSpace(config.ModrinthProjectType))
        {
            tasks.Add(Task.Run(() => FetchModrinthEntries(config, preferredVersion, communitySourcePreference)));
        }

        if (config.CurseForgeClassId is not null)
        {
            tasks.Add(Task.Run(() => FetchCurseForgeEntries(config, preferredVersion, communitySourcePreference)));
        }

        Task.WaitAll(tasks.ToArray());

        var entries = tasks
            .Select(task => task.Result)
            .SelectMany(result => result.Entries)
            .ToArray();
        var errors = tasks
            .Select(task => task.Result)
            .Where(result => !string.IsNullOrWhiteSpace(result.ErrorMessage))
            .Select(result => result.ErrorMessage!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new FetchResult(entries, errors);
    }

    private static SourceFetchResult FetchModrinthEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference)
    {
        try
        {
            var officialUrl = BuildModrinthSearchUrl(config, preferredVersion, useMirror: false);
            var mirrorUrl = BuildModrinthSearchUrl(config, preferredVersion, useMirror: true);
            var response = ReadJsonObject("Modrinth", officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey: false);
            var hits = response["hits"]?.AsArray() ?? [];

            var entries = hits
                .Select(hit => hit as JsonObject)
                .Where(hit => hit is not null)
                .Select(hit => BuildModrinthEntry(config, hit!, preferredVersion))
                .Where(entry => entry is not null)
                .Cast<FrontendDownloadResourceEntry>()
                .ToArray();

            return new SourceFetchResult(entries, null);
        }
        catch (Exception ex)
        {
            return new SourceFetchResult([], $"Modrinth 暂时不可用：{ex.Message}");
        }
    }

    private static SourceFetchResult FetchCurseForgeEntries(
        RouteConfig config,
        string? preferredVersion,
        int communitySourcePreference)
    {
        try
        {
            var officialUrl = BuildCurseForgeSearchUrl(config, preferredVersion, useMirror: false);
            var mirrorUrl = BuildCurseForgeSearchUrl(config, preferredVersion, useMirror: true);
            var response = ReadJsonObject("CurseForge", officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey: true);
            var data = response["data"]?.AsArray() ?? [];

            var entries = data
                .Select(item => item as JsonObject)
                .Where(item => item is not null)
                .Select(item => BuildCurseForgeEntry(config, item!, preferredVersion))
                .Where(entry => entry is not null)
                .Cast<FrontendDownloadResourceEntry>()
                .ToArray();

            return new SourceFetchResult(entries, null);
        }
        catch (Exception ex)
        {
            return new SourceFetchResult([], $"CurseForge 暂时不可用：{ex.Message}");
        }
    }

    private static FrontendDownloadResourceEntry? BuildModrinthEntry(
        RouteConfig config,
        JsonObject hit,
        string? preferredVersion)
    {
        var title = GetString(hit, "title");
        var slug = GetString(hit, "slug");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var projectType = GetString(hit, "project_type");
        var rawCategories = (hit["display_categories"] as JsonArray ?? hit["categories"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var translatedTags = TranslateModrinthCategories(rawCategories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var versions = (hit["versions"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        var website = $"https://modrinth.com/{projectType}/{slug}";
        var downloads = GetInt(hit, "downloads");
        var follows = GetInt(hit, "follows");
        var createdAt = ParseDateTimeOffset(hit["date_created"]);
        var updatedAt = ParseDateTimeOffset(hit["date_modified"]);
        var summary = BuildEntryInfo(
            GetString(hit, "description"),
            GetString(hit, "author"),
            updatedAt,
            downloads);

        return new FrontendDownloadResourceEntry(
            title,
            summary,
            "Modrinth",
            ResolvePrimaryVersion(versions, preferredVersion),
            ResolvePrimaryLoader(rawCategories, config.UseShaderLoaderOptions),
            translatedTags.Length == 0 ? [config.Title] : translatedTags,
            "打开页面",
            config.IconName,
            website,
            downloads,
            follows,
            ToRank(createdAt),
            ToRank(updatedAt));
    }

    private static FrontendDownloadResourceEntry? BuildCurseForgeEntry(
        RouteConfig config,
        JsonObject item,
        string? preferredVersion)
    {
        var title = GetString(item, "name");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var categories = item["categories"] as JsonArray ?? [];
        if (config.Route == LauncherFrontendSubpageKey.DownloadResourcePack
            && categories.Any(category => GetInt(category as JsonObject, "id") == 5193))
        {
            return null;
        }

        var translatedTags = categories
            .Select(category => TranslateCurseForgeCategory(GetInt(category as JsonObject, "id")))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var latestFiles = item["latestFilesIndexes"] as JsonArray ?? [];
        var primaryFile = SelectCurseForgeFileIndex(latestFiles, preferredVersion);
        var website = GetString(item["links"] as JsonObject, "websiteUrl");
        if (string.IsNullOrWhiteSpace(website))
        {
            var slug = GetString(item, "slug");
            if (!string.IsNullOrWhiteSpace(slug))
            {
                website = $"https://www.curseforge.com/minecraft/{config.CurseForgeSectionPath}/{slug}";
            }
        }

        var downloads = GetInt(item, "downloadCount");
        var releasedAt = ParseDateTimeOffset(item["dateReleased"]);
        var updatedAt = ParseDateTimeOffset(item["dateModified"]) ?? releasedAt;

        return new FrontendDownloadResourceEntry(
            title,
            BuildEntryInfo(GetString(item, "summary"), null, updatedAt, downloads),
            "CurseForge",
            primaryFile is null ? string.Empty : GetString(primaryFile, "gameVersion"),
            primaryFile is null ? ResolveShaderLoaderFromTags(translatedTags) : ResolveCurseForgeLoader(primaryFile, config.UseShaderLoaderOptions, translatedTags),
            translatedTags.Length == 0 ? [config.Title] : translatedTags,
            "打开页面",
            config.IconName,
            website,
            downloads,
            0,
            ToRank(releasedAt),
            ToRank(updatedAt));
    }

    private static JsonObject ReadJsonObject(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey)
    {
        var candidates = BuildCandidateUrls(officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey);
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (candidate.UseCurseForgeApiKey)
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", Secrets.CurseForgeAPIKey);
                }

                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonNode.Parse(content)?.AsObject()
                       ?? throw new InvalidOperationException($"{sourceName} 返回了无效的 JSON 对象。");
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(string.Join("；", errors.Distinct(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<RequestCandidate> BuildCandidateUrls(
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey)
    {
        var candidates = new List<RequestCandidate>();
        var canUseOfficial = !officialRequiresApiKey || !string.IsNullOrWhiteSpace(Secrets.CurseForgeAPIKey);

        switch (communitySourcePreference)
        {
            case 0:
                candidates.Add(new RequestCandidate(mirrorUrl, false));
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                break;
            case 1:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
            default:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new RequestCandidate(mirrorUrl, false));
        }

        return candidates
            .DistinctBy(candidate => candidate.Url)
            .ToArray();
    }

    private static string BuildModrinthSearchUrl(RouteConfig config, string? preferredVersion, bool useMirror)
    {
        var baseUrl = useMirror
            ? "https://mod.mcimirror.top/modrinth/v2/search"
            : "https://api.modrinth.com/v2/search";
        var facets = new List<string> { $"[\"project_type:{config.ModrinthProjectType}\"]" };

        if (!string.IsNullOrWhiteSpace(config.ModrinthCategory))
        {
            facets.Add($"[\"categories:{config.ModrinthCategory}\"]");
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            facets.Add($"[\"versions:{preferredVersion}\"]");
        }

        return $"{baseUrl}?limit={SearchPageSize}&index=downloads&facets={Uri.EscapeDataString($"[{string.Join(",", facets)}]")}";
    }

    private static string BuildCurseForgeSearchUrl(RouteConfig config, string? preferredVersion, bool useMirror)
    {
        var baseUrl = useMirror
            ? "https://mod.mcimirror.top/curseforge/v1/mods/search"
            : "https://api.curseforge.com/v1/mods/search";
        var parameters = new List<string>
        {
            "gameId=432",
            $"classId={config.CurseForgeClassId}",
            $"pageSize={SearchPageSize}",
            "sortField=6",
            "sortOrder=desc"
        };

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            parameters.Add($"gameVersion={Uri.EscapeDataString(preferredVersion)}");
        }

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }

    private static string BuildHintText(
        RouteConfig config,
        string? preferredVersion,
        IReadOnlyList<string> sourceErrors,
        bool hasEntries,
        bool usedVersionFallback)
    {
        if (sourceErrors.Count == 0 && !usedVersionFallback)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (usedVersionFallback && !string.IsNullOrWhiteSpace(preferredVersion))
        {
            parts.Add($"没有找到适配 Minecraft {preferredVersion} 的 {config.Title} 热门结果，已回退到社区通用榜单。");
        }

        if (sourceErrors.Count > 0)
        {
            parts.Add(hasEntries
                ? $"已显示可访问来源的实时结果，另有部分来源失败：{string.Join("；", sourceErrors)}"
                : $"当前无法获取实时社区结果：{string.Join("；", sourceErrors)}");
        }

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<FrontendDownloadResourceFilterOption> BuildTagOptions(
        IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return
        [
            new FrontendDownloadResourceFilterOption("全部", string.Empty),
            .. entries
                .SelectMany(entry => entry.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .Select(group => new FrontendDownloadResourceFilterOption(group.First(), group.First()))
        ];
    }

    private static string ResolvePreferredMinecraftVersion(FrontendInstanceComposition instanceComposition)
    {
        var candidate = NormalizeMinecraftVersion(instanceComposition.Selection.VanillaVersion);
        return string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate;
    }

    private static string? NormalizeMinecraftVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = Regex.Match(rawValue, @"\d+\.\d+(?:\.\d+)?");
        return match.Success ? match.Value : null;
    }

    private static string ResolvePrimaryVersion(IEnumerable<string> versions, string? preferredVersion)
    {
        var normalizedVersions = versions
            .Select(NormalizeMinecraftVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && normalizedVersions.Contains(preferredVersion, StringComparer.OrdinalIgnoreCase))
        {
            return preferredVersion;
        }

        return normalizedVersions
            .OrderByDescending(version => ParseVersion(version))
            .FirstOrDefault()
               ?? string.Empty;
    }

    private static JsonObject? SelectCurseForgeFileIndex(JsonArray latestFiles, string? preferredVersion)
    {
        var entries = latestFiles
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .Where(node => !string.IsNullOrWhiteSpace(NormalizeMinecraftVersion(GetString(node, "gameVersion"))))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var preferredMatch = entries.FirstOrDefault(entry =>
                string.Equals(NormalizeMinecraftVersion(GetString(entry, "gameVersion")), preferredVersion, StringComparison.OrdinalIgnoreCase));
            if (preferredMatch is not null)
            {
                return preferredMatch;
            }
        }

        return entries
            .OrderByDescending(entry => ParseVersion(NormalizeMinecraftVersion(GetString(entry, "gameVersion"))))
            .FirstOrDefault();
    }

    private static Version ParseVersion(string? rawValue)
    {
        return Version.TryParse(rawValue, out var version)
            ? version
            : new Version(0, 0);
    }

    private static string ResolvePrimaryLoader(IEnumerable<string> rawCategories, bool useShaderLoaderOptions)
    {
        var categorySet = rawCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (useShaderLoaderOptions)
        {
            if (categorySet.Contains("iris"))
            {
                return "Iris";
            }

            if (categorySet.Contains("optifine"))
            {
                return "OptiFine";
            }

            if (categorySet.Contains("vanilla"))
            {
                return "原版可用";
            }

            return string.Empty;
        }

        return categorySet.Contains("neoforge") ? "NeoForge"
            : categorySet.Contains("forge") ? "Forge"
            : categorySet.Contains("fabric") ? "Fabric"
            : categorySet.Contains("quilt") ? "Quilt"
            : string.Empty;
    }

    private static string ResolveCurseForgeLoader(JsonObject fileIndex, bool useShaderLoaderOptions, IReadOnlyList<string> tags)
    {
        if (useShaderLoaderOptions)
        {
            return ResolveShaderLoaderFromTags(tags);
        }

        return GetInt(fileIndex, "modLoader") switch
        {
            1 => "Forge",
            4 => "Fabric",
            5 => "Quilt",
            6 => "NeoForge",
            _ => string.Empty
        };
    }

    private static string ResolveShaderLoaderFromTags(IEnumerable<string> tags)
    {
        var tagSet = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tagSet.Contains("Iris"))
        {
            return "Iris";
        }

        if (tagSet.Contains("OptiFine"))
        {
            return "OptiFine";
        }

        if (tagSet.Contains("原版可用"))
        {
            return "原版可用";
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> TranslateModrinthCategories(IEnumerable<string> categories)
    {
        return categories
            .Select(category => category switch
            {
                "technology" => "科技",
                "magic" => "魔法",
                "adventure" => "冒险",
                "utility" => "实用",
                "optimization" => "性能优化",
                "vanilla-like" => "原版风",
                "realistic" => "写实风",
                "worldgen" => "世界元素",
                "food" => "食物/烹饪",
                "game-mechanics" => "游戏机制",
                "transportation" => "运输",
                "storage" => "仓储",
                "decoration" => "装饰",
                "mobs" => "生物",
                "equipment" => "装备",
                "social" => "服务器",
                "library" => "支持库",
                "multiplayer" => "多人",
                "challenging" => "硬核",
                "combat" => "战斗",
                "quests" => "任务",
                "kitchen-sink" => "水槽包",
                "lightweight" => "轻量",
                "simplistic" => "简洁",
                "tweaks" => "改良",
                "8x-" => "极简",
                "16x" => "16x",
                "32x" => "32x",
                "48x" => "48x",
                "64x" => "64x",
                "128x" => "128x",
                "256x" => "256x",
                "512x+" => "超高清",
                "audio" => "含声音",
                "fonts" => "含字体",
                "models" => "含模型",
                "gui" => "含 UI",
                "locale" => "含语言",
                "core-shaders" => "核心着色器",
                "modded" => "兼容 Mod",
                "fantasy" => "幻想风",
                "semi-realistic" => "半写实风",
                "cartoon" => "卡通风",
                "colored-lighting" => "彩色光照",
                "path-tracing" => "路径追踪",
                "pbr" => "PBR",
                "reflections" => "反射",
                "iris" => "Iris",
                "optifine" => "OptiFine",
                "vanilla" => "原版可用",
                "datapack" => "数据包",
                _ => string.Empty
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray();
    }

    private static string TranslateCurseForgeCategory(int categoryId)
    {
        return categoryId switch
        {
            406 => "世界元素",
            407 => "生物群系",
            410 => "维度",
            408 => "矿物/资源",
            409 => "天然结构",
            412 => "科技",
            415 => "管道/物流",
            4843 => "自动化",
            417 => "能源",
            4558 => "红石",
            436 => "食物/烹饪",
            416 => "农业",
            414 => "运输",
            420 => "仓储",
            419 => "魔法",
            422 => "冒险",
            424 => "装饰",
            411 => "生物",
            434 => "装备",
            6814 => "性能优化",
            9026 => "创造模式",
            423 => "信息显示",
            435 => "服务器",
            5191 => "改良",
            421 => "支持库",
            4484 => "多人",
            4479 => "硬核",
            4483 => "战斗",
            4478 => "任务",
            4472 => "科技",
            4473 => "魔法",
            4475 => "冒险",
            4476 => "探索",
            4477 => "小游戏",
            4471 => "科幻",
            4736 => "空岛",
            5128 => "原版改良",
            4487 => "FTB",
            4480 => "基于地图",
            4481 => "轻量",
            4482 => "大型",
            403 => "原版风",
            400 => "写实风",
            401 => "现代风",
            402 => "中世纪",
            399 => "蒸汽朋克",
            5244 => "含字体",
            404 => "动态效果",
            4465 => "兼容 Mod",
            393 => "16x",
            394 => "32x",
            395 => "64x",
            396 => "128x",
            397 => "256x",
            398 => "超高清",
            5193 => "数据包",
            6553 => "写实风",
            6554 => "幻想风",
            6555 => "原版风",
            6948 => "冒险",
            6949 => "幻想",
            6950 => "支持库",
            6952 => "魔法",
            6946 => "Mod 相关",
            6951 => "科技",
            6953 => "实用",
            248 => "冒险",
            249 => "创造",
            250 => "小游戏",
            251 => "跑酷",
            252 => "解谜",
            253 => "生存",
            4464 => "Mod 世界",
            _ => string.Empty
        };
    }

    private static string BuildEntryInfo(string? description, string? author, DateTimeOffset? updatedAt, int downloads)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(author))
        {
            metaParts.Add(author.Trim());
        }

        if (updatedAt is not null)
        {
            metaParts.Add($"更新于 {updatedAt.Value.LocalDateTime:yyyy/MM/dd}");
        }

        if (downloads > 0)
        {
            metaParts.Add($"{downloads:N0} 次下载");
        }

        if (metaParts.Count > 0)
        {
            parts.Add(string.Join(" • ", metaParts));
        }

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static int ToRank(DateTimeOffset? value)
    {
        return value is null ? 0 : (int)Math.Clamp(value.Value.ToUnixTimeSeconds(), 0, int.MaxValue);
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var rawValue = node?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static string GetString(JsonObject? obj, string propertyName)
    {
        return obj?[propertyName]?.GetValue<string>()?.Trim() ?? string.Empty;
    }

    private static int GetInt(JsonObject? obj, string propertyName)
    {
        if (obj?[propertyName] is null)
        {
            return 0;
        }

        return obj[propertyName]!.GetValue<int>();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CE-Frontend-Spike");
        return client;
    }

    private sealed record RouteConfig(
        LauncherFrontendSubpageKey Route,
        string Title,
        string IconName,
        bool UseShaderLoaderOptions,
        string? ModrinthProjectType,
        string? ModrinthCategory,
        int? CurseForgeClassId,
        string CurseForgeSectionPath);

    private sealed record CacheEntry(FrontendDownloadResourceState State, DateTimeOffset CreatedAt);

    private sealed record FetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        IReadOnlyList<string> SourceErrors);

    private sealed record SourceFetchResult(
        IReadOnlyList<FrontendDownloadResourceEntry> Entries,
        string? ErrorMessage);

    private sealed record RequestCandidate(string Url, bool UseCurseForgeApiKey);
}

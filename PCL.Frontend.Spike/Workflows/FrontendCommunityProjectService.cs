using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendCommunityProjectService
{
    private const string CompDetailTargetPrefix = "comp-detail:";
    private static readonly string CurseForgeApiKey = Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY") ?? string.Empty;
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, CacheEntry<FrontendCommunityProjectSummary>> SummaryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<FrontendCommunityProjectState>> DetailCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<FrontendHomePageMarketState>> MarketCache = new(StringComparer.OrdinalIgnoreCase);

    public static string CreateCompDetailTarget(string projectId)
    {
        return $"{CompDetailTargetPrefix}{Uri.EscapeDataString(projectId.Trim())}";
    }

    public static bool TryParseCompDetailTarget(string? target, out string projectId)
    {
        if (!string.IsNullOrWhiteSpace(target)
            && target.StartsWith(CompDetailTargetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            projectId = Uri.UnescapeDataString(target[CompDetailTargetPrefix.Length..]).Trim();
            return !string.IsNullOrWhiteSpace(projectId);
        }

        projectId = string.Empty;
        return false;
    }

    public static FrontendCommunityProjectLookupResult LookupProjects(
        IEnumerable<string> projectIds,
        int communitySourcePreference)
    {
        var normalizedIds = projectIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedIds.Length == 0)
        {
            return new FrontendCommunityProjectLookupResult(
                new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase),
                []);
        }

        var results = new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var missingModrinthIds = new List<string>();
        var missingCurseForgeIds = new List<string>();

        foreach (var projectId in normalizedIds)
        {
            if (TryGetFresh(SummaryCache, projectId, out var cached))
            {
                results[projectId] = cached;
                continue;
            }

            if (IsCurseForgeId(projectId))
            {
                missingCurseForgeIds.Add(projectId);
            }
            else
            {
                missingModrinthIds.Add(projectId);
            }
        }

        if (missingModrinthIds.Count > 0)
        {
            try
            {
                foreach (var summary in FetchModrinthSummaries(missingModrinthIds, communitySourcePreference))
                {
                    results[summary.ProjectId] = summary;
                    SummaryCache[summary.ProjectId] = new CacheEntry<FrontendCommunityProjectSummary>(summary, DateTimeOffset.UtcNow);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Modrinth 收藏元数据加载失败：{ex.Message}");
            }
        }

        if (missingCurseForgeIds.Count > 0)
        {
            try
            {
                foreach (var summary in FetchCurseForgeSummaries(missingCurseForgeIds, communitySourcePreference))
                {
                    results[summary.ProjectId] = summary;
                    SummaryCache[summary.ProjectId] = new CacheEntry<FrontendCommunityProjectSummary>(summary, DateTimeOffset.UtcNow);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"CurseForge 收藏元数据加载失败：{ex.Message}");
            }
        }

        return new FrontendCommunityProjectLookupResult(results, errors);
    }

    public static FrontendCommunityProjectState GetProjectState(
        string projectId,
        string preferredMinecraftVersion,
        int communitySourcePreference)
    {
        var normalizedId = projectId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return new FrontendCommunityProjectState(
                string.Empty,
                "未选择工程",
                "先从收藏夹或市场条目进入，才能查看对应工程详情。",
                string.Empty,
                "未指定来源",
                "等待选择",
                "尚未加载",
                0,
                0,
                "未提供兼容信息",
                "未提供标签信息",
                [],
                "当前详情页没有携带工程标识。",
                true);
        }

        var cacheKey = $"{normalizedId}|{NormalizeMinecraftVersion(preferredMinecraftVersion) ?? "*"}|{communitySourcePreference}";
        if (TryGetFresh(DetailCache, cacheKey, out var cached))
        {
            return cached;
        }

        FrontendCommunityProjectState state;
        try
        {
            state = IsCurseForgeId(normalizedId)
                ? FetchCurseForgeDetail(normalizedId, preferredMinecraftVersion, communitySourcePreference)
                : FetchModrinthDetail(normalizedId, preferredMinecraftVersion, communitySourcePreference);
        }
        catch (Exception ex)
        {
            state = new FrontendCommunityProjectState(
                normalizedId,
                $"项目 {normalizedId}",
                "无法从实时来源加载该工程。",
                string.Empty,
                IsCurseForgeId(normalizedId) ? "CurseForge" : "Modrinth",
                "加载失败",
                "未知",
                0,
                0,
                "未提供兼容信息",
                "未提供标签信息",
                [],
                ex.Message,
                true);
        }

        DetailCache[cacheKey] = new CacheEntry<FrontendCommunityProjectState>(state, DateTimeOffset.UtcNow);
        return state;
    }

    public static FrontendHomePageMarketState BuildHomePageMarketState(
        FrontendInstanceComposition instanceComposition,
        int communitySourcePreference)
    {
        var preferredVersion = ResolvePreferredMinecraftVersion(instanceComposition);
        var cacheKey = $"{preferredVersion}|{communitySourcePreference}";
        if (TryGetFresh(MarketCache, cacheKey, out var cached))
        {
            return cached;
        }

        var requests = new[]
        {
            new FrontendHomePageMarketSectionRequest(LauncherFrontendSubpageKey.DownloadMod, "热门 Mod", 5),
            new FrontendHomePageMarketSectionRequest(LauncherFrontendSubpageKey.DownloadPack, "热门整合包", 4),
            new FrontendHomePageMarketSectionRequest(LauncherFrontendSubpageKey.DownloadResourcePack, "热门资源包", 4),
            new FrontendHomePageMarketSectionRequest(LauncherFrontendSubpageKey.DownloadShader, "热门光影包", 4)
        };

        var sections = new List<FrontendDownloadCatalogSection>();
        var warnings = new List<string>();
        foreach (var request in requests)
        {
            try
            {
                var result = FrontendCommunityResourceCatalogService.QueryResources(
                    request.Route,
                    new FrontendCommunityResourceQuery(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                    instanceComposition,
                    communitySourcePreference);
                if (!string.IsNullOrWhiteSpace(result.State.HintText))
                {
                    warnings.Add(result.State.HintText);
                }

                var entries = result.State.Entries
                    .Take(request.Limit)
                    .Select(entry => new FrontendDownloadCatalogEntry(
                        entry.Title,
                        entry.Info,
                        BuildMarketMeta(entry),
                        "打开页面",
                        entry.TargetPath))
                    .ToArray();
                if (entries.Length == 0)
                {
                    entries =
                    [
                        new FrontendDownloadCatalogEntry(
                            "暂无实时结果",
                            $"{request.Title} 目前没有可展示的实时社区条目。",
                            "请稍后重试",
                            "查看详情",
                            null)
                    ];
                }

                sections.Add(new FrontendDownloadCatalogSection(request.Title, entries));
            }
            catch (Exception ex)
            {
                warnings.Add($"{request.Title} 加载失败：{ex.Message}");
                sections.Add(new FrontendDownloadCatalogSection(
                    request.Title,
                    [
                        new FrontendDownloadCatalogEntry(
                            "加载失败",
                            ex.Message,
                            "实时市场暂不可用",
                            "查看详情",
                            null)
                    ]));
            }
        }

        var state = new FrontendHomePageMarketState(
            string.IsNullOrWhiteSpace(preferredVersion)
                ? "主页市场会聚合热门社区资源，方便直接浏览当前 Mod、整合包、资源包与光影趋势。"
                : $"主页市场优先围绕 Minecraft {preferredVersion} 聚合热门社区资源，方便直接浏览当前 Mod、整合包、资源包与光影趋势。",
            sections,
            string.Join(" ", warnings.Distinct(StringComparer.Ordinal)),
            warnings.Count > 0);
        MarketCache[cacheKey] = new CacheEntry<FrontendHomePageMarketState>(state, DateTimeOffset.UtcNow);
        return state;
    }

    private static IReadOnlyList<FrontendCommunityProjectSummary> FetchModrinthSummaries(
        IReadOnlyList<string> projectIds,
        int communitySourcePreference)
    {
        var encodedIds = Uri.EscapeDataString(JsonSerializer.Serialize(projectIds));
        var response = ReadJsonArray(
            "Modrinth",
            $"https://api.modrinth.com/v2/projects?ids={encodedIds}",
            $"https://mod.mcimirror.top/modrinth/v2/projects?ids={encodedIds}",
            communitySourcePreference,
            officialRequiresApiKey: false,
            postBody: null);

        return response
            .Select(node => node as JsonObject)
            .Where(project => project is not null)
            .Select(project => BuildModrinthSummary(project!))
            .Where(summary => summary is not null)
            .Cast<FrontendCommunityProjectSummary>()
            .ToArray();
    }

    private static IReadOnlyList<FrontendCommunityProjectSummary> FetchCurseForgeSummaries(
        IReadOnlyList<string> projectIds,
        int communitySourcePreference)
    {
        var body = JsonSerializer.Serialize(new
        {
            modIds = projectIds
                .Select(id => int.TryParse(id, out var parsed) ? parsed : -1)
                .Where(id => id >= 0)
                .ToArray()
        });
        var response = ReadJsonObject(
            "CurseForge",
            "https://api.curseforge.com/v1/mods",
            "https://mod.mcimirror.top/curseforge/v1/mods",
            communitySourcePreference,
            officialRequiresApiKey: true,
            postBody: body);

        return (response["data"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(project => project is not null)
            .Select(project => BuildCurseForgeSummary(project!))
            .Where(summary => summary is not null)
            .Cast<FrontendCommunityProjectSummary>()
            .ToArray();
    }

    private static FrontendCommunityProjectState FetchModrinthDetail(
        string projectId,
        string preferredMinecraftVersion,
        int communitySourcePreference)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var encodedProject = Uri.EscapeDataString(projectId);
        var project = ReadJsonObject(
            "Modrinth",
            $"https://api.modrinth.com/v2/project/{encodedProject}",
            $"https://mod.mcimirror.top/modrinth/v2/project/{encodedProject}",
            communitySourcePreference,
            officialRequiresApiKey: false,
            postBody: null);
        var versions = ReadJsonArray(
            "Modrinth",
            $"https://api.modrinth.com/v2/project/{encodedProject}/version",
            $"https://mod.mcimirror.top/modrinth/v2/project/{encodedProject}/version",
            communitySourcePreference,
            officialRequiresApiKey: false,
            postBody: null);

        var title = GetString(project, "title") ?? projectId;
        var summary = NormalizeWhitespace(GetString(project, "description"));
        var body = TruncateText(CleanProjectDescription(GetString(project, "body")), 520);
        var website = BuildModrinthWebsite(project);
        var loaders = DistinctValues(project["loaders"] as JsonArray);
        var categories = TranslateModrinthCategories(DistinctValues(project["categories"] as JsonArray)
            .Concat(DistinctValues(project["additional_categories"] as JsonArray)));
        var gameVersions = DistinctValues(project["game_versions"] as JsonArray)
            .Select(version => NormalizeMinecraftVersion(version) ?? version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(version => ParseVersion(version))
            .ToArray();
        var releases = versions
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .OrderByDescending(version => MatchesPreferredVersion(version!, normalizedVersion))
            .ThenByDescending(version => ParseDateTimeOffset(version!["date_published"]))
            .Take(8)
            .Select(version => BuildModrinthReleaseEntry(version!, website))
            .Where(entry => entry is not null)
            .Cast<FrontendDownloadCatalogEntry>()
            .ToArray();
        var links = BuildModrinthLinkEntries(project, website);

        return new FrontendCommunityProjectState(
            projectId,
            title,
            string.IsNullOrWhiteSpace(summary) ? "该项目没有提供额外摘要。" : summary,
            body,
            "Modrinth",
            TranslateProjectStatus(GetString(project, "status")),
            FormatDateLabel(ParseDateTimeOffset(project["updated"])),
            GetInt(project, "downloads"),
            GetInt(project, "followers"),
            BuildCompatibilitySummary(gameVersions, loaders),
            BuildCategorySummary(categories),
            BuildProjectSections(releases, links),
            string.Empty,
            false);
    }

    private static FrontendCommunityProjectState FetchCurseForgeDetail(
        string projectId,
        string preferredMinecraftVersion,
        int communitySourcePreference)
    {
        var normalizedVersion = NormalizeMinecraftVersion(preferredMinecraftVersion);
        var project = ReadJsonObject(
            "CurseForge",
            $"https://api.curseforge.com/v1/mods/{projectId}",
            $"https://mod.mcimirror.top/curseforge/v1/mods/{projectId}",
            communitySourcePreference,
            officialRequiresApiKey: true,
            postBody: null);
        var data = project["data"]?.AsObject() ?? throw new InvalidOperationException("CurseForge 返回了空的工程数据。");
        var website = GetString(data["links"] as JsonObject, "websiteUrl")
                      ?? BuildCurseForgeWebsite(data);
        var releases = (data["latestFiles"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .OrderByDescending(file => MatchesPreferredVersionInFile(file!, normalizedVersion))
            .ThenByDescending(file => ParseDateTimeOffset(file!["fileDate"]))
            .Take(8)
            .Select(file => BuildCurseForgeReleaseEntry(file!, website))
            .Where(entry => entry is not null)
            .Cast<FrontendDownloadCatalogEntry>()
            .ToArray();
        var categories = (data["categories"] as JsonArray ?? [])
            .Select(node => TranslateCurseForgeCategory(GetInt(node as JsonObject, "id")))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var indexes = (data["latestFilesIndexes"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
        var gameVersions = indexes
            .Select(index => NormalizeMinecraftVersion(GetString(index, "gameVersion")))
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ParseVersion)
            .ToArray();
        var loaders = indexes
            .Select(index => TranslateCurseForgeLoader(GetInt(index, "modLoader")))
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var links = BuildCurseForgeLinkEntries(data, website);

        return new FrontendCommunityProjectState(
            projectId,
            GetString(data, "name") ?? $"项目 {projectId}",
            NormalizeWhitespace(GetString(data, "summary")) is { Length: > 0 } summary
                ? summary
                : "该项目没有提供额外摘要。",
            string.Empty,
            "CurseForge",
            TranslateCurseForgeStatus(GetInt(data, "status")),
            FormatDateLabel(ParseDateTimeOffset(data["dateModified"]) ?? ParseDateTimeOffset(data["dateReleased"])),
            GetInt(data, "downloadCount"),
            GetInt(data, "thumbsUpCount"),
            BuildCompatibilitySummary(gameVersions, loaders),
            BuildCategorySummary(categories),
            BuildProjectSections(releases, links),
            string.Empty,
            false);
    }

    private static FrontendCommunityProjectSummary? BuildModrinthSummary(JsonObject project)
    {
        var projectId = GetString(project, "id");
        var title = GetString(project, "title");
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new FrontendCommunityProjectSummary(
            projectId,
            title,
            NormalizeWhitespace(GetString(project, "description")) ?? "该项目没有提供摘要。",
            "Modrinth",
            null,
            TranslateProjectType(GetString(project, "project_type")),
            BuildModrinthWebsite(project),
            FormatDateLabel(ParseDateTimeOffset(project["updated"])),
            GetInt(project, "downloads"),
            GetInt(project, "followers"));
    }

    private static FrontendCommunityProjectSummary? BuildCurseForgeSummary(JsonObject project)
    {
        var projectId = GetInt(project, "id");
        var title = GetString(project, "name");
        if (projectId <= 0 || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var authors = (project["authors"] as JsonArray ?? [])
            .Select(node => GetString(node as JsonObject, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        return new FrontendCommunityProjectSummary(
            projectId.ToString(),
            title,
            NormalizeWhitespace(GetString(project, "summary")) ?? "该项目没有提供摘要。",
            "CurseForge",
            authors.Length == 0 ? null : string.Join(" / ", authors),
            TranslateCurseForgeClass(GetInt(project, "classId")),
            GetString(project["links"] as JsonObject, "websiteUrl") ?? BuildCurseForgeWebsite(project),
            FormatDateLabel(ParseDateTimeOffset(project["dateModified"]) ?? ParseDateTimeOffset(project["dateReleased"])),
            GetInt(project, "downloadCount"),
            GetInt(project, "thumbsUpCount"));
    }

    private static FrontendDownloadCatalogEntry? BuildModrinthReleaseEntry(JsonObject version, string? website)
    {
        var title = GetString(version, "name") ?? GetString(version, "version_number");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var info = BuildReleaseInfo(
            DistinctValues(version["game_versions"] as JsonArray),
            DistinctValues(version["loaders"] as JsonArray));
        var file = (version["files"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => node is not null && GetBool(node, "primary"));
        var target = GetString(file, "url") ?? website;
        var downloads = GetInt(version, "downloads");
        var meta = $"发布 {FormatDateLabel(ParseDateTimeOffset(version["date_published"]))}"
                   + (downloads > 0 ? $" • {FormatCompactCount(downloads)} 下载" : string.Empty);

        return new FrontendDownloadCatalogEntry(
            title,
            info,
            meta,
            string.IsNullOrWhiteSpace(target) ? "查看详情" : "打开下载",
            target);
    }

    private static FrontendDownloadCatalogEntry? BuildCurseForgeReleaseEntry(JsonObject file, string? website)
    {
        var title = GetString(file, "displayName") ?? GetString(file, "fileName");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var info = BuildReleaseInfo(
            DistinctValues(file["gameVersions"] as JsonArray),
            DistinctValues(file["gameVersions"] as JsonArray)
                .Select(NormalizeLoaderToken)
                .Where(loader => !string.IsNullOrWhiteSpace(loader))
                .Cast<string>()
                .ToArray());
        var downloads = GetInt(file, "downloadCount");
        var meta = $"发布 {FormatDateLabel(ParseDateTimeOffset(file["fileDate"]))}"
                   + (downloads > 0 ? $" • {FormatCompactCount(downloads)} 下载" : string.Empty);
        return new FrontendDownloadCatalogEntry(
            title,
            info,
            meta,
            string.IsNullOrWhiteSpace(website) ? "查看详情" : "打开项目页",
            website);
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> BuildModrinthLinkEntries(JsonObject project, string? website)
    {
        var entries = new List<FrontendDownloadCatalogEntry>();
        AddLinkEntry(entries, "项目主页", website, "打开项目主页");
        AddLinkEntry(entries, "问题反馈", GetString(project, "issues_url"), "打开 Issues");
        AddLinkEntry(entries, "源码仓库", GetString(project, "source_url"), "打开源码");
        AddLinkEntry(entries, "Wiki", GetString(project, "wiki_url"), "打开 Wiki");
        AddLinkEntry(entries, "Discord", GetString(project, "discord_url"), "打开社区");

        foreach (var donation in (project["donation_urls"] as JsonArray ?? [])
                     .Select(node => node as JsonObject)
                     .Where(node => node is not null))
        {
            AddLinkEntry(
                entries,
                $"支持作者 · {GetString(donation, "platform") ?? "赞助"}",
                GetString(donation, "url"),
                "打开链接");
        }

        return entries;
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> BuildCurseForgeLinkEntries(JsonObject project, string? website)
    {
        var links = project["links"] as JsonObject;
        var entries = new List<FrontendDownloadCatalogEntry>();
        AddLinkEntry(entries, "项目主页", website, "打开项目主页");
        AddLinkEntry(entries, "问题反馈", GetString(links, "issuesUrl"), "打开 Issues");
        AddLinkEntry(entries, "源码仓库", GetString(links, "sourceUrl"), "打开源码");
        AddLinkEntry(entries, "Wiki", GetString(links, "wikiUrl"), "打开 Wiki");
        return entries;
    }

    private static IReadOnlyList<FrontendDownloadCatalogSection> BuildProjectSections(
        IReadOnlyList<FrontendDownloadCatalogEntry> releases,
        IReadOnlyList<FrontendDownloadCatalogEntry> links)
    {
        var sections = new List<FrontendDownloadCatalogSection>();
        if (releases.Count > 0)
        {
            sections.Add(new FrontendDownloadCatalogSection("最近版本", releases));
        }

        if (links.Count > 0)
        {
            sections.Add(new FrontendDownloadCatalogSection("相关链接", links));
        }

        return sections;
    }

    private static JsonArray ReadJsonArray(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        return ReadJsonNode(sourceName, officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey, postBody)?.AsArray()
               ?? throw new InvalidOperationException($"{sourceName} 返回了无效的 JSON 数组。");
    }

    private static JsonObject ReadJsonObject(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        return ReadJsonNode(sourceName, officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey, postBody)?.AsObject()
               ?? throw new InvalidOperationException($"{sourceName} 返回了无效的 JSON 对象。");
    }

    private static JsonNode? ReadJsonNode(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        var candidates = BuildCandidateUrls(officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey);
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(postBody is null ? HttpMethod.Get : HttpMethod.Post, candidate.Url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (candidate.UseCurseForgeApiKey)
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", CurseForgeApiKey);
                }

                if (postBody is not null)
                {
                    request.Content = new StringContent(postBody, Encoding.UTF8, "application/json");
                }

                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonNode.Parse(content);
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
        var canUseOfficial = !officialRequiresApiKey || !string.IsNullOrWhiteSpace(CurseForgeApiKey);
        var candidates = new List<RequestCandidate>();
        switch (communitySourcePreference)
        {
            case 0:
                candidates.Add(new RequestCandidate(mirrorUrl, false));
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                break;
            default:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
        }

        return candidates
            .DistinctBy(candidate => candidate.Url)
            .ToArray();
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static bool MatchesPreferredVersion(JsonObject version, string? preferredVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            return false;
        }

        return DistinctValues(version["game_versions"] as JsonArray)
            .Select(value => NormalizeMinecraftVersion(value) ?? value)
            .Any(value => string.Equals(value, preferredVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPreferredVersionInFile(JsonObject file, string? preferredVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            return false;
        }

        return DistinctValues(file["gameVersions"] as JsonArray)
            .Select(value => NormalizeMinecraftVersion(value) ?? value)
            .Any(value => string.Equals(value, preferredVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildModrinthWebsite(JsonObject project)
    {
        var type = GetString(project, "project_type");
        var slug = GetString(project, "slug");
        return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(slug)
            ? string.Empty
            : $"https://modrinth.com/{type}/{slug}";
    }

    private static string BuildCurseForgeWebsite(JsonObject project)
    {
        var slug = GetString(project, "slug");
        var section = TranslateCurseForgeSection(GetInt(project, "classId"));
        return string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(section)
            ? string.Empty
            : $"https://www.curseforge.com/minecraft/{section}/{slug}";
    }

    private static void AddLinkEntry(
        ICollection<FrontendDownloadCatalogEntry> entries,
        string title,
        string? target,
        string actionText)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        entries.Add(new FrontendDownloadCatalogEntry(
            title,
            target,
            string.Empty,
            actionText,
            target));
    }

    private static bool IsCurseForgeId(string projectId)
    {
        return int.TryParse(projectId, out _);
    }

    private static bool TryGetFresh<T>(
        ConcurrentDictionary<string, CacheEntry<T>> cache,
        string key,
        out T value)
    {
        if (cache.TryGetValue(key, out var entry)
            && DateTimeOffset.UtcNow - entry.CreatedAt < TimeSpan.FromMinutes(10))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    private static string ResolvePreferredMinecraftVersion(FrontendInstanceComposition instanceComposition)
    {
        var candidate = NormalizeMinecraftVersion(instanceComposition.Selection.VanillaVersion);
        return string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate;
    }

    private static string BuildMarketMeta(FrontendDownloadResourceEntry entry)
    {
        var parts = new List<string> { entry.Source };
        if (!string.IsNullOrWhiteSpace(entry.Version))
        {
            parts.Add(entry.Version);
        }

        if (!string.IsNullOrWhiteSpace(entry.Loader))
        {
            parts.Add(entry.Loader);
        }

        if (entry.DownloadCount > 0)
        {
            parts.Add($"{FormatCompactCount(entry.DownloadCount)} 下载");
        }

        return string.Join(" • ", parts);
    }

    private static string BuildCompatibilitySummary(
        IReadOnlyList<string> versions,
        IReadOnlyList<string> loaders)
    {
        var versionLabel = versions.Count == 0
            ? "未提供 Minecraft 版本列表"
            : $"Minecraft {string.Join(" / ", versions.Take(6))}{(versions.Count > 6 ? " 等" : string.Empty)}";
        var loaderLabel = loaders.Count == 0
            ? "未提供加载器信息"
            : $"加载器：{string.Join(" / ", loaders)}";
        return $"{versionLabel} • {loaderLabel}";
    }

    private static string BuildCategorySummary(IReadOnlyList<string> categories)
    {
        return categories.Count == 0
            ? "未提供标签信息"
            : string.Join(" / ", categories.Take(10));
    }

    private static string BuildReleaseInfo(
        IReadOnlyList<string> versions,
        IReadOnlyList<string> loaders)
    {
        var normalizedVersions = versions
            .Select(version => NormalizeMinecraftVersion(version) ?? version)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var parts = new List<string>();
        if (normalizedVersions.Length > 0)
        {
            parts.Add($"Minecraft {string.Join(" / ", normalizedVersions.Take(4))}{(normalizedVersions.Length > 4 ? " 等" : string.Empty)}");
        }

        if (loaders.Count > 0)
        {
            parts.Add(string.Join(" / ", loaders.Take(4)));
        }

        return parts.Count == 0 ? "未提供版本信息" : string.Join(" • ", parts);
    }

    private static string? CleanProjectDescription(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var text = rawValue
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]+\)", " ");
        text = Regex.Replace(text, @"\[[^\]]+\]\(([^)]+)\)", "$1");
        text = Regex.Replace(text, @"[`*_>#-]+", " ");
        return NormalizeWhitespace(text);
    }

    private static string? NormalizeWhitespace(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return Regex.Replace(rawValue, @"\s+", " ").Trim();
    }

    private static string TruncateText(string? rawValue, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (rawValue.Length <= maxLength)
        {
            return rawValue;
        }

        return rawValue[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
    }

    private static string FormatDateLabel(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "未知";
    }

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}亿",
            >= 10_000 => $"{value / 10_000d:0.#}万",
            _ => value.ToString()
        };
    }

    private static Version ParseVersion(string? rawValue)
    {
        return Version.TryParse(rawValue, out var version)
            ? version
            : new Version(0, 0);
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

    private static IReadOnlyList<string> DistinctValues(JsonArray? values)
    {
        return (values ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeLoaderToken(string rawValue)
    {
        return rawValue switch
        {
            "Forge" => "Forge",
            "NeoForge" => "NeoForge",
            "Fabric" => "Fabric",
            "Quilt" => "Quilt",
            "OptiFine" => "OptiFine",
            _ => string.Empty
        };
    }

    private static string TranslateCurseForgeLoader(int modLoader)
    {
        return modLoader switch
        {
            1 => "Forge",
            4 => "Fabric",
            5 => "Quilt",
            6 => "NeoForge",
            _ => string.Empty
        };
    }

    private static string TranslateProjectType(string? projectType)
    {
        return projectType switch
        {
            "mod" => "Mod",
            "modpack" => "整合包",
            "resourcepack" => "资源包",
            "shader" => "光影包",
            "datapack" => "数据包",
            _ => string.Empty
        };
    }

    private static string TranslateCurseForgeClass(int classId)
    {
        return classId switch
        {
            6 => "Mod",
            12 => "资源包",
            17 => "世界",
            4471 => "整合包",
            6552 => "光影包",
            6945 => "数据包",
            _ => string.Empty
        };
    }

    private static string TranslateCurseForgeSection(int classId)
    {
        return classId switch
        {
            6 => "mc-mods",
            12 => "texture-packs",
            17 => "worlds",
            4471 => "modpacks",
            6552 => "shaders",
            6945 => "data-packs",
            _ => string.Empty
        };
    }

    private static string TranslateProjectStatus(string? status)
    {
        return status switch
        {
            "approved" or "listed" => "已发布",
            "unlisted" => "未列出",
            "archived" => "已归档",
            "draft" => "草稿",
            _ => string.IsNullOrWhiteSpace(status) ? "未知" : status
        };
    }

    private static string TranslateCurseForgeStatus(int status)
    {
        return status switch
        {
            4 => "已发布",
            5 => "已下架",
            _ => status == 0 ? "未知" : $"状态 {status}"
        };
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
                "datapack" => "数据包",
                "iris" => "Iris",
                "optifine" => "OptiFine",
                "quilt" => "Quilt",
                "fabric" => "Fabric",
                "forge" => "Forge",
                "neoforge" => "NeoForge",
                _ => string.Empty
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
            4477 => "小型整合包",
            5193 => "数据包",
            4546 => "16x",
            4547 => "32x",
            4548 => "64x+",
            5314 => "写实风",
            5315 => "卡通风",
            5317 => "幻想风",
            6553 => "Iris",
            6554 => "OptiFine",
            _ => string.Empty
        };
    }

    private static string? GetString(JsonObject? obj, string propertyName)
    {
        return obj is not null && obj.TryGetPropertyValue(propertyName, out var value) && value is not null
            ? value.GetValue<string?>()
            : null;
    }

    private static int GetInt(JsonObject? obj, string propertyName)
    {
        if (obj is null || !obj.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var intValue) => intValue,
            JsonValue jsonValue when jsonValue.TryGetValue<long>(out var longValue) => (int)Math.Clamp(longValue, int.MinValue, int.MaxValue),
            _ => 0
        };
    }

    private static bool GetBool(JsonObject? obj, string propertyName)
    {
        if (obj is null || !obj.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            return false;
        }

        return value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue) && boolValue;
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        return node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var rawValue) && DateTimeOffset.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private sealed record CacheEntry<T>(T Value, DateTimeOffset CreatedAt);

    private sealed record RequestCandidate(string Url, bool UseCurseForgeApiKey);
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityProjectService
{

    private const string CompDetailTargetPrefix = "comp-detail:";

    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static readonly ConcurrentDictionary<string, CacheEntry<FrontendCommunityProjectSummary>> SummaryCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, CacheEntry<FrontendCommunityProjectState>> DetailCache = new(StringComparer.OrdinalIgnoreCase);

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

    public static bool TryParseClipboardProjectLink(string? text, out FrontendClipboardCommunityProjectLink link)
    {
        if (string.IsNullOrWhiteSpace(text)
            || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
        {
            link = default!;
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 2
            && IsModrinthHost(uri.Host)
            && TryMapModrinthProjectTypeToRoute(segments[0], out var modrinthRoute))
        {
            link = new FrontendClipboardCommunityProjectLink(
                "Modrinth",
                segments[1],
                modrinthRoute,
                uri.ToString());
            return true;
        }

        if (segments.Length >= 3
            && IsCurseForgeHost(uri.Host)
            && string.Equals(segments[0], "minecraft", StringComparison.OrdinalIgnoreCase)
            && TryMapCurseForgeSectionToRoute(segments[1], out var curseForgeRoute))
        {
            link = new FrontendClipboardCommunityProjectLink(
                "CurseForge",
                segments[2],
                curseForgeRoute,
                uri.ToString());
            return true;
        }

        link = default!;
        return false;
    }

    public static FrontendClipboardCommunityProjectResolution ResolveClipboardProjectLink(
        FrontendClipboardCommunityProjectLink link,
        int communitySourcePreference)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Source switch
        {
            "Modrinth" => ResolveModrinthClipboardProjectLink(link, communitySourcePreference),
            "CurseForge" => ResolveCurseForgeClipboardProjectLink(link, communitySourcePreference),
            _ => throw new InvalidOperationException($"Unsupported community source: {link.Source}")
        };
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
                errors.Add($"Failed to load Modrinth favorite metadata: {ex.Message}");
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
                errors.Add($"Failed to load CurseForge favorite metadata: {ex.Message}");
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
                "No project selected",
                "Open a project from favorites or a resource entry to inspect its details.",
                string.Empty,
                "Unspecified source",
                null,
                null,
                string.Empty,
                "waiting",
                "unknown",
                0,
                0,
                BuildCompatibilitySummary([], []),
                string.Empty,
                [],
                [],
                "The current detail route does not include a project identifier.",
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
                $"Project {normalizedId}",
                "Unable to load this project from a live source.",
                string.Empty,
                IsCurseForgeId(normalizedId) ? "CurseForge" : "Modrinth",
                null,
                null,
                string.Empty,
                "load_failed",
                "unknown",
                0,
                0,
                BuildCompatibilitySummary([], []),
                string.Empty,
                [],
                [],
                BuildToken("warning_load_failed", ex.Message),
                true);
        }

        DetailCache[cacheKey] = new CacheEntry<FrontendCommunityProjectState>(state, DateTimeOffset.UtcNow);
        return state;
    }

}

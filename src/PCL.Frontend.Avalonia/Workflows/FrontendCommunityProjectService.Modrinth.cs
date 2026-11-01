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
        var iconUrl = GetString(project, "icon_url");
        var iconPath = FrontendCommunityIconCache.TryGetCachedIconPath(iconUrl);
        var loaders = NormalizeProjectLoaders(DistinctValues(project["loaders"] as JsonArray));
        var categories = TranslateModrinthCategories(DistinctValues(project["categories"] as JsonArray)
            .Concat(DistinctValues(project["additional_categories"] as JsonArray)));
        var gameVersions = DistinctValues(project["game_versions"] as JsonArray)
            .Select(version => NormalizeMinecraftVersion(version) ?? version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(version => ParseVersion(version))
            .ToArray();
        var versionEntries = versions
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
        var dependencyLookup = BuildModrinthDependencyLookup(versionEntries, communitySourcePreference);
        var releases = versionEntries
            .OrderByDescending(version => MatchesPreferredVersion(version!, normalizedVersion))
            .ThenByDescending(version => ParseDateTimeOffset(version!["date_published"]))
            .Select(version => BuildModrinthReleaseEntry(version!, website, dependencyLookup))
            .Where(entry => entry is not null)
            .Cast<FrontendCommunityProjectReleaseEntry>()
            .ToArray();
        var links = BuildModrinthLinkEntries(project, website);

        return new FrontendCommunityProjectState(
            projectId,
            title,
            string.IsNullOrWhiteSpace(summary) ? "This project does not provide additional summary information." : summary,
            body,
            "Modrinth",
            iconUrl,
            iconPath,
            website,
            TranslateProjectStatus(GetString(project, "status")),
            FormatDateLabel(ParseDateTimeOffset(project["updated"])),
            GetInt(project, "downloads"),
            GetInt(project, "followers"),
            BuildCompatibilitySummary(gameVersions, loaders),
            BuildCategorySummary(categories),
            releases,
            links,
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
            NormalizeWhitespace(GetString(project, "description")) ?? "This project does not provide a summary.",
            "Modrinth",
            null,
            TranslateProjectType(GetString(project, "project_type")),
            BuildModrinthWebsite(project),
            GetString(project, "icon_url"),
            FrontendCommunityIconCache.TryGetCachedIconPath(GetString(project, "icon_url")),
            FormatDateLabel(ParseDateTimeOffset(project["updated"])),
            GetInt(project, "downloads"),
            GetInt(project, "followers"));
    }

    private static FrontendCommunityProjectReleaseEntry? BuildModrinthReleaseEntry(
        JsonObject version,
        string? website,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> dependencyLookup)
    {
        var title = GetString(version, "name") ?? GetString(version, "version_number");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var gameVersions = DistinctValues(version["game_versions"] as JsonArray)
            .Select(value => NormalizeMinecraftVersion(value) ?? value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ParseVersion)
            .ToArray();
        var loaders = NormalizeProjectLoaders(DistinctValues(version["loaders"] as JsonArray));
        var info = BuildReleaseInfo(
            gameVersions,
            loaders);
        var file = (version["files"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => node is not null && GetBool(node, "primary"));
        var hashes = file?["hashes"] as JsonObject;
        var target = GetString(file, "url") ?? website;
        var downloads = GetInt(version, "downloads");
        var publishedAt = ParseDateTimeOffset(version["date_published"]);
        var meta = BuildToken(
            "release_meta",
            FormatDateLabel(publishedAt),
            downloads > 0 ? downloads.ToString() : string.Empty);
        var dependencies = BuildModrinthDependencyEntries(version["dependencies"] as JsonArray, dependencyLookup);

        return new FrontendCommunityProjectReleaseEntry(
            title,
            info,
            meta,
            string.IsNullOrWhiteSpace(target) ? "view_details" : "open_download",
            target,
            GetString(file, "filename"),
            !string.IsNullOrWhiteSpace(GetString(file, "url")),
            gameVersions,
            loaders,
            dependencies,
            publishedAt?.ToUnixTimeSeconds() ?? 0,
            TranslateModrinthReleaseChannel(GetString(version, "version_type")),
            GetString(version, "id"),
            null,
            GetString(hashes, "sha1"),
            GetString(hashes, "sha512"));
    }

    private static FrontendCommunityProjectReleaseChannel TranslateModrinthReleaseChannel(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "alpha" => FrontendCommunityProjectReleaseChannel.Alpha,
            "beta" => FrontendCommunityProjectReleaseChannel.Beta,
            _ => FrontendCommunityProjectReleaseChannel.Release
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

}

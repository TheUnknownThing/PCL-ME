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
        var data = project["data"]?.AsObject() ?? throw new InvalidOperationException("CurseForge returned an empty project payload.");
        var website = GetString(data["links"] as JsonObject, "websiteUrl")
                      ?? BuildCurseForgeWebsite(data);
        var logo = data["logo"] as JsonObject;
        var iconUrl = GetString(logo, "thumbnailUrl") ?? GetString(logo, "url");
        var iconPath = FrontendCommunityIconCache.TryGetCachedIconPath(iconUrl);
        var filesResponse = ReadJsonObject(
            "CurseForge",
            $"https://api.curseforge.com/v1/mods/{projectId}/files?pageSize=10000",
            $"https://mod.mcimirror.top/curseforge/v1/mods/{projectId}/files?pageSize=10000",
            communitySourcePreference,
            officialRequiresApiKey: true,
            postBody: null);
        var fileEntries = (filesResponse["data"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .ToArray();
        var dependencyLookup = BuildCurseForgeDependencyLookup(fileEntries, communitySourcePreference);
        var releases = fileEntries
            .OrderByDescending(file => MatchesPreferredVersionInFile(file!, normalizedVersion))
            .ThenByDescending(file => ParseDateTimeOffset(file!["fileDate"]))
            .Select(file => BuildCurseForgeReleaseEntry(file!, website, dependencyLookup))
            .Where(entry => entry is not null)
            .Cast<FrontendCommunityProjectReleaseEntry>()
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
            GetString(data, "name") ?? $"Project {projectId}",
            NormalizeWhitespace(GetString(data, "summary")) is { Length: > 0 } summary
                ? summary
                : "This project does not provide additional summary information.",
            string.Empty,
            "CurseForge",
            iconUrl,
            iconPath,
            website,
            TranslateCurseForgeStatus(GetInt(data, "status")),
            FormatDateLabel(ParseDateTimeOffset(data["dateModified"]) ?? ParseDateTimeOffset(data["dateReleased"])),
            GetInt(data, "downloadCount"),
            GetInt(data, "thumbsUpCount"),
            BuildCompatibilitySummary(gameVersions, loaders),
            BuildCategorySummary(categories),
            releases,
            links,
            string.Empty,
            false);
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
            NormalizeWhitespace(GetString(project, "summary")) ?? "This project does not provide a summary.",
            "CurseForge",
            authors.Length == 0 ? null : string.Join(" / ", authors),
            TranslateCurseForgeClass(GetInt(project, "classId")),
            GetString(project["links"] as JsonObject, "websiteUrl") ?? BuildCurseForgeWebsite(project),
            GetString(project["logo"] as JsonObject, "thumbnailUrl") ?? GetString(project["logo"] as JsonObject, "url"),
            FrontendCommunityIconCache.TryGetCachedIconPath(GetString(project["logo"] as JsonObject, "thumbnailUrl") ?? GetString(project["logo"] as JsonObject, "url")),
            FormatDateLabel(ParseDateTimeOffset(project["dateModified"]) ?? ParseDateTimeOffset(project["dateReleased"])),
            GetInt(project, "downloadCount"),
            GetInt(project, "thumbsUpCount"));
    }

    private static FrontendCommunityProjectReleaseEntry? BuildCurseForgeReleaseEntry(
        JsonObject file,
        string? website,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> dependencyLookup)
    {
        var title = GetString(file, "displayName") ?? GetString(file, "fileName");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var gameVersions = DistinctValues(file["gameVersions"] as JsonArray)
            .Select(value => NormalizeMinecraftVersion(value) ?? value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(ParseVersion)
            .ToArray();
        var loaders = DistinctValues(file["gameVersions"] as JsonArray)
            .Select(NormalizeLoaderToken)
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var info = BuildReleaseInfo(
            gameVersions,
            loaders);
        var downloads = GetInt(file, "downloadCount");
        var publishedAt = ParseDateTimeOffset(file["fileDate"]);
        var fileName = GetString(file, "fileName");
        var fileId = GetInt(file, "id");
        var downloadUrl = GetString(file, "downloadUrl");
        if (string.IsNullOrWhiteSpace(downloadUrl) && fileId > 0 && !string.IsNullOrWhiteSpace(fileName))
        {
            downloadUrl = BuildCurseForgeMediaUrl(fileId, fileName);
        }
        var meta = BuildToken(
            "release_meta",
            FormatDateLabel(publishedAt),
            downloads > 0 ? downloads.ToString() : string.Empty);
        var dependencies = BuildCurseForgeDependencyEntries(file["dependencies"] as JsonArray, dependencyLookup);
        return new FrontendCommunityProjectReleaseEntry(
            title,
            info,
            meta,
            string.IsNullOrWhiteSpace(downloadUrl) ? (string.IsNullOrWhiteSpace(website) ? "view_details" : "open_project_page") : "open_download",
            downloadUrl ?? website,
            fileName,
            !string.IsNullOrWhiteSpace(downloadUrl),
            gameVersions,
            loaders,
            dependencies,
            publishedAt?.ToUnixTimeSeconds() ?? 0,
            TranslateCurseForgeReleaseChannel(GetInt(file, "releaseType")));
    }

    private static string BuildCurseForgeMediaUrl(int fileId, string fileName)
    {
        return $"https://mediafiles.forgecdn.net/files/{fileId / 1000}/{fileId % 1000:D3}/{Uri.EscapeDataString(fileName)}";
    }

    private static FrontendCommunityProjectReleaseChannel TranslateCurseForgeReleaseChannel(int value)
    {
        return value switch
        {
            3 => FrontendCommunityProjectReleaseChannel.Alpha,
            2 => FrontendCommunityProjectReleaseChannel.Beta,
            _ => FrontendCommunityProjectReleaseChannel.Release
        };
    }

}

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

    private static IReadOnlyDictionary<string, FrontendCommunityProjectSummary> BuildModrinthDependencyLookup(
        IEnumerable<JsonObject> versions,
        int communitySourcePreference)
    {
        var versionList = versions.ToArray();
        foreach (var dependency in versionList.SelectMany(version => (version["dependencies"] as JsonArray ?? []).OfType<JsonObject>()))
        {
            if (!string.IsNullOrWhiteSpace(GetString(dependency, "project_id")) || GetString(dependency, "dependency_type") != "required") continue;
            var versionId = GetString(dependency, "version_id");
            if (string.IsNullOrWhiteSpace(versionId)) throw new InvalidDataException("A required dependency has no resolvable project or version ID.");
            var encoded = Uri.EscapeDataString(versionId);
            var resolved = ReadJsonObject("Modrinth", $"https://api.modrinth.com/v2/version/{encoded}",
                $"https://mod.mcimirror.top/modrinth/v2/version/{encoded}", communitySourcePreference, false, null);
            dependency["project_id"] = GetString(resolved, "project_id") ?? throw new InvalidDataException("The dependency version has no project ID.");
        }
        var projectIds = versionList
            .SelectMany(version => (version["dependencies"] as JsonArray ?? [])
                .Select(node => GetString(node as JsonObject, "project_id")))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projectIds.Length == 0)
        {
            return new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return FetchModrinthSummaries(projectIds, communitySourcePreference)
                .ToDictionary(summary => summary.ProjectId, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, FrontendCommunityProjectSummary> BuildCurseForgeDependencyLookup(
        IEnumerable<JsonObject> files,
        int communitySourcePreference)
    {
        var projectIds = files
            .SelectMany(file => (file["dependencies"] as JsonArray ?? [])
                .Select(node => GetInt(node as JsonObject, "modId"))
                .Where(id => id > 0)
                .Select(id => id.ToString()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projectIds.Length == 0)
        {
            return new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return FetchCurseForgeSummaries(projectIds, communitySourcePreference)
                .ToDictionary(summary => summary.ProjectId, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, FrontendCommunityProjectSummary>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static IReadOnlyList<FrontendCommunityProjectDependencyEntry> BuildModrinthDependencyEntries(
        JsonArray? dependencies,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> lookup)
    {
        if (dependencies is null || dependencies.Count == 0)
        {
            return [];
        }

        return dependencies
            .Select(node => BuildModrinthDependencyEntry(node as JsonObject, lookup))
            .Where(entry => entry is not null)
            .Cast<FrontendCommunityProjectDependencyEntry>()
            .ToArray();
    }

    private static FrontendCommunityProjectDependencyEntry? BuildModrinthDependencyEntry(
        JsonObject? dependency,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> lookup)
    {
        var kind = TranslateModrinthDependencyKind(GetString(dependency, "dependency_type"));
        if (kind is null
            || kind is FrontendCommunityProjectDependencyKind.Incompatible or FrontendCommunityProjectDependencyKind.Broken)
        {
            return null;
        }

        var projectId = GetString(dependency, "project_id");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }
        lookup.TryGetValue(projectId, out var summary);

        return new FrontendCommunityProjectDependencyEntry(
            projectId,
            summary?.Title ?? projectId,
            summary?.Summary ?? "",
            summary is null ? "" : BuildDependencyMeta(summary),
            summary?.IconUrl,
            summary?.IconPath,
            CreateCompDetailTarget(projectId),
            kind.Value,
            GetString(dependency, "version_id"));
    }

    internal static IReadOnlyList<FrontendCommunityProjectDependencyEntry> BuildCurseForgeDependencyEntries(
        JsonArray? dependencies,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> lookup)
    {
        if (dependencies is null || dependencies.Count == 0)
        {
            return [];
        }

        return dependencies
            .Select(node => BuildCurseForgeDependencyEntry(node as JsonObject, lookup))
            .Where(entry => entry is not null)
            .Cast<FrontendCommunityProjectDependencyEntry>()
            .ToArray();
    }

    private static FrontendCommunityProjectDependencyEntry? BuildCurseForgeDependencyEntry(
        JsonObject? dependency,
        IReadOnlyDictionary<string, FrontendCommunityProjectSummary> lookup)
    {
        var kind = TranslateCurseForgeDependencyKind(GetInt(dependency, "relationType"));
        if (kind is null || kind is FrontendCommunityProjectDependencyKind.Incompatible)
        {
            return null;
        }

        var projectId = GetInt(dependency, "modId");
        if (projectId <= 0)
        {
            return null;
        }
        lookup.TryGetValue(projectId.ToString(), out var summary);

        return new FrontendCommunityProjectDependencyEntry(
            projectId.ToString(),
            summary?.Title ?? projectId.ToString(),
            summary?.Summary ?? "",
            summary is null ? "" : BuildDependencyMeta(summary),
            summary?.IconUrl,
            summary?.IconPath,
            CreateCompDetailTarget(projectId.ToString()),
            kind.Value);
    }

    private static FrontendCommunityProjectDependencyKind? TranslateModrinthDependencyKind(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "embedded" => FrontendCommunityProjectDependencyKind.Embedded,
            "optional" => FrontendCommunityProjectDependencyKind.Optional,
            "required" => FrontendCommunityProjectDependencyKind.Required,
            "incompatible" => FrontendCommunityProjectDependencyKind.Incompatible,
            _ => null
        };
    }

    private static FrontendCommunityProjectDependencyKind? TranslateCurseForgeDependencyKind(int value)
    {
        return value switch
        {
            1 => FrontendCommunityProjectDependencyKind.Embedded,
            2 => FrontendCommunityProjectDependencyKind.Optional,
            3 => FrontendCommunityProjectDependencyKind.Required,
            4 => FrontendCommunityProjectDependencyKind.Tool,
            5 => FrontendCommunityProjectDependencyKind.Incompatible,
            6 => FrontendCommunityProjectDependencyKind.Include,
            _ => null
        };
    }

    private static string BuildDependencyMeta(FrontendCommunityProjectSummary summary)
    {
        return BuildToken(
            "dependency_meta",
            summary.Source,
            summary.ProjectType ?? string.Empty,
            summary.Author ?? string.Empty);
    }

}

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

    private static FrontendClipboardCommunityProjectResolution ResolveModrinthClipboardProjectLink(
        FrontendClipboardCommunityProjectLink link,
        int communitySourcePreference)
    {
        var encodedIdentifier = Uri.EscapeDataString(link.Identifier);
        var project = ReadJsonObject(
            "Modrinth",
            $"https://api.modrinth.com/v2/project/{encodedIdentifier}",
            $"https://mod.mcimirror.top/modrinth/v2/project/{encodedIdentifier}",
            communitySourcePreference,
            officialRequiresApiKey: false,
            postBody: null);
        var projectId = GetString(project, "id") ?? throw new InvalidOperationException("Modrinth project is missing a project ID.");
        var title = GetString(project, "title") ?? projectId;
        var route = TryMapModrinthProjectTypeToRoute(GetString(project, "project_type"), out var resolvedRoute)
            ? resolvedRoute
            : link.Route;
        return new FrontendClipboardCommunityProjectResolution(projectId, title, route, link.Source, link.Url);
    }

    private static FrontendClipboardCommunityProjectResolution ResolveCurseForgeClipboardProjectLink(
        FrontendClipboardCommunityProjectLink link,
        int communitySourcePreference)
    {
        var encodedSlug = Uri.EscapeDataString(link.Identifier);
        var response = ReadJsonObject(
            "CurseForge",
            $"https://api.curseforge.com/v1/mods/search?gameId=432&slug={encodedSlug}&pageSize=10",
            $"https://mod.mcimirror.top/curseforge/v1/mods/search?gameId=432&slug={encodedSlug}&pageSize=10",
            communitySourcePreference,
            officialRequiresApiKey: true,
            postBody: null);
        var project = (response["data"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Cast<JsonObject>()
            .OrderByDescending(candidate => MapCurseForgeClassToRoute(GetInt(candidate, "classId")) == link.Route)
            .ThenByDescending(candidate => GetInt(candidate, "downloadCount"))
            .FirstOrDefault()
            ?? throw new InvalidOperationException("CurseForge did not return a matching project.");
        var projectId = GetInt(project, "id");
        if (projectId <= 0)
        {
            throw new InvalidOperationException("CurseForge project is missing a project ID.");
        }

        var title = GetString(project, "name") ?? projectId.ToString();
        var route = MapCurseForgeClassToRoute(GetInt(project, "classId")) ?? link.Route;
        return new FrontendClipboardCommunityProjectResolution(projectId.ToString(), title, route, link.Source, link.Url);
    }

    private static bool IsModrinthHost(string host)
    {
        return string.Equals(host, "modrinth.com", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "www.modrinth.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurseForgeHost(string host)
    {
        return string.Equals(host, "curseforge.com", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "www.curseforge.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapModrinthProjectTypeToRoute(string? projectType, out LauncherFrontendSubpageKey route)
    {
        switch (projectType?.Trim().ToLowerInvariant())
        {
            case "mod":
                route = LauncherFrontendSubpageKey.DownloadMod;
                return true;
            case "modpack":
                route = LauncherFrontendSubpageKey.DownloadPack;
                return true;
            case "resourcepack":
                route = LauncherFrontendSubpageKey.DownloadResourcePack;
                return true;
            case "shader":
                route = LauncherFrontendSubpageKey.DownloadShader;
                return true;
            case "datapack":
                route = LauncherFrontendSubpageKey.DownloadDataPack;
                return true;
            default:
                route = default;
                return false;
        }
    }

    private static bool TryMapCurseForgeSectionToRoute(string? section, out LauncherFrontendSubpageKey route)
    {
        return (route = section?.Trim().ToLowerInvariant() switch
        {
            "mc-mods" => LauncherFrontendSubpageKey.DownloadMod,
            "modpacks" => LauncherFrontendSubpageKey.DownloadPack,
            "texture-packs" => LauncherFrontendSubpageKey.DownloadResourcePack,
            "shaders" => LauncherFrontendSubpageKey.DownloadShader,
            "data-packs" => LauncherFrontendSubpageKey.DownloadDataPack,
            "worlds" => LauncherFrontendSubpageKey.DownloadWorld,
            _ => default
        }) != default;
    }

    private static LauncherFrontendSubpageKey? MapCurseForgeClassToRoute(int classId)
    {
        return classId switch
        {
            6 => LauncherFrontendSubpageKey.DownloadMod,
            4471 => LauncherFrontendSubpageKey.DownloadPack,
            12 => LauncherFrontendSubpageKey.DownloadResourcePack,
            6552 => LauncherFrontendSubpageKey.DownloadShader,
            6945 => LauncherFrontendSubpageKey.DownloadDataPack,
            17 => LauncherFrontendSubpageKey.DownloadWorld,
            _ => null
        };
    }

}

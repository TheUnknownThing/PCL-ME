using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

    private static readonly IReadOnlyList<RouteConfig> RouteConfigs =
    [
        new RouteConfig(LauncherFrontendSubpageKey.DownloadMod, "Mod", "CommandBlock.png", false, "mod", null, 6, "mc-mods"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadPack, "pack", "CommandBlock.png", false, "modpack", null, 4471, "modpacks"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadDataPack, "data_pack", "RedstoneLampOn.png", false, "mod", "datapack", 6945, "data-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadResourcePack, "resource_pack", "Grass.png", false, "resourcepack", null, 12, "texture-packs"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadShader, "shader", "GoldBlock.png", true, "shader", null, 6552, "shaders"),
        new RouteConfig(LauncherFrontendSubpageKey.DownloadWorld, "world", "GrassPath.png", false, null, null, 17, "worlds")
    ];

    private static RouteConfig GetRouteConfig(LauncherFrontendSubpageKey route)
    {
        return RouteConfigs.First(config => config.Route == route);
    }

    private static IReadOnlyList<string> GetSourceOptions(RouteConfig config)
    {
        var options = new List<string>();
        if (config.CurseForgeClassId is not null)
        {
            options.Add("CurseForge");
        }

        if (!string.IsNullOrWhiteSpace(config.ModrinthProjectType))
        {
            options.Add("Modrinth");
        }

        return options;
    }

}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private DownloadResourceFilterOptionViewModel CreateDownloadResourceFilterOption(string label, string filterValue, bool isHeader = false)
    {
        return new DownloadResourceFilterOptionViewModel(
            isHeader ? LocalizeDownloadResourceTagGroup(label) : LocalizeDownloadResourceTag(label),
            filterValue,
            isHeader);
    }
    private string LocalizeDownloadResourceTagGroup(string label)
    {
        return label switch
        {
            "style" => T("download.resource.tag_groups.style"),
            "features" => T("download.resource.tag_groups.features"),
            "resolution" => T("download.resource.tag_groups.resolution"),
            "performance" => T("download.resource.tag_groups.performance"),
            _ => label
        };
    }

    private string LocalizeDownloadResourceTag(string tag)
    {
        return tag switch
        {
            "all" => T("common.filters.all"),
            "CurseForge" => "CurseForge",
            "Modrinth" => "Modrinth",
            "Iris" => "Iris",
            "OptiFine" => "OptiFine",
            "FTB" => "FTB",
            "PBR" => "PBR",
            "mod" => T("download.resource.search.mod"),
            "pack" => T("download.resource.search.pack"),
            "resource_pack" => T("download.resource.search.resource_pack"),
            "shader" => T("download.resource.search.shader"),
            "world" => T("download.resource.search.world"),
            "worldgen" => T("download.resource.tags.worldgen"),
            "biomes" => T("download.resource.tags.biomes"),
            "dimensions" => T("download.resource.tags.dimensions"),
            "ores_resources" => T("download.resource.tags.ores_resources"),
            "structures" => T("download.resource.tags.structures"),
            "technology" => T("download.resource.tags.technology"),
            "logistics" => T("download.resource.tags.logistics"),
            "automation" => T("download.resource.tags.automation"),
            "energy" => T("download.resource.tags.energy"),
            "redstone" => T("download.resource.tags.redstone"),
            "food_cooking" => T("download.resource.tags.food_cooking"),
            "farming" => T("download.resource.tags.farming"),
            "game_mechanics" => T("download.resource.tags.game_mechanics"),
            "transportation" => T("download.resource.tags.transportation"),
            "storage" => T("download.resource.tags.storage"),
            "magic" => T("download.resource.tags.magic"),
            "adventure" => T("download.resource.tags.adventure"),
            "decoration" => T("download.resource.tags.decoration"),
            "mobs" => T("download.resource.tags.mobs"),
            "utility" => T("download.resource.tags.utility"),
            "equipment_tools" => T("download.resource.tags.equipment_tools"),
            "creative_mode" => T("download.resource.tags.creative_mode"),
            "performance" => T("download.resource.tags.performance"),
            "information" => T("download.resource.tags.information"),
            "multiplayer" => T("download.resource.tags.multiplayer"),
            "library" => T("download.resource.tags.library"),
            "hardcore" => T("download.resource.tags.hardcore"),
            "combat" => T("download.resource.tags.combat"),
            "quests" => T("download.resource.tags.quests"),
            "kitchen_sink" => T("download.resource.tags.kitchen_sink"),
            "exploration" => T("download.resource.tags.exploration"),
            "minigames" => T("download.resource.tags.minigames"),
            "scifi" => T("download.resource.tags.scifi"),
            "skyblock" => T("download.resource.tags.skyblock"),
            "vanilla_plus" => T("download.resource.tags.vanilla_plus"),
            "map_based" => T("download.resource.tags.map_based"),
            "lightweight" => T("download.resource.tags.lightweight"),
            "large" => T("download.resource.tags.large"),
            "fantasy" => T("download.resource.tags.fantasy"),
            "modded" => T("download.resource.tags.modded"),
            "vanilla_style" => T("download.resource.tags.vanilla_style"),
            "realistic" => T("download.resource.tags.realistic"),
            "modern" => T("download.resource.tags.modern"),
            "medieval" => T("download.resource.tags.medieval"),
            "steampunk" => T("download.resource.tags.steampunk"),
            "themed" => T("download.resource.tags.themed"),
            "simple" => T("download.resource.tags.simple"),
            "tweaks" => T("download.resource.tags.tweaks"),
            "chaotic" => T("download.resource.tags.chaotic"),
            "entities" => T("download.resource.tags.entities"),
            "audio" => T("download.resource.tags.audio"),
            "fonts" => T("download.resource.tags.fonts"),
            "models" => T("download.resource.tags.models"),
            "locale" => T("download.resource.tags.locale"),
            "gui" => T("download.resource.tags.gui"),
            "core_shaders" => T("download.resource.tags.core_shaders"),
            "dynamic_effects" => T("download.resource.tags.dynamic_effects"),
            "mod_compatible" => T("download.resource.tags.mod_compatible"),
            "resolution_8x" => T("download.resource.tags.resolution_8x"),
            "16x" => T("download.resource.tags.resolution_16x"),
            "32x" => T("download.resource.tags.resolution_32x"),
            "48x" => T("download.resource.tags.resolution_48x"),
            "64x" => T("download.resource.tags.resolution_64x"),
            "128x" => T("download.resource.tags.resolution_128x"),
            "256x" => T("download.resource.tags.resolution_256x"),
            "resolution_512x" => T("download.resource.tags.resolution_512x"),
            "fantasy_style" => T("download.resource.tags.fantasy_style"),
            "semi_realistic" => T("download.resource.tags.semi_realistic"),
            "cartoon" => T("download.resource.tags.cartoon"),
            "colored_lighting" => T("download.resource.tags.colored_lighting"),
            "path_tracing" => T("download.resource.tags.path_tracing"),
            "reflections" => T("download.resource.tags.reflections"),
            "performance_very_low" => T("download.resource.tags.performance_very_low"),
            "performance_low" => T("download.resource.tags.performance_low"),
            "performance_medium" => T("download.resource.tags.performance_medium"),
            "performance_high" => T("download.resource.tags.performance_high"),
            "creative" => T("download.resource.tags.creative"),
            "parkour" => T("download.resource.tags.parkour"),
            "puzzle" => T("download.resource.tags.puzzle"),
            "survival" => T("download.resource.tags.survival"),
            "mod_world" => T("download.resource.tags.mod_world"),
            "vanilla_compatible" => T("download.resource.tags.vanilla_compatible"),
            "data_pack" => T("download.resource.tags.data_pack"),
            _ => tag
        };
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildFallbackDownloadResourceTagOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadMod => BuildModTagOptions(),
            LauncherFrontendSubpageKey.DownloadPack => BuildPackTagOptions(),
            LauncherFrontendSubpageKey.DownloadDataPack => BuildDataPackTagOptions(),
            LauncherFrontendSubpageKey.DownloadResourcePack => BuildResourcePackTagOptions(),
            LauncherFrontendSubpageKey.DownloadShader => BuildShaderTagOptions(),
            LauncherFrontendSubpageKey.DownloadWorld => BuildWorldTagOptions(),
            _ => [CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty)]
        };
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildModTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("worldgen", "worldgen"),
            CreateDownloadResourceFilterOption("biomes", "biomes"),
            CreateDownloadResourceFilterOption("dimensions", "dimensions"),
            CreateDownloadResourceFilterOption("ores_resources", "ores_resources"),
            CreateDownloadResourceFilterOption("structures", "structures"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("logistics", "logistics"),
            CreateDownloadResourceFilterOption("automation", "automation"),
            CreateDownloadResourceFilterOption("energy", "energy"),
            CreateDownloadResourceFilterOption("redstone", "redstone"),
            CreateDownloadResourceFilterOption("food_cooking", "food_cooking"),
            CreateDownloadResourceFilterOption("farming", "farming"),
            CreateDownloadResourceFilterOption("game_mechanics", "game_mechanics"),
            CreateDownloadResourceFilterOption("transportation", "transportation"),
            CreateDownloadResourceFilterOption("storage", "storage"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("mobs", "mobs"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("equipment_tools", "equipment_tools"),
            CreateDownloadResourceFilterOption("creative_mode", "creative_mode"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("information", "information"),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("library", "library")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildPackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("hardcore", "hardcore"),
            CreateDownloadResourceFilterOption("combat", "combat"),
            CreateDownloadResourceFilterOption("quests", "quests"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("kitchen_sink", "kitchen_sink"),
            CreateDownloadResourceFilterOption("exploration", "exploration"),
            CreateDownloadResourceFilterOption("minigames", "minigames"),
            CreateDownloadResourceFilterOption("scifi", "scifi"),
            CreateDownloadResourceFilterOption("skyblock", "skyblock"),
            CreateDownloadResourceFilterOption("vanilla_plus", "vanilla_plus"),
            CreateDownloadResourceFilterOption("FTB", "FTB"),
            CreateDownloadResourceFilterOption("map_based", "map_based"),
            CreateDownloadResourceFilterOption("lightweight", "lightweight"),
            CreateDownloadResourceFilterOption("large", "large")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildDataPackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("worldgen", "worldgen"),
            CreateDownloadResourceFilterOption("technology", "technology"),
            CreateDownloadResourceFilterOption("game_mechanics", "game_mechanics"),
            CreateDownloadResourceFilterOption("transportation", "transportation"),
            CreateDownloadResourceFilterOption("storage", "storage"),
            CreateDownloadResourceFilterOption("magic", "magic"),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("fantasy", "fantasy"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("mobs", "mobs"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("equipment_tools", "equipment_tools"),
            CreateDownloadResourceFilterOption("performance", "performance"),
            CreateDownloadResourceFilterOption("multiplayer", "multiplayer"),
            CreateDownloadResourceFilterOption("library", "library"),
            CreateDownloadResourceFilterOption("modded", "modded")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildResourcePackTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("style", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("vanilla_style", "vanilla_style"),
            CreateDownloadResourceFilterOption("realistic", "realistic"),
            CreateDownloadResourceFilterOption("modern", "modern"),
            CreateDownloadResourceFilterOption("medieval", "medieval"),
            CreateDownloadResourceFilterOption("steampunk", "steampunk"),
            CreateDownloadResourceFilterOption("themed", "themed"),
            CreateDownloadResourceFilterOption("simple", "simple"),
            CreateDownloadResourceFilterOption("decoration", "decoration"),
            CreateDownloadResourceFilterOption("combat", "combat"),
            CreateDownloadResourceFilterOption("utility", "utility"),
            CreateDownloadResourceFilterOption("tweaks", "tweaks"),
            CreateDownloadResourceFilterOption("chaotic", "chaotic"),
            CreateDownloadResourceFilterOption("features", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("entities", "entities"),
            CreateDownloadResourceFilterOption("audio", "audio"),
            CreateDownloadResourceFilterOption("fonts", "fonts"),
            CreateDownloadResourceFilterOption("models", "models"),
            CreateDownloadResourceFilterOption("locale", "locale"),
            CreateDownloadResourceFilterOption("gui", "gui"),
            CreateDownloadResourceFilterOption("core_shaders", "core_shaders"),
            CreateDownloadResourceFilterOption("dynamic_effects", "dynamic_effects"),
            CreateDownloadResourceFilterOption("mod_compatible", "mod_compatible"),
            CreateDownloadResourceFilterOption("resolution", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("resolution_8x", "resolution_8x"),
            CreateDownloadResourceFilterOption("16x", "16x"),
            CreateDownloadResourceFilterOption("32x", "32x"),
            CreateDownloadResourceFilterOption("48x", "48x"),
            CreateDownloadResourceFilterOption("64x", "64x"),
            CreateDownloadResourceFilterOption("128x", "128x"),
            CreateDownloadResourceFilterOption("256x", "256x"),
            CreateDownloadResourceFilterOption("resolution_512x", "resolution_512x")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildShaderTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("style", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("vanilla_style", "vanilla_style"),
            CreateDownloadResourceFilterOption("fantasy_style", "fantasy_style"),
            CreateDownloadResourceFilterOption("realistic", "realistic"),
            CreateDownloadResourceFilterOption("semi_realistic", "semi_realistic"),
            CreateDownloadResourceFilterOption("cartoon", "cartoon"),
            CreateDownloadResourceFilterOption("features", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("colored_lighting", "colored_lighting"),
            CreateDownloadResourceFilterOption("path_tracing", "path_tracing"),
            CreateDownloadResourceFilterOption("PBR", "PBR"),
            CreateDownloadResourceFilterOption("reflections", "reflections"),
            CreateDownloadResourceFilterOption("performance", string.Empty, isHeader: true),
            CreateDownloadResourceFilterOption("performance_very_low", "performance_very_low"),
            CreateDownloadResourceFilterOption("performance_low", "performance_low"),
            CreateDownloadResourceFilterOption("performance_medium", "performance_medium"),
            CreateDownloadResourceFilterOption("performance_high", "performance_high")
        ];
    }

    private IReadOnlyList<DownloadResourceFilterOptionViewModel> BuildWorldTagOptions()
    {
        return
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("adventure", "adventure"),
            CreateDownloadResourceFilterOption("creative", "creative"),
            CreateDownloadResourceFilterOption("minigames", "minigames"),
            CreateDownloadResourceFilterOption("parkour", "parkour"),
            CreateDownloadResourceFilterOption("puzzle", "puzzle"),
            CreateDownloadResourceFilterOption("survival", "survival"),
            CreateDownloadResourceFilterOption("mod_world", "mod_world")
        ];
    }
}

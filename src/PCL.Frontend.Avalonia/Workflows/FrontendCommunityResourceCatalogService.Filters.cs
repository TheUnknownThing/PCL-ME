using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityResourceCatalogService
{

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
            parts.Add($"version_fallback|{preferredVersion}|{config.Title}");
        }

        if (sourceErrors.Count > 0)
        {
            parts.Add(hasEntries
                ? $"partial_results|{string.Join(" ; ", sourceErrors)}"
                : $"unavailable|{string.Join(" ; ", sourceErrors)}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static IReadOnlyList<FrontendDownloadResourceFilterOption> BuildTagOptions(
        IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return
        [
            new FrontendDownloadResourceFilterOption("all", string.Empty),
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

    private static IEnumerable<FrontendDownloadResourceEntry> ApplyFinalClientSideFilters(
        IEnumerable<FrontendDownloadResourceEntry> entries,
        FrontendCommunityResourceQuery query)
    {
        var filtered = entries;

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filtered = filtered.Where(entry => entry.Tags.Any(tag => string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filtered = filtered.Where(entry =>
                entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Info.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)
                || entry.Tags.Any(tag => tag.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Version))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Version, query.Version, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedVersions.Any(version => string.Equals(version, query.Version, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Loader))
        {
            filtered = filtered.Where(entry =>
                string.Equals(entry.Loader, query.Loader, StringComparison.OrdinalIgnoreCase)
                || entry.SupportedLoaders.Any(loader => string.Equals(loader, query.Loader, StringComparison.OrdinalIgnoreCase)));
        }

        return query.Sort switch
        {
            "downloads" => filtered.OrderByDescending(entry => entry.DownloadCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "follows" => filtered.OrderByDescending(entry => entry.FollowCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "release" => filtered.OrderByDescending(entry => entry.ReleaseRank).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "update" => filtered.OrderByDescending(entry => entry.UpdateRank).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            "relevance" => filtered.OrderByDescending(entry => entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(entry => entry.DownloadCount)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => string.IsNullOrWhiteSpace(query.SearchText)
                ? filtered.OrderByDescending(entry => entry.DownloadCount).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(entry => entry.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(entry => entry.DownloadCount)
                    .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
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
                return "vanilla_compatible";
            }

            return string.Empty;
        }

        return categorySet.Contains("neoforge") ? "NeoForge"
            : categorySet.Contains("forge") ? "Forge"
            : categorySet.Contains("fabric") ? "Fabric"
            : categorySet.Contains("quilt") ? "Quilt"
            : string.Empty;
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

        if (tagSet.Contains("vanilla_compatible"))
        {
            return "vanilla_compatible";
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveSupportedLoaders(IEnumerable<string> rawValues, bool useShaderLoaderOptions)
    {
        var values = rawValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaders = new List<string>();

        if (useShaderLoaderOptions)
        {
            if (values.Contains("vanilla") || values.Contains("vanilla_compatible"))
            {
                loaders.Add("vanilla_compatible");
            }

            if (values.Contains("optifine") || values.Contains("OptiFine"))
            {
                loaders.Add("OptiFine");
            }

            if (values.Contains("iris") || values.Contains("Iris"))
            {
                loaders.Add("Iris");
            }

            return loaders;
        }

        if (values.Contains("forge") || values.Contains("Forge"))
        {
            loaders.Add("Forge");
        }

        if (values.Contains("neoforge") || values.Contains("NeoForge"))
        {
            loaders.Add("NeoForge");
        }

        if (values.Contains("fabric") || values.Contains("Fabric"))
        {
            loaders.Add("Fabric");
        }

        if (values.Contains("quilt") || values.Contains("Quilt"))
        {
            loaders.Add("Quilt");
        }

        return loaders;
    }

    private static string? MapTagToModrinthCategory(LauncherFrontendSubpageKey route, string tag)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => tag switch
            {
                "worldgen" => "worldgen",
                "technology" => "technology",
                "game_mechanics" => "game-mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "magic" => "magic",
                "adventure" => "adventure",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "utility" => "utility",
                "equipment_tools" => "equipment",
                "performance" => "optimization",
                "multiplayer" => "social",
                "library" => "library",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "multiplayer" => "multiplayer",
                "performance" => "optimization",
                "hardcore" => "challenging",
                "combat" => "combat",
                "quests" => "quests",
                "technology" => "technology",
                "magic" => "magic",
                "adventure" => "adventure",
                "kitchen_sink" => "kitchen-sink",
                "lightweight" => "lightweight",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "worldgen" => "worldgen",
                "technology" => "technology",
                "game_mechanics" => "game-mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "magic" => "magic",
                "adventure" => "adventure",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "utility" => "utility",
                "performance" => "optimization",
                "multiplayer" => "social",
                "library" => "library",
                "modded" => "modded",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "vanilla_style" => "vanilla-like",
                "realistic" => "realistic",
                "simple" => "simplistic",
                "combat" => "combat",
                "tweaks" => "tweaks",
                "audio" => "audio",
                "fonts" => "fonts",
                "models" => "models",
                "locale" => "locale",
                "gui" => "gui",
                "core_shaders" => "core-shaders",
                "mod_compatible" => "modded",
                "16x" => "16x",
                "32x" => "32x",
                "48x" => "48x",
                "64x" => "64x",
                "128x" => "128x",
                "256x" => "256x",
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadShader => tag switch
            {
                "vanilla_style" => "vanilla-like",
                "fantasy_style" => "fantasy",
                "realistic" => "realistic",
                "semi_realistic" => "semi-realistic",
                "cartoon" => "cartoon",
                "colored_lighting" => "colored-lighting",
                "path_tracing" => "path-tracing",
                "PBR" => "pbr",
                "reflections" => "reflections",
                _ => null
            },
            _ => null
        };
    }

    private static int? MapTagToCurseForgeCategory(LauncherFrontendSubpageKey route, string tag)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => tag switch
            {
                "worldgen" => 406,
                "technology" => 412,
                "game_mechanics" => 423,
                "transportation" => 414,
                "storage" => 420,
                "magic" => 419,
                "adventure" => 422,
                "decoration" => 424,
                "mobs" => 411,
                "performance" => 6814,
                "multiplayer" => 435,
                "library" => 421,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadPack => tag switch
            {
                "multiplayer" => 4484,
                "hardcore" => 4479,
                "combat" => 4483,
                "quests" => 4478,
                "technology" => 4472,
                "magic" => 4473,
                "adventure" => 4475,
                "exploration" => 4476,
                "minigames" => 4477,
                "skyblock" => 4736,
                "vanilla_plus" => 5128,
                "FTB" => 4487,
                "map_based" => 4480,
                "lightweight" => 4481,
                "large" => 4482,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadDataPack => tag switch
            {
                "adventure" => 6948,
                "fantasy" => 6949,
                "library" => 6950,
                "magic" => 6952,
                "modded" => 6946,
                "technology" => 6951,
                "utility" => 6953,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadResourcePack => tag switch
            {
                "vanilla_style" => 403,
                "realistic" => 400,
                "modern" => 401,
                "medieval" => 402,
                "steampunk" => 399,
                "fonts" => 5244,
                "dynamic_effects" => 404,
                "mod_compatible" => 4465,
                "16x" => 393,
                "32x" => 394,
                "64x" => 395,
                "128x" => 396,
                "256x" => 397,
                "resolution_512x" => 398,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadShader => tag switch
            {
                "realistic" => 6553,
                "fantasy_style" => 6554,
                "vanilla_style" => 6555,
                _ => null
            },
            LauncherFrontendSubpageKey.DownloadWorld => tag switch
            {
                "adventure" => 248,
                "creative" => 249,
                "minigames" => 250,
                "parkour" => 251,
                "puzzle" => 252,
                "survival" => 253,
                "mod_world" => 4464,
                _ => null
            },
            _ => null
        };
    }

    private static string? MapLoaderToModrinthCategory(LauncherFrontendSubpageKey route, string loader)
    {
        if (string.IsNullOrWhiteSpace(loader))
        {
            return null;
        }

        return route == LauncherFrontendSubpageKey.DownloadShader
            ? loader switch
            {
                "Iris" => "iris",
                "OptiFine" => "optifine",
                "vanilla_compatible" => "vanilla",
                _ => null
            }
            : loader switch
            {
                "Forge" => "forge",
                "NeoForge" => "neoforge",
                "Fabric" => "fabric",
                "Quilt" => "quilt",
                _ => null
            };
    }

    private static int? MapLoaderToCurseForgeLoaderType(LauncherFrontendSubpageKey route, string loader)
    {
        if (route == LauncherFrontendSubpageKey.DownloadShader || string.IsNullOrWhiteSpace(loader))
        {
            return null;
        }

        return loader switch
        {
            "Forge" => 1,
            "Fabric" => 4,
            "Quilt" => 5,
            "NeoForge" => 6,
            _ => null
        };
    }

}

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

    private static string BuildToken(string prefix, params string?[] values)
    {
        return string.Join(
            "|",
            new[] { prefix }.Concat(values.Select(value => Uri.EscapeDataString(value ?? string.Empty))));
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

    private static string BuildCompatibilitySummary(
        IReadOnlyList<string> versions,
        IReadOnlyList<string> loaders)
    {
        return BuildToken(
            "compatibility",
            string.Join(",", versions.Take(6)),
            versions.Count > 6 ? "1" : string.Empty,
            string.Join(",", loaders));
    }

    private static string BuildCategorySummary(IReadOnlyList<string> categories)
    {
        return string.Join("/", categories.Take(10));
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
        return BuildToken(
            "release_info",
            string.Join(",", normalizedVersions.Take(4)),
            normalizedVersions.Length > 4 ? "1" : string.Empty,
            string.Join(",", loaders.Take(4)));
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
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown";
    }

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}\u4EBF",
            >= 10_000 => $"{value / 10_000d:0.#}\u4E07",
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

    private static IReadOnlyList<string> NormalizeProjectLoaders(IEnumerable<string> rawValues)
    {
        return rawValues
            .Select(NormalizeProjectLoader)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeProjectLoader(string rawValue)
    {
        return rawValue.Trim().ToLowerInvariant() switch
        {
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "fabric" => "Fabric",
            "quilt" => "Quilt",
            "optifine" => "OptiFine",
            "iris" => "Iris",
            _ => rawValue.Trim()
        };
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
        return projectType?.Trim().ToLowerInvariant() switch
        {
            "mod" => "mod",
            "modpack" => "modpack",
            "resourcepack" => "resource_pack",
            "shader" => "shader",
            "datapack" => "data_pack",
            _ => string.Empty
        };
    }

    private static string TranslateCurseForgeClass(int classId)
    {
        return classId switch
        {
            6 => "mod",
            12 => "resource_pack",
            17 => "world",
            4471 => "modpack",
            6552 => "shader",
            6945 => "data_pack",
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
        return status?.Trim().ToLowerInvariant() switch
        {
            "approved" or "listed" => "published",
            "unlisted" => "unlisted",
            "archived" => "archived",
            "draft" => "draft",
            _ => string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim()
        };
    }

    private static string TranslateCurseForgeStatus(int status)
    {
        return status switch
        {
            4 => "published",
            5 => "removed",
            _ => status == 0 ? "unknown" : $"status_{status}"
        };
    }

    private static IReadOnlyList<string> TranslateModrinthCategories(IEnumerable<string> categories)
    {
        return categories
            .Select(category => category switch
            {
                "technology" => "technology",
                "magic" => "magic",
                "adventure" => "adventure",
                "utility" => "utility",
                "optimization" => "performance",
                "vanilla-like" => "vanilla_style",
                "realistic" => "realistic",
                "worldgen" => "worldgen",
                "food" => "food_cooking",
                "game-mechanics" => "game_mechanics",
                "transportation" => "transportation",
                "storage" => "storage",
                "decoration" => "decoration",
                "mobs" => "mobs",
                "equipment" => "equipment_tools",
                "social" => "server",
                "library" => "library",
                "multiplayer" => "multiplayer",
                "challenging" => "hardcore",
                "combat" => "combat",
                "quests" => "quests",
                "kitchen-sink" => "kitchen_sink",
                "lightweight" => "lightweight",
                "simplistic" => "simple",
                "tweaks" => "tweaks",
                "datapack" => "data_pack",
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
            406 => "worldgen",
            407 => "biomes",
            410 => "dimensions",
            408 => "ores_resources",
            409 => "structures",
            412 => "technology",
            415 => "pipes_logistics",
            4843 => "automation",
            417 => "energy",
            4558 => "redstone",
            436 => "food_cooking",
            416 => "farming",
            414 => "transportation",
            420 => "storage",
            419 => "magic",
            422 => "adventure",
            424 => "decoration",
            411 => "mobs",
            434 => "equipment_tools",
            6814 => "performance",
            423 => "information",
            435 => "server",
            5191 => "tweaks",
            421 => "library",
            4484 => "multiplayer",
            4479 => "hardcore",
            4483 => "combat",
            4478 => "quests",
            4472 => "technology",
            4473 => "magic",
            4475 => "adventure",
            4476 => "exploration",
            4477 => "small_modpack",
            5193 => "data_pack",
            4546 => "16x",
            4547 => "32x",
            4548 => "64x+",
            5314 => "realistic",
            5315 => "cartoon",
            5317 => "fantasy",
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

}

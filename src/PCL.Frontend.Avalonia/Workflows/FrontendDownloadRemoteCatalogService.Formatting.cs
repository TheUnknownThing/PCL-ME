using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendDownloadRemoteCatalogService
{
    private static IReadOnlyList<FrontendDownloadCatalogSection> BuildGroupedInstallerSections<T>(
        IEnumerable<T> items,
        Func<T, string> groupKeySelector,
        Func<IGrouping<string, T>, string> groupTitleSelector,
        Func<T, FrontendDownloadCatalogEntry> entrySelector,
        II18nService? i18n = null)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(groupKeySelector(item)))
            .GroupBy(groupKeySelector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupEntries = group.Select(entrySelector).ToArray();
                return new FrontendDownloadCatalogSection(
                    $"{groupTitleSelector(group)} ({groupEntries.Length})",
                    EnsureEntries(groupEntries, Text(i18n, "download.catalog.remote.empty.group_entries", "There are no available entries for {group_name}.", ("group_name", group.Key)), i18n),
                    IsCollapsible: true,
                    IsInitiallyExpanded: false);
            })
            .ToArray();
    }

    private static FrontendDownloadCatalogEntry CreateInstallerDownloadEntry(
        string title,
        string info,
        string? targetUrl,
        string? suggestedFileName = null)
    {
        return new FrontendDownloadCatalogEntry(
            title,
            info,
            string.Empty,
            "save_installer",
            targetUrl,
            FrontendDownloadCatalogEntryActionKind.DownloadFile,
            suggestedFileName);
    }
    private static string? DeriveFileNameFromUrl(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return null;
        }

        return Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.LocalPath)
            : null;
    }

    private static IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> BuildFailureStates(string error, II18nService? i18n = null)
    {
        return CatalogRoutes.ToDictionary(
            route => route,
            route => BuildFailureState(route, error, i18n));
    }

    private static FrontendDownloadCatalogState BuildFailureState(LauncherFrontendSubpageKey route, string error, II18nService? i18n = null)
    {
        var descriptor = GetGoldCatalogDescriptor(route, i18n);
        return new FrontendDownloadCatalogState(
            descriptor.IntroTitle,
            descriptor.IntroBody,
            descriptor.LoadingText,
            descriptor.Actions,
            [],
            LoadError: error);
    }

    private static string ClassifyClientCategory(JsonObject version)
    {
        var type = version["type"]?.GetValue<string>() ?? string.Empty;
        var id = version["id"]?.GetValue<string>() ?? string.Empty;
        switch (type.ToLowerInvariant())
        {
            case "release":
                return "release";
            case "snapshot":
            case "pending":
                if (id.StartsWith("1.", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("combat", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("rc", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("experimental", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(id, "1.2", StringComparison.OrdinalIgnoreCase)
                    && !id.Contains("pre", StringComparison.OrdinalIgnoreCase))
                {
                    return "release";
                }

                return IsAprilFoolsVersion(id, version["releaseTime"]?.GetValue<string>()) ? "april_fools" : "preview";
            case "special":
                return "april_fools";
            default:
                return "legacy";
        }
    }

    private static string BuildClientVersionInfo(JsonObject version, II18nService? i18n = null)
    {
        var id = NormalizeClientVersionTitle(version["id"]?.GetValue<string>(), i18n);
        var foolName = GetClientAprilFoolsName(id, i18n);
        if (!string.IsNullOrWhiteSpace(foolName))
        {
            return foolName;
        }

        return FormatReleaseTime(i18n, version["releaseTime"]?.GetValue<string>());
    }

    private static bool IsAprilFoolsVersion(string id, string? releaseTime)
    {
        if (!string.IsNullOrWhiteSpace(GetClientAprilFoolsName(id, null)))
        {
            return true;
        }

        var releaseMoment = ParseReleaseMoment(releaseTime);
        return releaseMoment != DateTimeOffset.MinValue
               && releaseMoment.UtcDateTime.AddHours(2).Month == 4
               && releaseMoment.UtcDateTime.AddHours(2).Day == 1;
    }

    private static string NormalizeClientVersionTitle(string? id, II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Text(i18n, "download.catalog.remote.labels.unknown_version", "Unknown version");
        }

        return id switch
        {
            "2point0_blue" => "2.0_blue",
            "2point0_red" => "2.0_red",
            "2point0_purple" => "2.0_purple",
            "20w14infinite" => "20w14∞",
            _ => id
        };
    }

    private static string GetClientAprilFoolsName(string? name, II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.ToLowerInvariant();
        if (normalized.StartsWith("2.0", StringComparison.Ordinal) || normalized.StartsWith("2point0", StringComparison.Ordinal))
        {
            var tag = normalized.EndsWith("red", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_red", " (Red variant)")
                : normalized.EndsWith("blue", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_blue", " (Blue variant)")
                : normalized.EndsWith("purple", StringComparison.Ordinal) ? Text(i18n, "download.catalog.remote.labels.variant_purple", " (Purple variant)")
                : string.Empty;
            return Text(i18n, "download.install.choices.summaries.april_fools.2013", "2013 | This secret update, planned for two years, took the game to a new level!") + tag;
        }

        return normalized switch
        {
            "15w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2015", "2015 | As a game for all ages, we need peace, love, and hugs."),
            "1.rv-pre1" => Text(i18n, "download.install.choices.summaries.april_fools.2016", "2016 | It's time to bring modern technology into Minecraft!"),
            "3d shareware v1.34" => Text(i18n, "download.install.choices.summaries.april_fools.2019", "2019 | We found this masterpiece from 1994 in the ruins of a basement!"),
            "20w14infinite" or "20w14∞" => Text(i18n, "download.install.choices.summaries.april_fools.2020", "2020 | We added 2 billion new dimensions and turned infinite imagination into reality!"),
            "22w13oneblockatatime" => Text(i18n, "download.install.choices.summaries.april_fools.2022", "2022 | One block at a time! Meet new digging, crafting, and riding gameplay."),
            "23w13a_or_b" => Text(i18n, "download.install.choices.summaries.april_fools.2023", "2023 | Research shows players like making choices, and the more the better!"),
            "24w14potato" => Text(i18n, "download.install.choices.summaries.april_fools.2024", "2024 | Poisonous potatoes have always been ignored and underestimated, so we supercharged them!"),
            "25w14craftmine" => Text(i18n, "download.install.choices.summaries.april_fools.2025", "2025 | You can craft anything, including your world itself!"),
            "26w14a" => Text(i18n, "download.install.choices.summaries.april_fools.2026", "2026 | Why do you need an inventory? Let the blocks follow you instead!"),
            _ => string.Empty
        };
    }

    private static GoldCatalogDescriptor GetGoldCatalogDescriptor(LauncherFrontendSubpageKey route, II18nService? i18n = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => new GoldCatalogDescriptor(string.Empty, string.Empty, "fetch_versions", []),
            LauncherFrontendSubpageKey.DownloadForge => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://files.minecraftforge.net", true))),
            LauncherFrontendSubpageKey.DownloadNeoForge => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://neoforged.net/", true))),
            LauncherFrontendSubpageKey.DownloadFabric => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.fabricmc.net", true))),
            LauncherFrontendSubpageKey.DownloadLegacyFabric => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://legacyfabric.net/", true))),
            LauncherFrontendSubpageKey.DownloadQuilt => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://quiltmc.org", true))),
            LauncherFrontendSubpageKey.DownloadOptiFine => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.optifine.net/", true))),
            LauncherFrontendSubpageKey.DownloadLiteLoader => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://www.liteloader.com", true))),
            LauncherFrontendSubpageKey.DownloadLabyMod => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://labymod.net", true))),
            LauncherFrontendSubpageKey.DownloadCleanroom => new GoldCatalogDescriptor(
                string.Empty,
                string.Empty,
                "fetch_list",
                CreateActions(new FrontendDownloadCatalogAction("open_website", "https://cleanroommc.com/", true))),
            _ => new GoldCatalogDescriptor(string.Empty, string.Empty, "fetch_list", [])
        };
    }

    private static IReadOnlyList<FrontendDownloadCatalogAction> CreateActions(params FrontendDownloadCatalogAction[] actions)
    {
        return actions.Where(action => !string.IsNullOrWhiteSpace(action.Target)).ToArray();
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> EnsureEntries(
        IReadOnlyList<FrontendDownloadCatalogEntry> entries,
        string emptyMessage,
        II18nService? i18n = null)
    {
        return entries.Count > 0
            ? entries
            : [new FrontendDownloadCatalogEntry(Text(i18n, "download.catalog.remote.labels.no_display_data", "Nothing to display"), emptyMessage, string.Empty, "view_details", null)];
    }

    private static string Text(
        II18nService? i18n,
        string key,
        string fallback,
        params (string Key, object? Value)[] args)
    {
        if (i18n is null)
        {
            return ApplyFallbackArgs(fallback, args);
        }

        if (args.Length == 0)
        {
            return i18n.T(key);
        }

        return i18n.T(
            key,
            args.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }

    private static string ApplyFallbackArgs(string fallback, IReadOnlyList<(string Key, object? Value)> args)
    {
        var result = fallback;
        foreach (var (key, value) in args)
        {
            result = result.Replace("{" + key + "}", value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
    private static string NormalizeMinecraftVersion(string? preferredMinecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredMinecraftVersion))
        {
            return string.Empty;
        }

        var value = preferredMinecraftVersion.Trim();
        return value.Any(char.IsDigit) ? value : string.Empty;
    }

    private static string FormatReleaseTime(II18nService? i18n, string? value)
    {
        var moment = ParseReleaseMoment(value);
        return moment == DateTimeOffset.MinValue
            ? Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable")
            : moment.LocalDateTime.ToString("yyyy/MM/dd HH:mm");
    }

    private static DateTimeOffset ParseReleaseMoment(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string FormatDdMmYyyy(II18nService? i18n, string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return string.IsNullOrWhiteSpace(value) ? Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable") : value;
        }

        return $"{parts[2]}/{parts[1]}/{parts[0]}";
    }

    private static string FormatUnixTime(II18nService? i18n, long seconds)
    {
        if (seconds <= 0)
        {
            return Text(i18n, "download.install.choices.summaries.release_time_unavailable", "Release time unavailable");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
    }

    private static long ReadInt64(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return 0;
    }

    private static string GetRouteTitle(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadClient => "Minecraft",
            LauncherFrontendSubpageKey.DownloadOptiFine => "OptiFine",
            LauncherFrontendSubpageKey.DownloadForge => "Forge",
            LauncherFrontendSubpageKey.DownloadNeoForge => "NeoForge",
            LauncherFrontendSubpageKey.DownloadCleanroom => "Cleanroom",
            LauncherFrontendSubpageKey.DownloadFabric => "Fabric",
            LauncherFrontendSubpageKey.DownloadLegacyFabric => "Legacy Fabric",
            LauncherFrontendSubpageKey.DownloadQuilt => "Quilt",
            LauncherFrontendSubpageKey.DownloadLiteLoader => "LiteLoader",
            LauncherFrontendSubpageKey.DownloadLabyMod => "LabyMod",
            _ => route.ToString()
        };
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{

    private IReadOnlyList<string> BuildCommunityProjectCategoryTags(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary)
            || string.Equals(summary, T("resource_detail.selection.no_tags"), StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return summary
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(LocalizeCommunityProjectCategory)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private FrontendDownloadCatalogEntry LocalizeCommunityProjectLinkEntry(FrontendDownloadCatalogEntry entry)
    {
        return entry with
        {
            Title = LocalizeCommunityProjectLinkTitle(entry.Title),
            ActionText = LocalizeCommunityProjectLinkAction(entry.ActionText)
        };
    }

    private FrontendCommunityProjectReleaseEntry LocalizeCommunityProjectReleaseEntry(FrontendCommunityProjectReleaseEntry entry)
    {
        return entry with
        {
            Info = LocalizeCommunityProjectReleaseInfo(entry.Info),
            Meta = LocalizeCommunityProjectReleaseMeta(entry.Meta),
            ActionText = LocalizeCommunityProjectLinkAction(entry.ActionText)
        };
    }

    private bool TryParseCommunityProjectToken(string? value, string prefix, out string[] segments)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.StartsWith(prefix + "|", StringComparison.Ordinal))
        {
            segments = value
                .Split('|')
                .Skip(1)
                .Select(Uri.UnescapeDataString)
                .ToArray();
            return true;
        }

        segments = [];
        return false;
    }

    private static IReadOnlyList<string> SplitCommunityProjectList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
    }

    private string AppendCommunityProjectAndMore(string value, bool hasMore)
    {
        return hasMore ? value + T("resource_detail.values.and_more") : value;
    }

    private string LocalizeCommunityProjectStatus(string value)
    {
        return value switch
        {
            "published" => T("resource_detail.statuses.published"),
            "unlisted" => T("resource_detail.statuses.unlisted"),
            "archived" => T("resource_detail.statuses.archived"),
            "draft" => T("resource_detail.statuses.draft"),
            "removed" => T("resource_detail.statuses.removed"),
            "load_failed" => T("resource_detail.statuses.load_failed"),
            "unknown" or "" => T("resource_detail.values.unknown"),
            _ when value.StartsWith("status_", StringComparison.OrdinalIgnoreCase) =>
                T("resource_detail.statuses.fallback", ("status", value["status_".Length..])),
            _ => value
        };
    }

    private string LocalizeCommunityProjectUpdatedLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)
            ? T("resource_detail.values.unknown")
            : value;
    }

    private string LocalizeCommunityProjectCompatibilitySummary(string value)
    {
        if (!TryParseCommunityProjectToken(value, "compatibility", out var segments))
        {
            return value;
        }

        var versions = SplitCommunityProjectList(segments.ElementAtOrDefault(0));
        var versionLabel = versions.Count == 0
            ? T("resource_detail.compatibility.no_versions")
            : T(
                "resource_detail.compatibility.versions",
                ("versions", AppendCommunityProjectAndMore(string.Join(" / ", versions), segments.ElementAtOrDefault(1) == "1")));
        var loaders = SplitCommunityProjectList(segments.ElementAtOrDefault(2));
        var loaderLabel = loaders.Count == 0
            ? T("resource_detail.compatibility.no_loaders")
            : T("resource_detail.compatibility.loaders", ("loaders", string.Join(" / ", loaders)));
        return T("resource_detail.compatibility.summary", ("version_summary", versionLabel), ("loader_summary", loaderLabel));
    }

    private string LocalizeCommunityProjectCategorySummary(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return T("resource_detail.selection.no_tags");
        }

        var categories = value
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(LocalizeCommunityProjectCategory)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Take(10)
            .ToArray();
        return categories.Length == 0 ? T("resource_detail.selection.no_tags") : string.Join(" / ", categories);
    }

    private string LocalizeCommunityProjectWarningText(string value)
    {
        if (TryParseCommunityProjectToken(value, "warning_load_failed", out var segments))
        {
            return T("resource_detail.warnings.load_failed", ("message", segments.ElementAtOrDefault(0) ?? string.Empty));
        }

        return value;
    }

    private string LocalizeCommunityProjectLinkTitle(string value)
    {
        if (TryParseCommunityProjectToken(value, "support_author", out var segments))
        {
            return T("resource_detail.link_titles.support_author", ("platform", segments.ElementAtOrDefault(0) ?? T("resource_detail.link_titles.default_support_platform")));
        }

        return value switch
        {
            "homepage" => T("resource_detail.link_titles.homepage"),
            "issues" => T("resource_detail.link_titles.issues"),
            "source" => T("resource_detail.link_titles.source"),
            "wiki" => T("resource_detail.link_titles.wiki"),
            "community" => T("resource_detail.link_titles.community"),
            _ => value
        };
    }

    private string LocalizeCommunityProjectLinkAction(string value)
    {
        return value switch
        {
            "view_details" => T("resource_detail.actions.view_details"),
            "open_download" => T("resource_detail.link_actions.open_download"),
            "open_project_page" => T("resource_detail.link_actions.open_project_page"),
            "open_homepage" => T("resource_detail.link_actions.open_homepage"),
            "open_issues" => T("resource_detail.link_actions.open_issues"),
            "open_source" => T("resource_detail.link_actions.open_source"),
            "open_wiki" => T("resource_detail.link_actions.open_wiki"),
            "open_community" => T("resource_detail.link_actions.open_community"),
            "open_link" => T("resource_detail.link_actions.open_link"),
            _ => value
        };
    }

    private string LocalizeCommunityProjectReleaseInfo(string value)
    {
        if (!TryParseCommunityProjectToken(value, "release_info", out var segments))
        {
            return value;
        }

        var parts = new List<string>();
        var versions = SplitCommunityProjectList(segments.ElementAtOrDefault(0));
        if (versions.Count > 0)
        {
            parts.Add(T(
                "resource_detail.release_info.versions",
                ("versions", AppendCommunityProjectAndMore(string.Join(" / ", versions), segments.ElementAtOrDefault(1) == "1"))));
        }

        var loaders = SplitCommunityProjectList(segments.ElementAtOrDefault(2));
        if (loaders.Count > 0)
        {
            parts.Add(string.Join(" / ", loaders));
        }

        return parts.Count == 0 ? T("resource_detail.release_info.unavailable") : string.Join(" • ", parts);
    }

    private string LocalizeCommunityProjectReleaseMeta(string value)
    {
        if (!TryParseCommunityProjectToken(value, "release_meta", out var segments))
        {
            return value;
        }

        var date = LocalizeCommunityProjectUpdatedLabel(segments.ElementAtOrDefault(0) ?? string.Empty);
        if (!int.TryParse(segments.ElementAtOrDefault(1), out var downloadCount) || downloadCount <= 0)
        {
            return T("resource_detail.release_meta.published", ("date", date));
        }

        return T(
            "resource_detail.release_meta.published_with_downloads",
            ("date", date),
            ("downloads", T("resource_detail.release_meta.downloads", ("count", FormatCompactCount(downloadCount)))));
    }

    private string LocalizeCommunityProjectDependencyMeta(string value)
    {
        if (!TryParseCommunityProjectToken(value, "dependency_meta", out var segments))
        {
            return value;
        }

        var parts = new List<string>();
        var source = segments.ElementAtOrDefault(0);
        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add(source);
        }

        var projectType = LocalizeCommunityProjectProjectType(segments.ElementAtOrDefault(1) ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(projectType))
        {
            parts.Add(projectType);
        }

        var author = segments.ElementAtOrDefault(2);
        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add(author);
        }

        return string.Join(" • ", parts);
    }

    private string LocalizeCommunityProjectProjectType(string value)
    {
        return value switch
        {
            "mod" => T("resource_detail.project_types.mod"),
            "modpack" => T("resource_detail.project_types.modpack"),
            "resource_pack" => T("resource_detail.project_types.resource_pack"),
            "shader" => T("resource_detail.project_types.shader"),
            "data_pack" => T("resource_detail.project_types.data_pack"),
            "world" => T("resource_detail.project_types.world"),
            _ => value
        };
    }

    private string LocalizeCommunityProjectCategory(string value)
    {
        return value switch
        {
            "technology" => T("resource_detail.categories.technology"),
            "magic" => T("resource_detail.categories.magic"),
            "adventure" => T("resource_detail.categories.adventure"),
            "utility" => T("resource_detail.categories.utility"),
            "performance" => T("resource_detail.categories.performance"),
            "vanilla_style" => T("resource_detail.categories.vanilla_style"),
            "realistic" => T("resource_detail.categories.realistic"),
            "worldgen" => T("resource_detail.categories.worldgen"),
            "food_cooking" => T("resource_detail.categories.food_cooking"),
            "game_mechanics" => T("resource_detail.categories.game_mechanics"),
            "transportation" => T("resource_detail.categories.transportation"),
            "storage" => T("resource_detail.categories.storage"),
            "decoration" => T("resource_detail.categories.decoration"),
            "mobs" => T("resource_detail.categories.mobs"),
            "equipment_tools" => T("resource_detail.categories.equipment_tools"),
            "server" => T("resource_detail.categories.server"),
            "library" => T("resource_detail.categories.library"),
            "multiplayer" => T("resource_detail.categories.multiplayer"),
            "hardcore" => T("resource_detail.categories.hardcore"),
            "combat" => T("resource_detail.categories.combat"),
            "quests" => T("resource_detail.categories.quests"),
            "kitchen_sink" => T("resource_detail.categories.kitchen_sink"),
            "lightweight" => T("resource_detail.categories.lightweight"),
            "simple" => T("resource_detail.categories.simple"),
            "tweaks" => T("resource_detail.categories.tweaks"),
            "data_pack" => T("resource_detail.categories.data_pack"),
            "biomes" => T("resource_detail.categories.biomes"),
            "dimensions" => T("resource_detail.categories.dimensions"),
            "ores_resources" => T("resource_detail.categories.ores_resources"),
            "structures" => T("resource_detail.categories.structures"),
            "pipes_logistics" => T("resource_detail.categories.pipes_logistics"),
            "automation" => T("resource_detail.categories.automation"),
            "energy" => T("resource_detail.categories.energy"),
            "redstone" => T("resource_detail.categories.redstone"),
            "farming" => T("resource_detail.categories.farming"),
            "information" => T("resource_detail.categories.information"),
            "exploration" => T("resource_detail.categories.exploration"),
            "small_modpack" => T("resource_detail.categories.small_modpack"),
            "cartoon" => T("resource_detail.categories.cartoon"),
            "fantasy" => T("resource_detail.categories.fantasy"),
            "16x" => T("resource_detail.categories.resolution_16x"),
            "32x" => T("resource_detail.categories.resolution_32x"),
            "64x+" => T("resource_detail.categories.resolution_64x_plus"),
            _ => value
        };
    }

}

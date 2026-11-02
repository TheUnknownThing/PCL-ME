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
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private string LocalizeDownloadResourceActionText(string actionText)
    {
        return actionText switch
        {
            "view_details" => T("resource_detail.actions.view_details"),
            _ => actionText
        };
    }

    private string LocalizeDownloadResourceHintText(string hintText)
    {
        if (string.IsNullOrWhiteSpace(hintText))
        {
            return string.Empty;
        }

        var lines = hintText
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                if (line.StartsWith("version_fallback|", StringComparison.Ordinal))
                {
                    var parts = line.Split('|', 3);
                    if (parts.Length == 3)
                    {
                        return T(
                            "download.resource.hints.version_fallback",
                            ("version", parts[1]),
                            ("surface_name", LocalizeDownloadResourceSurfaceName(parts[2])));
                    }
                }

                if (line.StartsWith("partial_results|", StringComparison.Ordinal))
                {
                    return T(
                        "download.resource.hints.partial_results",
                        ("message", line["partial_results|".Length..].Trim()));
                }

                if (line.StartsWith("unavailable|", StringComparison.Ordinal))
                {
                    return T(
                        "download.resource.hints.unavailable",
                        ("message", line["unavailable|".Length..].Trim()));
                }

                return line;
            })
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join(" ", lines);
    }

    private string LocalizeDownloadResourceSurfaceName(string surfaceId)
    {
        return surfaceId switch
        {
            "mod" => T("download.resource.search.mod"),
            "pack" => T("download.resource.search.pack"),
            "resource_pack" => T("download.resource.search.resource_pack"),
            "shader" => T("download.resource.search.shader"),
            "world" => T("download.resource.search.world"),
            _ => surfaceId
        };
    }

    private string FormatDownloadResourceVersionLabel(string version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? T("download.resource.entry.version_unknown")
            : version;
    }

    private string FormatDownloadResourceDownloadCountLabel(int downloadCount)
    {
        if (downloadCount <= 0)
        {
            return T("download.resource.entry.downloads_none");
        }

        return T(
            "download.resource.entry.downloads_with_count",
            ("count", FormatDownloadResourceCompactCount(downloadCount)));
    }

    private string FormatDownloadResourceUpdatedLabel(int updateRank, int releaseRank)
    {
        var rank = updateRank > 0 ? updateRank : releaseRank;
        if (rank <= 0)
        {
            return T("download.resource.entry.updated_unknown");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(rank).LocalDateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }
        catch
        {
            return T("download.resource.entry.updated_unknown");
        }
    }

    private string FormatDownloadResourceCompactCount(int value)
    {
        if (string.Equals(T("download.resource.entry.count_style"), "east_asian", StringComparison.OrdinalIgnoreCase))
        {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}\u4EBF",
            >= 10_000 => $"{value / 10_000d:0.#}\u4E07",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
        }

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000d:0.#}B",
            >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
            >= 1_000 => $"{value / 1_000d:0.#}K",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
    }

}

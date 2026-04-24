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

internal sealed partial class LauncherViewModel
{
    private static bool SupportsCommunityProjectLoaderFiltering(LauncherFrontendSubpageKey? originSubpage)
    {
        return originSubpage is not LauncherFrontendSubpageKey.DownloadWorld
            and not LauncherFrontendSubpageKey.DownloadDataPack
            and not LauncherFrontendSubpageKey.DownloadResourcePack;
    }

    private static IReadOnlyList<CommunityProjectFilterButtonViewModel> BuildCommunityProjectFilterButtons(
        IReadOnlyList<string> options,
        string selectedValue,
        string allLabel,
        Action<string> applyFilter)
    {
        var buttons = new List<CommunityProjectFilterButtonViewModel>
        {
            new(
                allLabel,
                string.IsNullOrWhiteSpace(selectedValue),
                new ActionCommand(() => applyFilter(string.Empty)))
        };
        buttons.AddRange(options.Select(option => new CommunityProjectFilterButtonViewModel(
            option,
            string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase),
            new ActionCommand(() => applyFilter(option)))));
        return buttons;
    }

    private (bool GroupByDrop, bool FoldOld) DetermineCommunityProjectVersionGrouping(
        IReadOnlyList<FrontendCommunityProjectReleaseEntry> releases)
    {
        if (releases.Count == 0)
        {
            return (false, false);
        }

        var versions = releases
            .SelectMany(release => release.GameVersions)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exactCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, false, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (exactCount < 9)
        {
            return (false, false);
        }

        var groupedCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, true, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (groupedCount < 9)
        {
            return (true, false);
        }

        var foldedCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, false, true))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (foldedCount < 9)
        {
            return (false, true);
        }

        return (true, true);
    }

    private IReadOnlyList<string> BuildCommunityProjectVersionOptions((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return _communityProjectState.Releases
            .SelectMany(release => release.GameVersions.Count == 0 ? [T("resource_detail.values.other")] : release.GameVersions)
            .Select(version => GetGroupedCommunityProjectVersionName(version, versionGrouping.GroupByDrop, versionGrouping.FoldOld))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => GetCommunityProjectVersionSortPriority(value))
            .ThenByDescending(value => ParseVersion(NormalizeMinecraftVersion(value) ?? value))
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildCommunityProjectLoaderOptions()
    {
        if (!SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage))
        {
            return [];
        }

        return FrontendLoaderVisibilityService.FilterVisibleLoaders(
                _communityProjectState.Releases.SelectMany(release => release.Loaders),
                IgnoreQuiltLoader)
            .OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string ResolveCommunityProjectVersionFilter(
        IReadOnlyList<string> versionOptions,
        (bool GroupByDrop, bool FoldOld) versionGrouping,
        string? preferredVersion)
    {
        if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter))
        {
            if (versionOptions.Any(option => string.Equals(option, _selectedCommunityProjectVersionFilter, StringComparison.OrdinalIgnoreCase)))
            {
                return _selectedCommunityProjectVersionFilter;
            }

            var grouped = GetGroupedCommunityProjectVersionName(
                _selectedCommunityProjectVersionFilter,
                versionGrouping.GroupByDrop,
                versionGrouping.FoldOld);
            if (versionOptions.Any(option => string.Equals(option, grouped, StringComparison.OrdinalIgnoreCase)))
            {
                return grouped;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var groupedPreferred = GetGroupedCommunityProjectVersionName(preferredVersion, versionGrouping.GroupByDrop, versionGrouping.FoldOld);
            if (versionOptions.Any(option => string.Equals(option, groupedPreferred, StringComparison.OrdinalIgnoreCase)))
            {
                return groupedPreferred;
            }
        }

        return string.Empty;
    }

    private string ResolveCommunityProjectLoaderFilter(IReadOnlyList<string> loaderOptions)
    {
        if (!SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage))
        {
            return string.Empty;
        }

        return loaderOptions.Any(option => string.Equals(option, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
            ? _selectedCommunityProjectLoaderFilter
            : string.Empty;
    }

    private void SelectCommunityProjectVersionFilter(string filterValue)
    {
        _selectedCommunityProjectVersionFilter = filterValue;
        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private void SelectCommunityProjectLoaderFilter(string filterValue)
    {
        if (!SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage))
        {
            _selectedCommunityProjectLoaderFilter = string.Empty;
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            return;
        }

        _selectedCommunityProjectLoaderFilter = filterValue;
        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private static bool ShouldAutoSyncCommunityProjectFiltersWithInstance(LauncherFrontendSubpageKey? originSubpage)
    {
        return originSubpage != LauncherFrontendSubpageKey.DownloadPack;
    }

    private FrontendCommunityProjectReleaseEntry? GetCurrentCommunityProjectRelease((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return SelectPreferredCommunityProjectRelease(GetVisibleCommunityProjectReleases(versionGrouping));
    }

    private IEnumerable<FrontendCommunityProjectReleaseEntry> GetVisibleCommunityProjectReleases((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return _communityProjectState.Releases.Where(release => MatchesCommunityProjectReleaseFilters(release, versionGrouping));
    }

    private bool MatchesCommunityProjectReleaseFilters(
        FrontendCommunityProjectReleaseEntry release,
        (bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var versions = release.GameVersions.Count == 0 ? [T("resource_detail.values.other")] : release.GameVersions;
        var loaders = release.Loaders.Count == 0 ? [string.Empty] : release.Loaders;

        var matchesVersion = string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            || versions.Any(version => string.Equals(
                GetGroupedCommunityProjectVersionName(version, versionGrouping.GroupByDrop, versionGrouping.FoldOld),
                _selectedCommunityProjectVersionFilter,
                StringComparison.OrdinalIgnoreCase));
        var matchesLoader = !SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage)
            || string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
            || loaders.Any(loader => string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase));
        return matchesVersion && matchesLoader;
    }

    private void ApplyCurrentInstanceCommunityProjectFilters()
    {
        if (!ShouldAutoSyncCommunityProjectFiltersWithInstance(_selectedCommunityProjectOriginSubpage))
        {
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            return;
        }

        if (_communityProjectState.Releases.Count == 0)
        {
            RaiseCommunityProjectProperties();
            return;
        }

        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        var versionOptions = BuildCommunityProjectVersionOptions(versionGrouping);
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var groupedPreferredVersion = GetGroupedCommunityProjectVersionName(
                preferredVersion,
                versionGrouping.GroupByDrop,
                versionGrouping.FoldOld);
            _selectedCommunityProjectVersionFilter = versionOptions.Any(option =>
                string.Equals(option, groupedPreferredVersion, StringComparison.OrdinalIgnoreCase))
                ? groupedPreferredVersion
                : string.Empty;
        }
        else
        {
            _selectedCommunityProjectVersionFilter = string.Empty;
        }

        var loaderOptions = BuildCommunityProjectLoaderOptions();
        if (SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage))
        {
            var preferredLoader = ResolvePreferredInstanceLoaderLabel(_instanceComposition, _selectedCommunityProjectOriginSubpage);
            _selectedCommunityProjectLoaderFilter = !string.IsNullOrWhiteSpace(preferredLoader)
                && loaderOptions.Any(option => string.Equals(option, preferredLoader, StringComparison.OrdinalIgnoreCase))
                ? preferredLoader
                : string.Empty;
        }
        else
        {
            _selectedCommunityProjectLoaderFilter = string.Empty;
        }

        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private int GetCommunityProjectVersionSortPriority(string value)
    {
        return value switch
        {
            var other when string.Equals(other, T("resource_detail.values.other"), StringComparison.Ordinal) => 0,
            var legacy when string.Equals(legacy, T("resource_detail.versions.legacy"), StringComparison.Ordinal) => 1,
            var snapshot when string.Equals(snapshot, T("resource_detail.versions.snapshot"), StringComparison.Ordinal) => 2,
            _ => 3
        };
    }

    private int GetCommunityProjectGroupPriority(string groupTitle)
    {
        var version = ExtractCommunityProjectGroupVersion(groupTitle);
        // Version filters are applied before ordering, so boosting the exact filter value here
        // would incorrectly push 1.21 above newer patch groups like 1.21.11.
        var priority = GetCommunityProjectVersionSortPriority(version) * 10;
        if (!SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage))
        {
            return priority;
        }

        var loader = ExtractCommunityProjectGroupLoader(groupTitle);
        if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
            && string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
        {
            priority += 2;
        }

        return priority;
    }

    private static string ExtractCommunityProjectGroupVersion(string groupTitle)
    {
        var parts = groupTitle.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && CommunityProjectKnownLoaders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            ? parts[1]
            : groupTitle;
    }

    private static string ExtractCommunityProjectGroupLoader(string groupTitle)
    {
        var parts = groupTitle.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && CommunityProjectKnownLoaders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            ? parts[0]
            : string.Empty;
    }

    private string GetGroupedCommunityProjectVersionName(string? version, bool groupByDrop, bool foldOld)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return T("resource_detail.values.other");
        }

        var trimmed = version.Trim();
        if (trimmed.Contains('w', StringComparison.OrdinalIgnoreCase))
        {
            return T("resource_detail.versions.snapshot");
        }

        if (!IsCommunityProjectVersionFormat(trimmed))
        {
            return T("resource_detail.values.other");
        }

        var drop = GetCommunityProjectVersionDrop(trimmed);
        if (drop <= 0)
        {
            return T("resource_detail.values.other");
        }

        if (foldOld && drop < 120)
        {
            return T("resource_detail.versions.legacy");
        }

        if (groupByDrop)
        {
            return GetCommunityProjectDropVersion(drop);
        }

        return NormalizeMinecraftVersion(trimmed) ?? trimmed;
    }

    private static bool IsCommunityProjectVersionFormat(string version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith("1.", StringComparison.Ordinal))
        {
            return true;
        }

        return int.TryParse(normalized.Split('.')[0], out var major) && major > 25;
    }

    private static int GetCommunityProjectVersionDrop(string version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !int.TryParse(segments[0], out var major)
            || !int.TryParse(segments[1], out var minor))
        {
            return 0;
        }

        return major == 1 ? minor * 10 : major * 10 + minor;
    }

    private static string GetCommunityProjectDropVersion(int drop)
    {
        return drop >= 250 ? $"{drop / 10}.{drop % 10}" : $"1.{drop / 10}";
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

}

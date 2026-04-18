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

    private IReadOnlyList<CommunityProjectReleaseGroupViewModel> BuildCommunityProjectReleaseGroups((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var showLoaderPrefix = SupportsCommunityProjectLoaderFiltering(_selectedCommunityProjectOriginSubpage)
                               && BuildCommunityProjectLoaderOptions().Count > 1;
        var groups = new Dictionary<string, List<FrontendCommunityProjectReleaseEntry>>(StringComparer.OrdinalIgnoreCase);
        var dedupe = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var release in _communityProjectState.Releases)
        {
            var gameVersions = release.GameVersions.Count == 0 ? [T("resource_detail.values.other")] : release.GameVersions;
            var visibleLoaders = FrontendLoaderVisibilityService.FilterVisibleLoaders(release.Loaders, IgnoreQuiltLoader);
            var loaders = visibleLoaders.Count == 0 ? [string.Empty] : visibleLoaders;

            foreach (var gameVersion in gameVersions)
            {
                var groupedVersion = GetGroupedCommunityProjectVersionName(gameVersion, versionGrouping.GroupByDrop, versionGrouping.FoldOld);
                if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
                    && !string.Equals(groupedVersion, _selectedCommunityProjectVersionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalizedVersion = NormalizeMinecraftVersion(gameVersion)
                                        ?? (string.IsNullOrWhiteSpace(gameVersion) ? T("resource_detail.values.other") : gameVersion.Trim());
                foreach (var loader in loaders)
                {
                    if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
                        && !string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var title = string.IsNullOrWhiteSpace(loader) || !showLoaderPrefix
                        ? normalizedVersion
                        : $"{loader} {normalizedVersion}";
                    AddCommunityProjectReleaseGroupEntry(groups, dedupe, title, release);
                }
            }
        }

        var orderedGroups = groups
            .OrderByDescending(pair => GetCommunityProjectGroupPriority(pair.Key))
            .ThenByDescending(pair => ParseVersion(ExtractCommunityProjectGroupVersion(pair.Key)))
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var shouldAutoExpandSingleGroup = orderedGroups.Length == 1;
        var shouldAutoExpandFirstGroup = shouldAutoExpandSingleGroup
            || !string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            || !string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter);
        return orderedGroups
            .Select((pair, index) => new CommunityProjectReleaseGroupViewModel(
                pair.Key,
                shouldAutoExpandFirstGroup && index == 0,
                pair.Value
                    .OrderByDescending(entry => entry.PublishedUnixTime)
                    .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(LocalizeCommunityProjectReleaseEntry)
                    .Select(entry => new DownloadCatalogEntryViewModel(
                        entry.Title,
                        entry.Info,
                        entry.Meta,
                        entry.ActionText,
                        entry.IsDirectDownload && !string.IsNullOrWhiteSpace(entry.Target)
                            ? CreateCommunityProjectReleaseDownloadCommand(entry)
                            : string.IsNullOrWhiteSpace(entry.Target)
                                ? CreateIntentCommand(entry.Title, entry.Info)
                                : CreateOpenTargetCommand(T("resource_detail.activities.open_file", ("entry_title", entry.Title)), entry.Target, entry.Target),
                        GetCommunityProjectReleaseChannelIcon(entry.Channel)))
                    .ToArray()))
            .ToArray();
    }

    private Bitmap? GetCommunityProjectReleaseChannelIcon(FrontendCommunityProjectReleaseChannel channel)
    {
        return channel switch
        {
            FrontendCommunityProjectReleaseChannel.Alpha => LoadLauncherBitmap("Images", "Icons", "A.png"),
            FrontendCommunityProjectReleaseChannel.Beta => LoadLauncherBitmap("Images", "Icons", "B.png"),
            _ => LoadLauncherBitmap("Images", "Icons", "R.png")
        };
    }

    private void AddCommunityProjectReleaseGroupEntry(
        IDictionary<string, List<FrontendCommunityProjectReleaseEntry>> groups,
        IDictionary<string, HashSet<string>> dedupe,
        string title,
        FrontendCommunityProjectReleaseEntry release)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? T("resource_detail.values.other") : title;
        if (!groups.TryGetValue(normalizedTitle, out var entries))
        {
            entries = [];
            groups[normalizedTitle] = entries;
            dedupe[normalizedTitle] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var key = $"{release.Title}|{release.Target}|{release.PublishedUnixTime}";
        if (dedupe[normalizedTitle].Add(key))
        {
            entries.Add(release);
        }
    }

}

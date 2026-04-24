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

    private void RebuildCommunityProjectDependencySections((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var release = GetCurrentCommunityProjectRelease(versionGrouping);
        _communityProjectDependencyReleaseTitle = release?.Title ?? string.Empty;
        var visibleDependencies = release?.Dependencies
            .Where(entry => entry.Kind != FrontendCommunityProjectDependencyKind.Embedded)
            .ToArray();
        if (release is null || visibleDependencies is null || visibleDependencies.Length == 0)
        {
            ReplaceItems(CommunityProjectDependencySections, []);
            return;
        }

        var fallbackDependencyIcon = GetCommunityProjectDependencyIcon();
        var sections = visibleDependencies
            .GroupBy(entry => entry.Kind)
            .OrderBy(group => GetCommunityProjectDependencyPriority(group.Key))
            .Select(group => new DownloadCatalogSectionViewModel(
                GetCommunityProjectDependencyGroupTitle(group.Key),
                group.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
                    .Select(entry => new DownloadCatalogEntryViewModel(
                        entry.Title,
                        entry.Summary,
                        LocalizeCommunityProjectDependencyMeta(entry.Meta),
                        T("resource_detail.actions.view_details"),
                        CreateCommunityProjectDependencyCommand(entry),
                        LoadCachedBitmapFromPath(entry.IconPath) ?? fallbackDependencyIcon,
                        entry.IconUrl))
                    .ToArray()))
            .ToArray();
        ReplaceItems(CommunityProjectDependencySections, sections);
        QueueCommunityProjectDependencyIconLoad(sections);
    }

    private ActionCommand CreateCommunityProjectDependencyCommand(FrontendCommunityProjectDependencyEntry entry)
    {
        return FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId)
            ? new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title))
            : string.IsNullOrWhiteSpace(entry.Target)
                ? CreateIntentCommand(entry.Title, entry.Summary)
                : CreateOpenTargetCommand(T("resource_detail.activities.open_dependency_project", ("entry_title", entry.Title)), entry.Target, entry.Target);
    }

    private static int GetCommunityProjectDependencyPriority(FrontendCommunityProjectDependencyKind kind)
    {
        return kind switch
        {
            FrontendCommunityProjectDependencyKind.Required => 0,
            FrontendCommunityProjectDependencyKind.Tool => 1,
            FrontendCommunityProjectDependencyKind.Include => 2,
            FrontendCommunityProjectDependencyKind.Optional => 3,
            FrontendCommunityProjectDependencyKind.Embedded => 4,
            FrontendCommunityProjectDependencyKind.Incompatible => 5,
            _ => 6
        };
    }

    private string GetCommunityProjectDependencyGroupTitle(FrontendCommunityProjectDependencyKind kind)
    {
        return kind switch
        {
            FrontendCommunityProjectDependencyKind.Required => T("resource_detail.dependencies.groups.required"),
            FrontendCommunityProjectDependencyKind.Tool => T("resource_detail.dependencies.groups.tool"),
            FrontendCommunityProjectDependencyKind.Include => T("resource_detail.dependencies.groups.included"),
            FrontendCommunityProjectDependencyKind.Optional => T("resource_detail.dependencies.groups.optional"),
            FrontendCommunityProjectDependencyKind.Embedded => T("resource_detail.dependencies.groups.embedded"),
            FrontendCommunityProjectDependencyKind.Incompatible => T("resource_detail.dependencies.groups.incompatible"),
            _ => T("resource_detail.dependencies.groups.other")
        };
    }

    private Bitmap? GetCommunityProjectDependencyIcon()
    {
        return _selectedCommunityProjectOriginSubpage switch
        {
            LauncherFrontendSubpageKey.DownloadDataPack => LoadLauncherBitmap("Images", "Blocks", "RedstoneLampOn.png"),
            LauncherFrontendSubpageKey.DownloadResourcePack => LoadLauncherBitmap("Images", "Blocks", "Grass.png"),
            LauncherFrontendSubpageKey.DownloadShader => LoadLauncherBitmap("Images", "Blocks", "GoldBlock.png"),
            LauncherFrontendSubpageKey.DownloadWorld => LoadLauncherBitmap("Images", "Blocks", "GrassPath.png"),
            _ => LoadLauncherBitmap("Images", "Blocks", "CommandBlock.png")
        };
    }

    private void QueueCommunityProjectDependencyIconLoad(IEnumerable<DownloadCatalogSectionViewModel> sections)
    {
        foreach (var entry in sections.SelectMany(section => section.Items))
        {
            if (!entry.TryBeginIconLoad())
            {
                continue;
            }

            _ = LoadCommunityProjectDependencyIconAsync(entry);
        }
    }

    private async Task LoadCommunityProjectDependencyIconAsync(DownloadCatalogEntryViewModel entry)
    {
        var iconPath = await FrontendCommunityIconCache.EnsureCachedIconAsync(entry.IconUrl);
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        var bitmap = await Task.Run(() => LoadCachedBitmapFromPath(iconPath));
        if (bitmap is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => entry.ApplyIcon(bitmap));
    }

}

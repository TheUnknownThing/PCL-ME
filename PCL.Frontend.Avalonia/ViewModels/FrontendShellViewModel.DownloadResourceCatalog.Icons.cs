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
    private IReadOnlyList<DownloadResourceEntryViewModel> CreateDownloadResourceEntries(IReadOnlyList<FrontendDownloadResourceEntry> entries)
    {
        return entries
            .Select(entry =>
            {
                var icon = LoadCachedBitmapFromPath(entry.IconPath);
                if (icon is null && !string.IsNullOrWhiteSpace(entry.IconName))
                {
                    icon = LoadLauncherBitmap("Images", "Blocks", entry.IconName);
                }

                return new DownloadResourceEntryViewModel(
                    icon,
                    entry.Title,
                    entry.Info,
                    entry.Source,
                    entry.Version,
                    entry.Loader,
                    entry.Tags,
                    entry.Tags.Select(LocalizeDownloadResourceTag).ToArray(),
                    entry.SupportedVersions,
                    entry.SupportedLoaders,
                    entry.DownloadCount,
                    entry.FollowCount,
                    entry.ReleaseRank,
                    entry.UpdateRank,
                    FormatDownloadResourceVersionLabel(entry.Version),
                    FormatDownloadResourceDownloadCountLabel(entry.DownloadCount),
                    FormatDownloadResourceUpdatedLabel(entry.UpdateRank, entry.ReleaseRank),
                    LocalizeDownloadResourceActionText(entry.ActionText),
                    string.IsNullOrWhiteSpace(entry.TargetPath)
                        ? new ActionCommand(() => AddActivity(
                            T("download.resource.activities.entry_action", ("entry_title", entry.Title)),
                            BuildActivityDetail(entry.Info, entry.Source)))
                        : FrontendCommunityProjectService.TryParseCompDetailTarget(entry.TargetPath, out var projectId)
                            ? new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title, entry.Version, entry.Loader, _currentRoute.Subpage))
                            : CreateOpenTargetCommand(
                                T("download.resource.activities.open_page", ("entry_title", entry.Title)),
                                entry.TargetPath,
                                entry.TargetPath),
                    entry.IconUrl);
            })
            .ToArray();
    }

    private void QueueDownloadResourceIconLoad(IEnumerable<DownloadResourceEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            if (!entry.TryBeginIconLoad())
            {
                continue;
            }

            _ = LoadDownloadResourceIconAsync(entry);
        }
    }

    private async Task LoadDownloadResourceIconAsync(DownloadResourceEntryViewModel entry)
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
    private DownloadResourceEntryViewModel CreateDownloadResourceEntry(
        string title,
        string info,
        string source,
        string version,
        string loader,
        IReadOnlyList<string> tags,
        string actionText,
        string? iconFileName,
        int downloadCount,
        int followCount,
        int releaseRank,
        int updateRank)
    {
        Bitmap? icon = null;
        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            icon = LoadLauncherBitmap("Images", "Blocks", iconFileName);
        }

        return new DownloadResourceEntryViewModel(
            icon,
            title,
            info,
            source,
            version,
            loader,
            tags,
            tags.Select(LocalizeDownloadResourceTag).ToArray(),
            [],
            [],
            downloadCount,
            followCount,
            releaseRank,
            updateRank,
            FormatDownloadResourceVersionLabel(version),
            FormatDownloadResourceDownloadCountLabel(downloadCount),
            FormatDownloadResourceUpdatedLabel(updateRank, releaseRank),
            LocalizeDownloadResourceActionText(actionText),
            new ActionCommand(() => AddActivity(
                T("download.resource.activities.entry_action", ("entry_title", title)),
                BuildActivityDetail(source, version, string.Join(" / ", tags)))),
            null);
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private InstanceScreenshotEntryViewModel CreateInstanceScreenshotEntry(string title, string info, string path, Bitmap? image)
    {
        return new InstanceScreenshotEntryViewModel(
            image,
            title,
            info,
            path,
            new ActionCommand(() => OpenInstanceTarget(
                SD("instance.content.screenshot.actions.open_file"),
                path,
                SD("instance.content.screenshot.messages.missing_file"))));
    }

    private void RefreshInstanceScreenshotEntries()
    {
        var entries = _instanceComposition.Screenshot.Entries.Select(entry =>
        {
            var info = LocalizeResourceSummary(entry.Summary);
            var existing = InstanceScreenshotEntries.FirstOrDefault(item =>
                string.Equals(item.Path, entry.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Title, entry.Title, StringComparison.Ordinal)
                && string.Equals(item.Info, info, StringComparison.Ordinal));

            return existing ?? CreateInstanceScreenshotEntry(
                entry.Title,
                info,
                entry.Path,
                LoadInstanceBitmap(entry.Path, "Images", "Backgrounds", "server_bg.png"));
        }).ToArray();

        ReplaceItems(
            InstanceScreenshotEntries,
            entries);
    }

}

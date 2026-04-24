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
            new ActionCommand(() => OpenInstanceTarget(
                SD("instance.content.screenshot.actions.open_file"),
                path,
                SD("instance.content.screenshot.messages.missing_file"))));
    }

    private void RefreshInstanceScreenshotEntries()
    {
        ReplaceItems(
            InstanceScreenshotEntries,
            _instanceComposition.Screenshot.Entries.Select(entry => CreateInstanceScreenshotEntry(
                entry.Title,
                LocalizeResourceSummary(entry.Summary),
                entry.Path,
                LoadInstanceBitmap(entry.Path, "Images", "Backgrounds", "server_bg.png"))));
    }

}

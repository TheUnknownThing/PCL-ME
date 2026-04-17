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
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void RefreshInstanceWorldEntries()
    {
        var filteredEntries = _instanceComposition.World.Entries
            .Where(entry => MatchesSearch(entry.Title, entry.Summary, entry.Path, InstanceWorldSearchQuery));

        ReplaceItems(
            InstanceWorldEntries,
            ApplyInstanceWorldSort(filteredEntries)
                .Select(entry => new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => OpenVersionSaveDetails(entry.Path)))));
    }

    private IEnumerable<FrontendInstanceDirectoryEntry> ApplyInstanceWorldSort(IEnumerable<FrontendInstanceDirectoryEntry> entries)
    {
        return _instanceWorldSortMethod switch
        {
            InstanceWorldSortMethod.CreateTime => entries
                .OrderByDescending(entry => GetDirectoryCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceWorldSortMethod.ModifyTime => entries
                .OrderByDescending(entry => GetDirectoryLastWriteTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void SetInstanceWorldSortMethod(InstanceWorldSortMethod target)
    {
        if (_instanceWorldSortMethod == target)
        {
            return;
        }

        _instanceWorldSortMethod = target;

        RaisePropertyChanged(nameof(InstanceWorldSortText));
        RefreshInstanceWorldEntries();
    }

    internal void SetInstanceWorldFileNameSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.FileName);

    internal void SetInstanceWorldCreateTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.CreateTime);

    internal void SetInstanceWorldModifyTimeSort() => SetInstanceWorldSortMethod(InstanceWorldSortMethod.ModifyTime);

    private string GetInstanceWorldSortName(InstanceWorldSortMethod method)
    {
        return method switch
        {
            InstanceWorldSortMethod.CreateTime => SD("instance.content.sort.create_time"),
            InstanceWorldSortMethod.ModifyTime => SD("instance.content.sort.modify_time"),
            _ => SD("instance.content.sort.file_name")
        };
    }

    private static DateTime GetDirectoryCreationTimeUtc(string path)
    {
        try
        {
            return Directory.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetDirectoryLastWriteTimeUtc(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

}

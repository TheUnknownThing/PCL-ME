using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.ViewModels;
internal sealed class CommunityProjectFilterButtonViewModel(
    string text,
    bool isChecked,
    ActionCommand command)
{
    public string Text { get; } = text;

    public bool IsChecked { get; } = isChecked;

    public ActionCommand Command { get; } = command;
}

internal sealed class CommunityProjectActionButtonViewModel(
    string text,
    string iconData,
    double iconScale,
    PclIconTextButtonColorState colorType,
    ActionCommand command)
{
    public string Text { get; } = text;

    public string IconData { get; } = iconData;

    public double IconScale { get; } = iconScale;

    public PclIconTextButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

internal sealed class CommunityProjectReleaseGroupViewModel(
    string title,
    bool isExpanded,
    IReadOnlyList<DownloadCatalogEntryViewModel> items)
{
    public string Title { get; } = title;

    public bool IsExpanded { get; } = isExpanded;

    public IReadOnlyList<DownloadCatalogEntryViewModel> Items { get; } = items;
}

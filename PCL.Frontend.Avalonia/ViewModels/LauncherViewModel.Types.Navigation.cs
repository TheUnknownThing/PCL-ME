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
internal sealed class NavigationEntryViewModel(
    string title,
    string summary,
    string meta,
    bool isSelected,
    string iconPath,
    double iconScale,
    NavigationPalette palette,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Meta { get; } = meta;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public IBrush BackgroundBrush { get; } = palette.Background;

    public IBrush BorderBrush { get; } = palette.Border;

    public IBrush ForegroundBrush { get; } = palette.Foreground;

    public IBrush AccentBrush { get; } = palette.Accent;

    public ActionCommand Command { get; } = command;
}

internal sealed class SidebarSectionViewModel(
    string title,
    bool hasTitle,
    int enterDelay,
    IReadOnlyList<SidebarListItemViewModel> items)
{
    public string Title { get; } = title;

    public bool HasTitle { get; } = hasTitle;

    public int EnterDelay { get; } = enterDelay;

    public IReadOnlyList<SidebarListItemViewModel> Items { get; } = items;
}

internal sealed class SidebarListItemViewModel(
    string title,
    string summary,
    bool isSelected,
    string iconPath,
    double iconScale,
    int enterDelay,
    ActionCommand command,
    string accessoryToolTip,
    string accessoryIconPath,
    ActionCommand? accessoryCommand) : ViewModelBase
{
    private bool _isSelected = isSelected;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public int EnterDelay { get; } = enterDelay;

    public ActionCommand Command { get; } = command;

    public string AccessoryToolTip { get; } = accessoryToolTip;

    public string AccessoryIconPath { get; } = accessoryIconPath;

    public ActionCommand? AccessoryCommand { get; } = accessoryCommand;
}

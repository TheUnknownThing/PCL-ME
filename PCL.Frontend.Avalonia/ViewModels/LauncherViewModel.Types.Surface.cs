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
internal sealed class SurfaceFactViewModel(
    string label,
    string value,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Label { get; } = label;

    public string Value { get; } = value;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceSectionViewModel(
    string eyebrow,
    string title,
    IReadOnlyList<SurfaceLineViewModel> lines,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Eyebrow { get; } = eyebrow;

    public string Title { get; } = title;

    public IReadOnlyList<SurfaceLineViewModel> Lines { get; } = lines;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceLineViewModel(
    string text,
    IBrush accentBrush,
    IBrush foregroundBrush)
{
    public string Text { get; } = text;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class ActivityItemViewModel(string time, string title, string body)
{
    public string Time { get; } = time;

    public string Title { get; } = title;

    public string Body { get; } = body;
}

internal sealed class AboutEntryViewModel(
    string title,
    string info,
    Bitmap? avatar,
    string? actionText,
    ActionCommand? actionCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public Bitmap? Avatar { get; } = avatar;

    public string ActionText { get; } = actionText ?? string.Empty;

    public ActionCommand? ActionCommand { get; } = actionCommand;

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionText);
}

internal sealed class SimpleListEntryViewModel(string title, string info, ActionCommand command)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public ActionCommand Command { get; } = command;
}

internal sealed class KeyValueEntryViewModel(string label, string value)
{
    public string Label { get; } = label;

    public string Value { get; } = value;
}

internal sealed class ExportOptionEntryViewModel(
    string key,
    string title,
    string description,
    bool isChecked) : ViewModelBase
{
    private bool _isChecked = isChecked;

    public string Key { get; } = key;

    public string Title { get; } = title;

    public string Description { get; } = description;

    public string InlineDescription => HasDescription ? $"   {Description}" : string.Empty;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

internal sealed class ExportOptionGroupViewModel(
    ExportOptionEntryViewModel header,
    IReadOnlyList<ExportOptionEntryViewModel> children)
{
    public ExportOptionEntryViewModel Header { get; } = header;

    public IReadOnlyList<ExportOptionEntryViewModel> Children { get; } = children;

    public bool HasChildren => Children.Count > 0;
}

internal sealed class ToolboxActionViewModel(
    string title,
    string toolTip,
    double minWidth,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string ToolTip { get; } = toolTip;

    public double MinWidth { get; } = minWidth;

    public PclButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

internal sealed class SurfaceNoticeViewModel(
    string text,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush,
    Thickness margin)
{
    public string Text { get; } = text;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;

    public Thickness Margin { get; } = margin;
}

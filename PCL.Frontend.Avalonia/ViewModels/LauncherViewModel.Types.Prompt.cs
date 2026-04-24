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
internal sealed class PromptLaneViewModel(
    AvaloniaPromptLaneKind kind,
    string title,
    string summary,
    ActionCommand command) : ViewModelBase
{
    private int _count;
    private bool _isSelected;

    public AvaloniaPromptLaneKind Kind { get; } = kind;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public ActionCommand Command { get; } = command;

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(BackgroundBrush));
                RaisePropertyChanged(nameof(BorderBrush));
                RaisePropertyChanged(nameof(ForegroundBrush));
            }
        }
    }

    public IBrush BackgroundBrush => IsSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent", "#01EAF2FE");

    public IBrush BorderBrush => IsSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#D5E6FD");

    public IBrush ForegroundBrush => IsSelected
        ? Brushes.White
        : FrontendThemeResourceResolver.GetBrush("ColorBrushGray1", "#404040");
}

internal sealed class PromptCardViewModel(
    AvaloniaPromptLaneKind lane,
    string id,
    string title,
    string message,
    string source,
    string severity,
    IBrush titleBrush,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IReadOnlyList<PromptOptionViewModel> options)
{
    public AvaloniaPromptLaneKind Lane { get; } = lane;

    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Source { get; } = source;

    public string Severity { get; } = severity;

    public IBrush TitleBrush { get; } = titleBrush;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IReadOnlyList<PromptOptionViewModel> Options { get; } = options;
}

internal sealed class PromptOptionViewModel(
    string label,
    string detail,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Label { get; } = label;

    public string Detail { get; } = detail;

    public PclButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

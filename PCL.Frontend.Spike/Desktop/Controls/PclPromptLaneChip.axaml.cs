using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclPromptLaneChip : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, int>(nameof(Count));

    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, IBrush?>(nameof(BackgroundBrush));

    public static readonly StyledProperty<IBrush?> ChipBorderBrushProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, IBrush?>(nameof(ChipBorderBrush));

    public static readonly StyledProperty<IBrush?> ForegroundBrushProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, IBrush?>(nameof(ForegroundBrush));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclPromptLaneChip, ICommand?>(nameof(Command));

    public PclPromptLaneChip()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush? ChipBorderBrush
    {
        get => GetValue(ChipBorderBrushProperty);
        set => SetValue(ChipBorderBrushProperty, value);
    }

    public IBrush? ForegroundBrush
    {
        get => GetValue(ForegroundBrushProperty);
        set => SetValue(ForegroundBrushProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}

using Avalonia;
using Avalonia.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclMemorySummary : UserControl
{
    public static readonly StyledProperty<GridLength> UsedBarWidthProperty =
        AvaloniaProperty.Register<PclMemorySummary, GridLength>(nameof(UsedBarWidth), new GridLength(1, GridUnitType.Star));

    public static readonly StyledProperty<GridLength> AllocatedBarWidthProperty =
        AvaloniaProperty.Register<PclMemorySummary, GridLength>(nameof(AllocatedBarWidth), new GridLength(1, GridUnitType.Star));

    public static readonly StyledProperty<GridLength> FreeBarWidthProperty =
        AvaloniaProperty.Register<PclMemorySummary, GridLength>(nameof(FreeBarWidth), new GridLength(1, GridUnitType.Star));

    public static readonly StyledProperty<string> UsedRamLabelProperty =
        AvaloniaProperty.Register<PclMemorySummary, string>(nameof(UsedRamLabel), "0.0 GB");

    public static readonly StyledProperty<string> UsageHeaderTextProperty =
        AvaloniaProperty.Register<PclMemorySummary, string>(nameof(UsageHeaderText), string.Empty);

    public static readonly StyledProperty<string> TotalRamLabelProperty =
        AvaloniaProperty.Register<PclMemorySummary, string>(nameof(TotalRamLabel), "0.0 GB");

    public static readonly StyledProperty<string> AllocatedRamLabelProperty =
        AvaloniaProperty.Register<PclMemorySummary, string>(nameof(AllocatedRamLabel), "0.0 GB");

    public static readonly StyledProperty<string> AllocationPrefixTextProperty =
        AvaloniaProperty.Register<PclMemorySummary, string>(nameof(AllocationPrefixText), string.Empty);

    public PclMemorySummary()
    {
        InitializeComponent();
    }

    public GridLength UsedBarWidth
    {
        get => GetValue(UsedBarWidthProperty);
        set => SetValue(UsedBarWidthProperty, value);
    }

    public GridLength AllocatedBarWidth
    {
        get => GetValue(AllocatedBarWidthProperty);
        set => SetValue(AllocatedBarWidthProperty, value);
    }

    public GridLength FreeBarWidth
    {
        get => GetValue(FreeBarWidthProperty);
        set => SetValue(FreeBarWidthProperty, value);
    }

    public string UsedRamLabel
    {
        get => GetValue(UsedRamLabelProperty);
        set => SetValue(UsedRamLabelProperty, value);
    }

    public string UsageHeaderText
    {
        get => GetValue(UsageHeaderTextProperty);
        set => SetValue(UsageHeaderTextProperty, value);
    }

    public string TotalRamLabel
    {
        get => GetValue(TotalRamLabelProperty);
        set => SetValue(TotalRamLabelProperty, value);
    }

    public string AllocatedRamLabel
    {
        get => GetValue(AllocatedRamLabelProperty);
        set => SetValue(AllocatedRamLabelProperty, value);
    }

    public string AllocationPrefixText
    {
        get => GetValue(AllocationPrefixTextProperty);
        set => SetValue(AllocationPrefixTextProperty, value);
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclSidebarButton : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclSidebarButton, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SummaryProperty =
        AvaloniaProperty.Register<PclSidebarButton, string>(nameof(Summary), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PclSidebarButton, bool>(nameof(IsSelected));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclSidebarButton, ICommand?>(nameof(Command));

    private static readonly IBrush SelectedBackground = Brush.Parse("#EAF2FE");
    private static readonly IBrush SelectedBorder = Brush.Parse("#D5E6FD");
    private static readonly IBrush HoverBackground = Brush.Parse("#EFF5FE");
    private static readonly IBrush HoverBorder = Brush.Parse("#E0EAFD");
    private static readonly IBrush IdleBackground = Brush.Parse("#01FFFFFF");
    private static readonly IBrush IdleBorder = Brush.Parse("#01FFFFFF");
    private bool _isHovered;
    private bool _isPressed;

    public PclSidebarButton()
    {
        InitializeComponent();

        ButtonHost.PointerEntered += (_, _) =>
        {
            _isHovered = true;
            RefreshVisualState();
        };
        ButtonHost.PointerExited += (_, _) =>
        {
            _isHovered = false;
            _isPressed = false;
            RefreshVisualState();
        };
        ButtonHost.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(ButtonHost).Properties.IsLeftButtonPressed)
            {
                _isPressed = true;
                RefreshVisualState();
            }
        };
        ButtonHost.PointerReleased += (_, _) =>
        {
            _isPressed = false;
            RefreshVisualState();
        };

        TitleBlock.Text = Title;
        SummaryBlock.Text = Summary;
        RefreshVisualState();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            TitleBlock.Text = change.GetNewValue<string>();
        }
        else if (change.Property == SummaryProperty)
        {
            SummaryBlock.Text = change.GetNewValue<string>();
        }
        else if (change.Property == IsSelectedProperty)
        {
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        PanBack.Background = IsSelected
            ? SelectedBackground
            : _isHovered
                ? HoverBackground
                : IdleBackground;
        PanBack.BorderBrush = IsSelected
            ? SelectedBorder
            : _isHovered
                ? HoverBorder
                : IdleBorder;
        PanBack.Opacity = _isPressed ? 0.92 : 1.0;
        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.985, 0.985)
            : _isHovered
                ? new ScaleTransform(1.01, 1.01)
                : new ScaleTransform(1, 1);

        TitleBlock.Foreground = IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#404040");
        SummaryBlock.Foreground = IsSelected ? Brush.Parse("#4B5968") : Brush.Parse("#7D8897");
        ChevronBlock.Foreground = IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#A6B8CF");
        SelectionBar.IsVisible = IsSelected;
    }
}

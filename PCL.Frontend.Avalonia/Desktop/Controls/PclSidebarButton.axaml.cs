using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

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
        var idleBrush = GetBrush("ColorBrushSemiTransparent", "#01FFFFFF");
        PanBack.Background = IsSelected
            ? _isHovered
                ? GetBrush("ColorBrushEntrySelectedHoverBackground", "#DDEBFE")
                : GetBrush("ColorBrushEntrySelectedBackground", "#EAF2FE")
            : _isHovered
                ? GetBrush("ColorBrushEntryHoverBackground", "#E2EEFE")
                : idleBrush;
        PanBack.BorderBrush = IsSelected
            ? GetBrush("ColorBrush6", "#D5E6FD")
            : _isHovered
                ? GetBrush("ColorBrush7", "#E0EAFD")
                : idleBrush;
        PanBack.Opacity = _isPressed ? 0.92 : 1.0;
        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.985, 0.985)
            : _isHovered
                ? new ScaleTransform(1.01, 1.01)
                : new ScaleTransform(1, 1);

        TitleBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3", "#1370F3")
            : GetBrush("ColorBrushGray1", "#404040");
        SummaryBlock.Foreground = IsSelected
            ? GetBrush("ColorBrushEntrySecondarySelected", "#4B78C2")
            : GetBrush("ColorBrushEntrySecondaryIdle", "#7D8897");
        ChevronBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3", "#1370F3")
            : GetBrush("ColorBrushEntryChevronIdle", "#A6B8CF");
        SelectionBar.IsVisible = IsSelected;
    }

    private IBrush GetBrush(string resourceKey, string fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return Brush.Parse(fallback);
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<PclButtonColorState> ColorTypeProperty =
        AvaloniaProperty.Register<PclButton, PclButtonColorState>(nameof(ColorType), PclButtonColorState.Normal);

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<PclButton, Thickness>(nameof(ContentPadding), default);

    public static readonly StyledProperty<Thickness> TextMarginProperty =
        AvaloniaProperty.Register<PclButton, Thickness>(nameof(TextMargin), default);

    private bool _isHovered;
    private bool _isPressed;

    public PclButton()
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

        LabText.Text = Text;
        RefreshVisualState();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public PclButtonColorState ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public Thickness TextMargin
    {
        get => GetValue(TextMarginProperty);
        set => SetValue(TextMarginProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            LabText.Text = change.GetNewValue<string>();
        }
        else if (change.Property == IsEnabledProperty || change.Property == ColorTypeProperty)
        {
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        var borderBrush = ResolveBorderBrush();
        var background = ResolveBackgroundBrush();

        PanFore.BorderBrush = borderBrush;
        PanFore.Background = background;
        LabText.Foreground = borderBrush;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
    }

    private IBrush ResolveBorderBrush()
    {
        if (!IsEnabled)
        {
            return Brush.Parse("#A6A6A6");
        }

        return ColorType switch
        {
            PclButtonColorState.Highlight when _isHovered => Brush.Parse("#1370F3"),
            PclButtonColorState.Highlight => Brush.Parse("#0B5BCB"),
            PclButtonColorState.Red when _isHovered => Brush.Parse("#FF4C4C"),
            PclButtonColorState.Red => Brush.Parse("#CE2111"),
            _ when _isHovered => Brush.Parse("#1370F3"),
            _ => Brush.Parse("#343D4A")
        };
    }

    private IBrush ResolveBackgroundBrush()
    {
        if (!IsEnabled)
        {
            return Brush.Parse("#40FFFFFF");
        }

        if (_isHovered)
        {
            return ColorType == PclButtonColorState.Red
                ? Brush.Parse("#80FBDDDD")
                : Brush.Parse("#E0EAFD");
        }

        return Brush.Parse("#55FFFFFF");
    }
}

internal enum PclButtonColorState
{
    Normal = 0,
    Highlight = 1,
    Red = 2
}

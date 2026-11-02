using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.UI;

namespace PCL.UI.Controls;

public sealed partial class PclButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<PclButtonColorState> ColorTypeProperty =
        AvaloniaProperty.Register<PclButton, PclButtonColorState>(nameof(ColorType), PclButtonColorState.Normal);

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<PclButton, Thickness>(nameof(ContentPadding), new Thickness(13, 0));

    public static readonly StyledProperty<Thickness> TextMarginProperty =
        AvaloniaProperty.Register<PclButton, Thickness>(nameof(TextMargin), default);

    public static readonly StyledProperty<bool> UseFloatingSurfaceProperty =
        AvaloniaProperty.Register<PclButton, bool>(nameof(UseFloatingSurface));

    private bool _isPressed;
    private bool _isAppearanceSubscribed;

    public PclButton()
    {
        InitializeComponent();
        new ButtonTextOverflowToolTipController(ButtonHost, LabText);

        AttachedToVisualTree += (_, _) =>
        {
            SubscribeAppearance();
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        DetachedFromVisualTree += (_, _) => UnsubscribeAppearance();
        DataContextChanged += (_, _) =>
        {
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        ButtonHost.PropertyChanged += OnButtonHostPropertyChanged;
        ButtonHost.PointerExited += (_, _) =>
        {
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

    public bool UseFloatingSurface
    {
        get => GetValue(UseFloatingSurfaceProperty);
        set => SetValue(UseFloatingSurfaceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            LabText.Text = change.GetNewValue<string>();
        }
        else if (change.Property == IsEnabledProperty ||
                 change.Property == ColorTypeProperty ||
                 change.Property == UseFloatingSurfaceProperty)
        {
            RefreshVisualState();
        }
    }

    private void OnButtonHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != InputElement.IsPointerOverProperty)
        {
            return;
        }

        if (!ButtonHost.IsPointerOver)
        {
            _isPressed = false;
        }

        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        var isHovered = ButtonHost.IsPointerOver;
        var borderBrush = ResolveBorderBrush();
        var background = ResolveBackgroundBrush(isHovered);

        PanFore.BorderBrush = borderBrush;
        PanFore.Background = background;
        LabText.Foreground = borderBrush;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
    }

    private void QueueRefreshVisualState()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }

    private void SubscribeAppearance()
    {
        if (_isAppearanceSubscribed)
        {
            return;
        }

        PclTheme.Changed += OnAppearanceChanged;
        _isAppearanceSubscribed = true;
    }

    private void UnsubscribeAppearance()
    {
        if (!_isAppearanceSubscribed)
        {
            return;
        }

        PclTheme.Changed -= OnAppearanceChanged;
        _isAppearanceSubscribed = false;
    }

    private void OnAppearanceChanged()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }

    public void FocusButtonHost()
    {
        ButtonHost.Focus();
    }

    private IBrush ResolveBorderBrush()
    {
        var isHovered = ButtonHost.IsPointerOver;
        if (!IsEnabled)
        {
            return PclTheme.GetBrush("ColorBrushGray4");
        }

        return ColorType switch
        {
            PclButtonColorState.Highlight when isHovered => PclTheme.GetBrush("ColorBrush3"),
            PclButtonColorState.Highlight => PclTheme.GetBrush("ColorBrush2"),
            PclButtonColorState.Red when isHovered => PclTheme.GetBrush("ColorBrushRedLight"),
            PclButtonColorState.Red => PclTheme.GetBrush("ColorBrushRedDark"),
            _ when isHovered => PclTheme.GetBrush("ColorBrush3"),
            _ => PclTheme.GetBrush("ColorBrush1")
        };
    }

    private IBrush ResolveBackgroundBrush(bool isHovered)
    {
        if (!IsEnabled)
        {
            return ResolveIdleBackgroundBrush();
        }

        if (isHovered)
        {
            if (ColorType == PclButtonColorState.Red)
            {
                return PclTheme.GetBrush("ColorBrushSemanticDangerBackground");
            }

            return UseFloatingSurface
                ? PclTheme.GetBrush("ColorBrushMyCardMouseOver")
                : PclTheme.GetBrush("ColorBrush7");
        }

        return ResolveIdleBackgroundBrush();
    }

    private IBrush ResolveIdleBackgroundBrush()
    {
        return UseFloatingSurface
            ? PclTheme.GetBrush("ColorBrushMyCard")
            : PclTheme.GetBrush("ColorBrushHalfWhite");
    }
}

public enum PclButtonColorState
{
    Normal = 0,
    Highlight = 1,
    Red = 2
}

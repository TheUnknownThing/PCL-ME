using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclIconTextButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclIconTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclIconTextButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<PclIconTextButton, double>(nameof(IconScale), 1.0);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclIconTextButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<PclIconTextButtonColorState> ColorTypeProperty =
        AvaloniaProperty.Register<PclIconTextButton, PclIconTextButtonColorState>(nameof(ColorType), PclIconTextButtonColorState.Normal);

    public static readonly StyledProperty<bool> UseFloatingSurfaceProperty =
        AvaloniaProperty.Register<PclIconTextButton, bool>(nameof(UseFloatingSurface));

    public static readonly StyledProperty<PclIconTextButtonIconPlacement> IconPlacementProperty =
        AvaloniaProperty.Register<PclIconTextButton, PclIconTextButtonIconPlacement>(nameof(IconPlacement), PclIconTextButtonIconPlacement.Left);

    private bool _isHovered;
    private bool _isPressed;

    public event EventHandler<RoutedEventArgs>? Click;

    public PclIconTextButton()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            RefreshVisualState();
            QueueRefreshVisualState();
        };
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
        ButtonHost.Click += (_, args) => Click?.Invoke(this, args);

        UpdateIcon();
        UpdateIconPlacement();
        RefreshVisualState();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public double IconScale
    {
        get => GetValue(IconScaleProperty);
        set => SetValue(IconScaleProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public PclIconTextButtonColorState ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    public bool UseFloatingSurface
    {
        get => GetValue(UseFloatingSurfaceProperty);
        set => SetValue(UseFloatingSurfaceProperty, value);
    }

    public PclIconTextButtonIconPlacement IconPlacement
    {
        get => GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconDataProperty || change.Property == IconScaleProperty)
        {
            UpdateIcon();
        }
        else if (change.Property == ColorTypeProperty ||
                 change.Property == IsEnabledProperty ||
                 change.Property == UseFloatingSurfaceProperty)
        {
            RefreshVisualState();
        }
        else if (change.Property == IconPlacementProperty)
        {
            UpdateIconPlacement();
        }
    }

    private void UpdateIcon()
    {
        var hasIcon = !string.IsNullOrWhiteSpace(IconData);
        ShapeIcon.IsVisible = hasIcon;
        ShapeIcon.Data = hasIcon ? Geometry.Parse(IconData) : null;
        ShapeIcon.RenderTransform = new ScaleTransform(IconScale, IconScale);
    }

    private void UpdateIconPlacement()
    {
        ContentHost.Children.Clear();

        if (IconPlacement == PclIconTextButtonIconPlacement.Right)
        {
            LabText.Margin = new Thickness(12, 0, 7, 1);
            ShapeIcon.Margin = new Thickness(0, 0, 12, 0);
            ContentHost.Children.Add(LabText);
            ContentHost.Children.Add(ShapeIcon);
            return;
        }

        ShapeIcon.Margin = new Thickness(12, 0, 0, 0);
        LabText.Margin = new Thickness(7, 0, 12, 1);
        ContentHost.Children.Add(ShapeIcon);
        ContentHost.Children.Add(LabText);
    }

    private void RefreshVisualState()
    {
        if (!IsEnabled)
        {
            PanBack.Background = ResolveIdleBackgroundBrush();
            ShapeIcon.Fill = FrontendThemeResourceResolver.GetBrush("ColorBrushGray5");
            LabText.Foreground = FrontendThemeResourceResolver.GetBrush("ColorBrushGray5");
            PanBack.RenderTransform = new ScaleTransform(1, 1);
            return;
        }

        var foreground = ColorType == PclIconTextButtonColorState.Highlight
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush3")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush1");

        if (_isPressed)
        {
            PanBack.Background = FrontendThemeResourceResolver.GetBrush("ColorBrush6");
        }
        else if (_isHovered)
        {
            PanBack.Background = UseFloatingSurface
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushMyCardMouseOver")
                : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent");
            foreground = FrontendThemeResourceResolver.GetBrush("ColorBrush3");
        }
        else
        {
            PanBack.Background = ResolveIdleBackgroundBrush();
        }

        ShapeIcon.Fill = foreground;
        LabText.Foreground = foreground;
        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.97, 0.97)
            : new ScaleTransform(1, 1);
    }

    private void QueueRefreshVisualState()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }

    private IBrush ResolveIdleBackgroundBrush()
    {
        return UseFloatingSurface
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushMyCard")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent");
    }
}

internal enum PclIconTextButtonColorState
{
    Normal = 0,
    Highlight = 1
}

internal enum PclIconTextButtonIconPlacement
{
    Left = 0,
    Right = 1
}

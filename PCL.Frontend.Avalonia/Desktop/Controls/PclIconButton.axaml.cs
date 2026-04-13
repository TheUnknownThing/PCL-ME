using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclIconButton : UserControl
{
    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclIconButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclIconButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<PclIconButton, IBrush?>(
            nameof(IconBrush));

    public static readonly StyledProperty<IBrush?> HoverBackgroundBrushProperty =
        AvaloniaProperty.Register<PclIconButton, IBrush?>(
            nameof(HoverBackgroundBrush));

    public static readonly StyledProperty<IBrush?> IdleBackgroundBrushProperty =
        AvaloniaProperty.Register<PclIconButton, IBrush?>(
            nameof(IdleBackgroundBrush));

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<PclIconButton, double>(nameof(IconScale), 1.0);

    private bool _isHovered;
    private bool _isPressed;
    private bool _isAppearanceSubscribed;
    private DispatcherTimer? _releaseBounceTimer;

    public event EventHandler<RoutedEventArgs>? Click;

    public PclIconButton()
    {
        InitializeComponent();

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
            StartReleaseBounce();
        };
        ButtonHost.Click += (_, args) => Click?.Invoke(this, args);

        UpdateIcon(IconData);
        RefreshVisualState();
    }

    public string IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public IBrush? HoverBackgroundBrush
    {
        get => GetValue(HoverBackgroundBrushProperty);
        set => SetValue(HoverBackgroundBrushProperty, value);
    }

    public IBrush? IdleBackgroundBrush
    {
        get => GetValue(IdleBackgroundBrushProperty);
        set => SetValue(IdleBackgroundBrushProperty, value);
    }

    public double IconScale
    {
        get => GetValue(IconScaleProperty);
        set => SetValue(IconScaleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconDataProperty)
        {
            UpdateIcon(change.GetNewValue<string>());
        }
        else if (change.Property == IconBrushProperty ||
                 change.Property == HoverBackgroundBrushProperty ||
                 change.Property == IdleBackgroundBrushProperty ||
                 change.Property == IconScaleProperty ||
                 change.Property == IsEnabledProperty)
        {
            RefreshVisualState();
        }
    }

    private void UpdateIcon(string data)
    {
        var hasIcon = !string.IsNullOrWhiteSpace(data);
        ShapeIcon.IsVisible = hasIcon;
        ShapeIcon.Data = hasIcon ? Geometry.Parse(data) : null;
    }

    private void RefreshVisualState()
    {
        var iconBrush = IconBrush
            ?? FrontendThemeResourceResolver.GetBrush("ColorBrushGray1");
        var hoverBackgroundBrush = HoverBackgroundBrush
            ?? FrontendThemeResourceResolver.GetBrush("ColorBrush8");
        var idleBackgroundBrush = IdleBackgroundBrush
            ?? FrontendThemeResourceResolver.GetBrush("ColorBrushTransparent");

        ShapeIcon.Fill = iconBrush;
        ShapeIcon.RenderTransform = new ScaleTransform(IconScale, IconScale);
        PanBack.Background = IsEnabled && _isHovered ? hoverBackgroundBrush : idleBackgroundBrush;
        PanBack.Opacity = IsEnabled ? 1.0 : 0.2;
        PanBack.RenderTransform = _isPressed ? new ScaleTransform(0.8, 0.8) : new ScaleTransform(1, 1);
    }

    private async void StartReleaseBounce()
    {
        _releaseBounceTimer?.Stop();
        PanBack.RenderTransform = new ScaleTransform(1.05, 1.05);
        _releaseBounceTimer = new DispatcherTimer { Interval = MotionDurations.QuickState };
        _releaseBounceTimer.Tick += (_, _) =>
        {
            _releaseBounceTimer?.Stop();
            _releaseBounceTimer = null;
            if (_isPressed)
            {
                return;
            }

            RefreshVisualState();
        };
        _releaseBounceTimer.Start();
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

        FrontendAppearanceService.AppearanceChanged += OnAppearanceChanged;
        _isAppearanceSubscribed = true;
    }

    private void UnsubscribeAppearance()
    {
        if (!_isAppearanceSubscribed)
        {
            return;
        }

        FrontendAppearanceService.AppearanceChanged -= OnAppearanceChanged;
        _isAppearanceSubscribed = false;
    }

    private void OnAppearanceChanged()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }
}

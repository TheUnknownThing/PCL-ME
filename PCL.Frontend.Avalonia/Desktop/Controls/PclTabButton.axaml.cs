using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclTabButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclTabButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclTabButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PclTabButton, bool>(nameof(IsSelected));

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<PclTabButton, double>(nameof(IconScale), 1.0);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclTabButton, ICommand?>(nameof(Command));

    private bool _isHovered;
    private bool _isPressed;
    private bool _isAppearanceSubscribed;

    public PclTabButton()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        AttachedToVisualTree += (_, _) =>
        {
            SubscribeAppearance();
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        DetachedFromVisualTree += (_, _) => UnsubscribeAppearance();

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
        ButtonHost.Click += (_, _) => Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Background);

        RefreshVisualState();
        LabText.Text = Text;
        UpdateIcon(IconData);
        ApplyIconScale(IconScale);
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

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
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

    private void UpdateIcon(string data)
    {
        var hasIcon = !string.IsNullOrWhiteSpace(data);
        ShapeLogo.IsVisible = hasIcon;
        ShapeLogo.Data = hasIcon ? Geometry.Parse(data) : null;
    }

    private void ApplyIconScale(double scale)
    {
        ShapeLogo.RenderTransform = new ScaleTransform(scale, scale);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            LabText.Text = change.GetNewValue<string>();
        }
        else if (change.Property == IconDataProperty)
        {
            UpdateIcon(change.GetNewValue<string>());
        }
        else if (change.Property == IsSelectedProperty)
        {
            RefreshVisualState();
        }
        else if (change.Property == IconScaleProperty)
        {
            ApplyIconScale(change.GetNewValue<double>());
        }
    }

    private void RefreshVisualState()
    {
        var foreground = IsSelected
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarSelectedForeground")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarForeground");
        var background = IsSelected
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarSelectionBackground")
            : _isPressed
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarHoverBackground")
                : _isHovered
                    ? FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarHoverBackground")
                    : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent");

        PanBack.Background = background;
        ShapeLogo.Fill = foreground;
        LabText.Foreground = foreground;
        PanBack.Opacity = 1.0;
        PanBack.RenderTransform = TransformOperations.Parse("scale(1)");
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

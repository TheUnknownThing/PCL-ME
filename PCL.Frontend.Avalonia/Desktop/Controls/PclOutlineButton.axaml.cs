using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclOutlineButton : UserControl
{
    public static readonly StyledProperty<Thickness> TextMarginProperty =
        AvaloniaProperty.Register<PclOutlineButton, Thickness>(nameof(TextMargin), new Thickness(0, -1, 0, 0));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclOutlineButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclOutlineButton, ICommand?>(nameof(Command));

    private bool _isHovered;
    private bool _isPressed;
    private bool _isAppearanceSubscribed;

    public PclOutlineButton()
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
        PointerEntered += (_, _) =>
        {
            _isHovered = true;
            RefreshVisualState();
        };
        PointerExited += (_, _) =>
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

        RefreshVisualState();
        LabText.Text = Text;
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
        else if (change.Property == IsEnabledProperty)
        {
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        var foreground = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush3")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushGray1");
        var background = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush7")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushHalfWhite");

        PanFore.BorderBrush = foreground;
        PanFore.Background = background;
        PanFore.Opacity = _isPressed ? 0.88 : 1.0;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
        LabText.Foreground = foreground;
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

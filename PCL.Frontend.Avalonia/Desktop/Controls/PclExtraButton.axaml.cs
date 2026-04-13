using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclExtraButton : UserControl
{
    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclExtraButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclExtraButton, ICommand?>(nameof(Command));

    private bool _isHovered;
    private bool _isPressed;
    private bool _isAppearanceSubscribed;

    public PclExtraButton()
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
            RefreshVisualState();
        };

        RefreshVisualState();
        var hasIcon = !string.IsNullOrWhiteSpace(IconData);
        IconPathElement.IsVisible = hasIcon;
        IconPathElement.Data = hasIcon ? Geometry.Parse(IconData) : null;
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconDataProperty)
        {
            var data = change.GetNewValue<string>();
            IconPathElement.IsVisible = !string.IsNullOrWhiteSpace(data);
            IconPathElement.Data = string.IsNullOrWhiteSpace(data) ? null : Geometry.Parse(data);
        }
    }

    private void RefreshVisualState()
    {
        PanColor.Background = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush4")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush3");
        PanColor.Opacity = _isPressed ? 0.88 : 1.0;
        PanColor.RenderTransform = _isPressed
            ? new ScaleTransform(0.85, 0.85)
            : _isHovered
                ? new ScaleTransform(1.02, 1.02)
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

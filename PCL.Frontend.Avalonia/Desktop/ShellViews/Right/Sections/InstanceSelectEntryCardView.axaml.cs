using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right.Sections;

internal sealed partial class InstanceSelectEntryCardView : UserControl
{
    private bool _isPressed;
    private bool _isAppearanceSubscribed;

    public InstanceSelectEntryCardView()
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
            _isPressed = false;
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        LayoutRoot.PointerEntered += (_, _) => RefreshVisualState();
        LayoutRoot.PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisualState();
        };
        LayoutRoot.PointerPressed += OnPointerPressed;
        LayoutRoot.PointerReleased += OnPointerReleased;

        RefreshVisualState();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(LayoutRoot).Properties.IsLeftButtonPressed || IsPointerOverAction())
        {
            return;
        }

        _isPressed = true;
        RefreshVisualState();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPressed)
        {
            _isPressed = false;
            RefreshVisualState();
        }
    }

    private bool IsPointerOverAction()
    {
        return ActionStack.IsHitTestVisible && ActionStack.IsPointerOver;
    }

    private void RefreshVisualState()
    {
        var entry = DataContext as InstanceSelectEntryViewModel;
        var isSelected = entry?.IsSelected == true;
        var isHovered = LayoutRoot.IsPointerOver;

        BackgroundSurface.Background = isSelected
            ? GetBrush(isHovered ? "ColorBrushEntrySelectedHoverBackground" : "ColorBrushEntrySelectedBackground")
            : isHovered
                ? GetBrush("ColorBrushEntryHoverBackground")
                : GetBrush("ColorBrushTransparent");
        BackgroundSurface.Opacity = isSelected || isHovered ? 1.0 : 0.0;

        LayoutRoot.RenderTransform = _isPressed && isHovered
            ? new ScaleTransform(0.996, 0.996)
            : new ScaleTransform(1, 1);

        ActionStack.Opacity = isHovered ? 1.0 : 0.0;
        ActionStack.IsHitTestVisible = isHovered;
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
        QueueRefreshVisualState();
    }

    private static IBrush GetBrush(string resourceKey)
    {
        return FrontendThemeResourceResolver.GetBrush(resourceKey);
    }
}

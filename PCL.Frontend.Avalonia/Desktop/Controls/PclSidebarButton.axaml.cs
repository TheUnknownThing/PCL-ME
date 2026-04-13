using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

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
    private bool _isAppearanceSubscribed;

    public PclSidebarButton()
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
        var idleBrush = GetBrush("ColorBrushSemiTransparent");
        PanBack.Background = IsSelected
            ? _isHovered
                ? GetBrush("ColorBrushEntrySelectedHoverBackground")
                : GetBrush("ColorBrushEntrySelectedBackground")
            : _isHovered
                ? GetBrush("ColorBrushEntryHoverBackground")
                : idleBrush;
        PanBack.BorderBrush = IsSelected
            ? GetBrush("ColorBrush6")
            : _isHovered
                ? GetBrush("ColorBrush7")
                : idleBrush;
        PanBack.Opacity = _isPressed ? 0.92 : 1.0;
        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.985, 0.985)
            : _isHovered
                ? new ScaleTransform(1.01, 1.01)
                : new ScaleTransform(1, 1);

        TitleBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3")
            : GetBrush("ColorBrushGray1");
        SummaryBlock.Foreground = IsSelected
            ? GetBrush("ColorBrushEntrySecondarySelected")
            : GetBrush("ColorBrushEntrySecondaryIdle");
        ChevronBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3")
            : GetBrush("ColorBrushEntryChevronIdle");
        SelectionBar.IsVisible = IsSelected;
    }

    private IBrush GetBrush(string resourceKey)
    {
        return FrontendThemeResourceResolver.GetBrush(resourceKey);
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

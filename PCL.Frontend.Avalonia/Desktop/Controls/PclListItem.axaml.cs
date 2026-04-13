using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclListItem : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<string> InfoProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(Info), string.Empty);

    public static readonly StyledProperty<string> TitleSuffixProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(TitleSuffix), string.Empty);

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<PclListItem, double>(nameof(IconScale), 1.0);

    public static readonly StyledProperty<IImage?> IconSourceProperty =
        AvaloniaProperty.Register<PclListItem, IImage?>(nameof(IconSource));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PclListItem, bool>(nameof(IsSelected));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclListItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<string> ItemToolTipProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(ItemToolTip), string.Empty);

    public static readonly StyledProperty<string> SecondaryAccessoryIconDataProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(SecondaryAccessoryIconData), string.Empty);

    public static readonly StyledProperty<string> SecondaryAccessoryToolTipProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(SecondaryAccessoryToolTip), string.Empty);

    public static readonly StyledProperty<ICommand?> SecondaryAccessoryCommandProperty =
        AvaloniaProperty.Register<PclListItem, ICommand?>(nameof(SecondaryAccessoryCommand));

    public static readonly StyledProperty<string> AccessoryIconDataProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(AccessoryIconData), string.Empty);

    public static readonly StyledProperty<string> AccessoryToolTipProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(AccessoryToolTip), string.Empty);

    public static readonly StyledProperty<ICommand?> AccessoryCommandProperty =
        AvaloniaProperty.Register<PclListItem, ICommand?>(nameof(AccessoryCommand));

    public static readonly StyledProperty<bool> AccessoryIsSpinningProperty =
        AvaloniaProperty.Register<PclListItem, bool>(nameof(AccessoryIsSpinning));

    private bool _isPressed;
    private bool? _selectionBarSelectedState;
    private bool _isAppearanceSubscribed;
    private DispatcherTimer? _accessorySpinTimer;
    private double _accessorySpinAngle;

    public PclListItem()
    {
        InitializeComponent();
        SelectionBarMotion.Initialize(SelectionBar);

        AttachedToVisualTree += (_, _) =>
        {
            SubscribeAppearance();
            UpdateToolTip(ItemToolTip);
            UpdateAccessories();
            UpdateAccessorySpinState();
            _selectionBarSelectedState = null;
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        DetachedFromVisualTree += (_, _) => UnsubscribeAppearance();
        DataContextChanged += (_, _) =>
        {
            UpdateToolTip(ItemToolTip);
            UpdateAccessories();
            UpdateAccessorySpinState();
            _selectionBarSelectedState = null;
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        LayoutRoot.PropertyChanged += OnLayoutRootPropertyChanged;
        LayoutRoot.PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisualState();
        };
        LayoutRoot.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(LayoutRoot).Properties.IsLeftButtonPressed && !IsPointerOverAccessory(args))
            {
                _isPressed = true;
                RefreshVisualState();
            }
        };
        LayoutRoot.PointerReleased += (_, args) =>
        {
            if (_isPressed && args.InitialPressMouseButton == MouseButton.Left && !IsPointerOverAccessory(args))
            {
                ExecuteCommand(Command);
            }

            _isPressed = false;
            RefreshVisualState();
        };

        TitleBlock.Text = Title;
        UpdateTitleSuffix(TitleSuffix);
        UpdateInfo(Info);
        UpdateIcon(IconData);
        UpdateIconSource(IconSource);
        UpdateToolTip(ItemToolTip);
        UpdateAccessories();
        UpdateAccessorySpinState();
        ApplyIconScale(IconScale);
        RefreshVisualState();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    public string TitleSuffix
    {
        get => GetValue(TitleSuffixProperty);
        set => SetValue(TitleSuffixProperty, value);
    }

    public double IconScale
    {
        get => GetValue(IconScaleProperty);
        set => SetValue(IconScaleProperty, value);
    }

    public IImage? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
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

    public string ItemToolTip
    {
        get => GetValue(ItemToolTipProperty);
        set => SetValue(ItemToolTipProperty, value);
    }

    public string SecondaryAccessoryIconData
    {
        get => GetValue(SecondaryAccessoryIconDataProperty);
        set => SetValue(SecondaryAccessoryIconDataProperty, value);
    }

    public string SecondaryAccessoryToolTip
    {
        get => GetValue(SecondaryAccessoryToolTipProperty);
        set => SetValue(SecondaryAccessoryToolTipProperty, value);
    }

    public ICommand? SecondaryAccessoryCommand
    {
        get => GetValue(SecondaryAccessoryCommandProperty);
        set => SetValue(SecondaryAccessoryCommandProperty, value);
    }

    public string AccessoryIconData
    {
        get => GetValue(AccessoryIconDataProperty);
        set => SetValue(AccessoryIconDataProperty, value);
    }

    public string AccessoryToolTip
    {
        get => GetValue(AccessoryToolTipProperty);
        set => SetValue(AccessoryToolTipProperty, value);
    }

    public ICommand? AccessoryCommand
    {
        get => GetValue(AccessoryCommandProperty);
        set => SetValue(AccessoryCommandProperty, value);
    }

    public bool AccessoryIsSpinning
    {
        get => GetValue(AccessoryIsSpinningProperty);
        set => SetValue(AccessoryIsSpinningProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!IsComponentReady())
        {
            return;
        }

        if (change.Property == TitleProperty)
        {
            TitleBlock.Text = change.GetNewValue<string>();
        }
        else if (change.Property == TitleSuffixProperty)
        {
            UpdateTitleSuffix(change.GetNewValue<string>());
        }
        else if (change.Property == InfoProperty)
        {
            UpdateInfo(change.GetNewValue<string>());
        }
        else if (change.Property == IconDataProperty)
        {
            UpdateIcon(change.GetNewValue<string>());
        }
        else if (change.Property == IconSourceProperty)
        {
            UpdateIconSource(change.GetNewValue<IImage?>());
        }
        else if (change.Property == IconScaleProperty)
        {
            ApplyIconScale(change.GetNewValue<double>());
        }
        else if (change.Property == HeightProperty)
        {
            RefreshIconLayout();
            _selectionBarSelectedState = null;
            RefreshVisualState();
        }
        else if (change.Property == IsSelectedProperty)
        {
            RefreshVisualState();
        }
        else if (change.Property == ItemToolTipProperty)
        {
            UpdateToolTip(change.GetNewValue<string>());
        }
        else if (change.Property == SecondaryAccessoryIconDataProperty
                 || change.Property == SecondaryAccessoryCommandProperty
                 || change.Property == AccessoryIconDataProperty
                 || change.Property == AccessoryCommandProperty
                 || change.Property == AccessoryIsSpinningProperty)
        {
            UpdateAccessories();
            if (change.Property == AccessoryIsSpinningProperty)
            {
                UpdateAccessorySpinState();
            }
        }
    }

    private bool IsComponentReady()
    {
        return LayoutRoot is not null &&
               TitleBlock is not null &&
               TitleSuffixBlock is not null &&
               InfoBlock is not null &&
               LogoPath is not null &&
               LogoImage is not null &&
               MainButton is not null &&
               AccessoryHost is not null &&
               SecondaryAccessoryButton is not null &&
               SecondaryAccessoryPath is not null &&
               AccessoryButton is not null &&
               AccessoryPath is not null;
    }

    private void UpdateIcon(string data)
    {
        var hasIcon = !string.IsNullOrWhiteSpace(data);
        LogoPath.IsVisible = hasIcon && IconSource is null;
        LogoPath.Data = hasIcon ? Geometry.Parse(data) : null;
        RefreshIconLayout();
    }

    private void UpdateIconSource(IImage? source)
    {
        LogoImage.Source = source;
        LogoImage.IsVisible = source is not null;
        if (source is not null)
        {
            LogoPath.IsVisible = false;
        }

        RefreshIconLayout();
    }

    private void RefreshIconLayout()
    {
        var hasIcon = LogoPath.IsVisible || LogoImage.IsVisible;
        var isCompactLayout = double.IsNaN(Height) || Height < 40;
        var iconColumnWidth = isCompactLayout ? 18 : 22;
        LayoutRoot.ColumnDefinitions[1].Width = hasIcon ? new GridLength(14) : new GridLength(6);
        LayoutRoot.ColumnDefinitions[2].Width = hasIcon ? new GridLength(iconColumnWidth) : new GridLength(6);
        var textOffset = LogoImage.IsVisible ? 12 : 4;
        MainButton.Margin = new Thickness(textOffset, 0, 0, 0);
        TitleBlock.Margin = new Thickness(0, 0, 0, isCompactLayout ? 0 : 2);
    }

    private void UpdateInfo(string info)
    {
        var hasInfo = !string.IsNullOrWhiteSpace(info);
        InfoBlock.IsVisible = hasInfo;
        InfoBlock.Text = hasInfo ? info : string.Empty;
    }

    private void UpdateTitleSuffix(string suffix)
    {
        var hasSuffix = !string.IsNullOrWhiteSpace(suffix);
        TitleSuffixBlock.IsVisible = hasSuffix;
        TitleSuffixBlock.Text = hasSuffix ? suffix : string.Empty;
    }

    private void UpdateToolTip(string toolTip)
    {
        ToolTip.SetTip(LayoutRoot, string.IsNullOrWhiteSpace(toolTip) ? null : toolTip);
    }

    private void UpdateAccessories()
    {
        var hasSecondaryAccessory = !string.IsNullOrWhiteSpace(SecondaryAccessoryIconData) && SecondaryAccessoryCommand is not null;
        var hasAccessory = !string.IsNullOrWhiteSpace(AccessoryIconData) && AccessoryCommand is not null;
        var hasAnyAccessory = hasSecondaryAccessory || hasAccessory;
        AccessoryHost.IsVisible = hasAnyAccessory;
        SecondaryAccessoryButton.IsVisible = hasSecondaryAccessory;
        AccessoryButton.IsVisible = hasAccessory;
        SecondaryAccessoryPath.Data = hasSecondaryAccessory ? Geometry.Parse(SecondaryAccessoryIconData) : null;
        AccessoryPath.Data = hasAccessory ? Geometry.Parse(AccessoryIconData) : null;
    }

    private void UpdateAccessorySpinState()
    {
        if (!AccessoryIsSpinning)
        {
            _accessorySpinTimer?.Stop();
            _accessorySpinTimer = null;
            _accessorySpinAngle = 0;
            return;
        }

        _accessorySpinTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _accessorySpinTimer.Tick -= OnAccessorySpinTick;
        _accessorySpinTimer.Tick += OnAccessorySpinTick;
        if (!_accessorySpinTimer.IsEnabled)
        {
            _accessorySpinTimer.Start();
        }
    }

    private void OnAccessorySpinTick(object? sender, EventArgs e)
    {
        _accessorySpinAngle = (_accessorySpinAngle + 18) % 360;
        RefreshVisualState();
    }

    private void ApplyIconScale(double scale)
    {
        LogoPath.RenderTransform = new ScaleTransform(scale, scale);
        LogoImage.RenderTransform = new ScaleTransform(scale, scale);
    }

    private bool IsPointerOverAccessory(PointerEventArgs args)
    {
        return IsPointerOverButton(AccessoryButton, args) ||
               IsPointerOverButton(SecondaryAccessoryButton, args);
    }

    private static bool IsPointerOverButton(Button button, PointerEventArgs args)
    {
        if (!button.IsVisible || !button.IsHitTestVisible)
        {
            return false;
        }

        var position = args.GetPosition(button);
        return position.X >= 0 &&
               position.Y >= 0 &&
               position.X <= button.Bounds.Width &&
               position.Y <= button.Bounds.Height;
    }

    private static void ExecuteCommand(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private void OnLayoutRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != InputElement.IsPointerOverProperty)
        {
            return;
        }

        if (!LayoutRoot.IsPointerOver)
        {
            _isPressed = false;
        }

        RefreshVisualState();
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
        _accessorySpinTimer?.Stop();
        _accessorySpinTimer = null;
    }

    private void OnAppearanceChanged()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                UpdateToolTip(ItemToolTip);
                UpdateAccessories();
                RefreshVisualState();
            },
            DispatcherPriority.Render);
    }

    private double GetSelectionBarHeight()
    {
        var layoutHeight = Bounds.Height > 0
            ? Bounds.Height
            : double.IsNaN(Height)
                ? MinHeight
                : Height;
        return Math.Max(0d, layoutHeight - 14d);
    }

    private void RefreshVisualState()
    {
        var isHovered = LayoutRoot.IsPointerOver;
        var showHighlight = IsSelected || isHovered;
        RectBack.Opacity = showHighlight ? 1.0 : 0.0;
        RectBack.Background = IsSelected
            ? isHovered
                ? GetBrush("ColorBrushEntrySelectedHoverBackground")
                : GetBrush("ColorBrushEntrySelectedBackground")
            : GetBrush("ColorBrushEntryHoverBackground");
        RectBack.BorderBrush = IsSelected
            ? GetBrush("ColorBrush6")
            : GetBrush("ColorBrushTransparent");
        SelectionBarMotion.Apply(SelectionBar, ref _selectionBarSelectedState, IsSelected, GetSelectionBarHeight());
        TitleBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3")
            : GetBrush("ColorBrushGray1");
        InfoBlock.Foreground = IsSelected
            ? GetBrush("ColorBrushEntrySecondarySelected")
            : GetBrush("ColorBrushEntrySecondaryIdle");
        LogoPath.Fill = IsSelected
            ? GetBrush("ColorBrush3")
            : GetBrush("ColorBrushGray2");
        AccessoryButton.RenderTransform = new RotateTransform(_accessorySpinAngle, 0, 0);
        AccessoryPath.Fill = !AccessoryButton.IsEnabled
            ? GetBrush("ColorBrush5")
            : isHovered
                ? GetBrush("ColorBrush4")
                : GetBrush("ColorBrush5");
        SecondaryAccessoryPath.Fill = isHovered
            ? GetBrush("ColorBrush4")
            : GetBrush("ColorBrush5");
        var showAccessories = IsSelected || isHovered;
        AccessoryButton.IsHitTestVisible = AccessoryButton.IsEnabled && AccessoryButton.IsVisible && showAccessories;
        AccessoryButton.Opacity = AccessoryButton.IsVisible && showAccessories ? 1.0 : 0.0;
        SecondaryAccessoryButton.IsHitTestVisible = SecondaryAccessoryButton.IsVisible && showAccessories;
        SecondaryAccessoryButton.Opacity = SecondaryAccessoryButton.IsVisible && showAccessories ? 1.0 : 0.0;
        RenderTransform = _isPressed ? new ScaleTransform(0.985, 0.985) : new ScaleTransform(1, 1);
    }
}

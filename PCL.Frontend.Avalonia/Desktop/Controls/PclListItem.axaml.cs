using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Frontend.Avalonia.Desktop.Animation;

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

    public static readonly StyledProperty<string> AccessoryIconDataProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(AccessoryIconData), string.Empty);

    public static readonly StyledProperty<string> AccessoryToolTipProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(AccessoryToolTip), string.Empty);

    public static readonly StyledProperty<ICommand?> AccessoryCommandProperty =
        AvaloniaProperty.Register<PclListItem, ICommand?>(nameof(AccessoryCommand));

    private bool _isPressed;
    private bool? _selectionBarSelectedState;

    public PclListItem()
    {
        InitializeComponent();
        SelectionBarMotion.Initialize(SelectionBar);

        AttachedToVisualTree += (_, _) =>
        {
            UpdateAccessory();
            _selectionBarSelectedState = null;
            RefreshVisualState();
        };
        DataContextChanged += (_, _) =>
        {
            UpdateAccessory();
            _selectionBarSelectedState = null;
            RefreshVisualState();
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
        UpdateAccessory();
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
        else if (change.Property == AccessoryIconDataProperty || change.Property == AccessoryCommandProperty)
        {
            UpdateAccessory();
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

    private void UpdateAccessory()
    {
        var hasAccessory = !string.IsNullOrWhiteSpace(AccessoryIconData) && AccessoryCommand is not null;
        AccessoryButton.IsVisible = hasAccessory;
        var isHovered = LayoutRoot.IsPointerOver;
        AccessoryButton.IsHitTestVisible = hasAccessory && (IsSelected || isHovered);
        AccessoryButton.Opacity = hasAccessory && (IsSelected || isHovered) ? 1.0 : 0.0;
        AccessoryPath.Data = hasAccessory ? Geometry.Parse(AccessoryIconData) : null;
    }

    private void ApplyIconScale(double scale)
    {
        LogoPath.RenderTransform = new ScaleTransform(scale, scale);
        LogoImage.RenderTransform = new ScaleTransform(scale, scale);
    }

    private bool IsPointerOverAccessory(PointerEventArgs args)
    {
        if (!AccessoryButton.IsVisible || !AccessoryButton.IsHitTestVisible)
        {
            return false;
        }

        var position = args.GetPosition(AccessoryButton);
        return position.X >= 0 &&
               position.Y >= 0 &&
               position.X <= AccessoryButton.Bounds.Width &&
               position.Y <= AccessoryButton.Bounds.Height;
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

    private IBrush GetBrush(string resourceKey, string fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return Brush.Parse(fallback);
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
                ? GetBrush("ColorBrushEntrySelectedHoverBackground", "#DDEBFE")
                : GetBrush("ColorBrushEntrySelectedBackground", "#EAF2FE")
            : GetBrush("ColorBrushEntryHoverBackground", "#E2EEFE");
        RectBack.BorderBrush = IsSelected
            ? GetBrush("ColorBrush6", "#D5E6FD")
            : isHovered
                ? GetBrush("ColorBrush6", "#D5E6FD")
                : GetBrush("ColorBrush7", "#E0EAFD");
        SelectionBarMotion.Apply(SelectionBar, ref _selectionBarSelectedState, IsSelected, GetSelectionBarHeight());
        TitleBlock.Foreground = IsSelected
            ? GetBrush("ColorBrush3", "#1370F3")
            : GetBrush("ColorBrushGray1", "#404040");
        InfoBlock.Foreground = IsSelected
            ? GetBrush("ColorBrushEntrySecondarySelected", "#4B78C2")
            : GetBrush("ColorBrushEntrySecondaryIdle", "#7D8897");
        LogoPath.Fill = IsSelected
            ? GetBrush("ColorBrush3", "#1370F3")
            : GetBrush("ColorBrushGray2", "#737373");
        AccessoryPath.Fill = isHovered
            ? GetBrush("ColorBrush4", "#4890F5")
            : GetBrush("ColorBrush5", "#96C0F9");
        AccessoryButton.IsHitTestVisible = AccessoryButton.IsVisible && (IsSelected || isHovered);
        AccessoryButton.Opacity = AccessoryButton.IsVisible && (IsSelected || isHovered) ? 1.0 : 0.0;
        RenderTransform = _isPressed ? new ScaleTransform(0.985, 0.985) : new ScaleTransform(1, 1);
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclListItem : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<string> InfoProperty =
        AvaloniaProperty.Register<PclListItem, string>(nameof(Info), string.Empty);

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<PclListItem, double>(nameof(IconScale), 1.0);

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

    private bool _isHovered;
    private bool _isPressed;

    public PclListItem()
    {
        InitializeComponent();

        MainButton.PointerEntered += (_, _) =>
        {
            _isHovered = true;
            RefreshVisualState();
        };
        MainButton.PointerExited += (_, _) =>
        {
            _isHovered = false;
            _isPressed = false;
            RefreshVisualState();
        };
        MainButton.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(MainButton).Properties.IsLeftButtonPressed)
            {
                _isPressed = true;
                RefreshVisualState();
            }
        };
        MainButton.PointerReleased += (_, _) =>
        {
            _isPressed = false;
            RefreshVisualState();
        };

        TitleBlock.Text = Title;
        UpdateInfo(Info);
        UpdateIcon(IconData);
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

    public double IconScale
    {
        get => GetValue(IconScaleProperty);
        set => SetValue(IconScaleProperty, value);
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

        if (change.Property == TitleProperty)
        {
            TitleBlock.Text = change.GetNewValue<string>();
        }
        else if (change.Property == InfoProperty)
        {
            UpdateInfo(change.GetNewValue<string>());
        }
        else if (change.Property == IconDataProperty)
        {
            UpdateIcon(change.GetNewValue<string>());
        }
        else if (change.Property == IconScaleProperty)
        {
            ApplyIconScale(change.GetNewValue<double>());
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

    private void UpdateIcon(string data)
    {
        var hasIcon = !string.IsNullOrWhiteSpace(data);
        LogoPath.IsVisible = hasIcon;
        LogoPath.Data = hasIcon ? Geometry.Parse(data) : null;
    }

    private void UpdateInfo(string info)
    {
        var hasInfo = !string.IsNullOrWhiteSpace(info);
        InfoBlock.IsVisible = hasInfo;
        InfoBlock.Text = hasInfo ? info : string.Empty;
    }

    private void UpdateAccessory()
    {
        var hasAccessory = !string.IsNullOrWhiteSpace(AccessoryIconData) && AccessoryCommand is not null;
        AccessoryButton.IsVisible = hasAccessory;
        AccessoryPath.Data = hasAccessory ? Geometry.Parse(AccessoryIconData) : null;
    }

    private void ApplyIconScale(double scale)
    {
        LogoPath.RenderTransform = new ScaleTransform(scale, scale);
    }

    private void RefreshVisualState()
    {
        var showHighlight = IsSelected || _isHovered;
        RectBack.Opacity = showHighlight ? 1.0 : 0.0;
        RectBack.Background = IsSelected ? Brush.Parse("#EAF2FE") : Brush.Parse("#F4F8FE");
        RectBack.BorderBrush = IsSelected ? Brush.Parse("#D5E6FD") : Brush.Parse("#EDF3FC");
        SelectionBar.IsVisible = IsSelected;
        TitleBlock.Foreground = IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#404040");
        InfoBlock.Foreground = IsSelected ? Brush.Parse("#4B78C2") : Brush.Parse("#7D8897");
        LogoPath.Fill = IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#737373");
        AccessoryPath.Fill = _isHovered ? Brush.Parse("#4890F5") : Brush.Parse("#96C0F9");
        RenderTransform = _isPressed ? new ScaleTransform(0.985, 0.985) : new ScaleTransform(1, 1);
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

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

    private bool _isHovered;
    private bool _isPressed;

    public event EventHandler<RoutedEventArgs>? Click;

    public PclIconTextButton()
    {
        InitializeComponent();

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconDataProperty || change.Property == IconScaleProperty)
        {
            UpdateIcon();
        }
        else if (change.Property == ColorTypeProperty || change.Property == IsEnabledProperty)
        {
            RefreshVisualState();
        }
    }

    private void UpdateIcon()
    {
        var hasIcon = !string.IsNullOrWhiteSpace(IconData);
        ShapeIcon.IsVisible = hasIcon;
        ShapeIcon.Data = hasIcon ? Geometry.Parse(IconData) : null;
        ShapeIcon.RenderTransform = new ScaleTransform(IconScale, IconScale);
    }

    private void RefreshVisualState()
    {
        if (!IsEnabled)
        {
            PanBack.Background = Brush.Parse("#01EAF2FE");
            ShapeIcon.Fill = Brush.Parse("#CCCCCC");
            LabText.Foreground = Brush.Parse("#CCCCCC");
            PanBack.RenderTransform = new ScaleTransform(1, 1);
            return;
        }

        var foreground = ColorType == PclIconTextButtonColorState.Highlight
            ? Brush.Parse("#1370F3")
            : Brush.Parse("#343D4A");

        if (_isPressed)
        {
            PanBack.Background = Brush.Parse("#D5E6FD");
        }
        else if (_isHovered)
        {
            PanBack.Background = Brush.Parse("#BEE0EAFD");
            foreground = Brush.Parse("#1370F3");
        }
        else
        {
            PanBack.Background = Brush.Parse("#01EAF2FE");
        }

        ShapeIcon.Fill = foreground;
        LabText.Foreground = foreground;
        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.97, 0.97)
            : new ScaleTransform(1, 1);
    }
}

internal enum PclIconTextButtonColorState
{
    Normal = 0,
    Highlight = 1
}

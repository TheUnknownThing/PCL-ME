using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace PCL.Frontend.Spike.Desktop.Controls;

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

    private static readonly IBrush SelectedBackground = Brush.Parse("#FFFFFF");
    private static readonly IBrush HoverBackground = Brush.Parse("#32EAF2FE");
    private static readonly IBrush PressedBackground = Brush.Parse("#78EAF2FE");
    private static readonly IBrush IdleBackground = Brush.Parse("#01EAF2FE");
    private static readonly IBrush SelectedForeground = Brush.Parse("#1370F3");
    private static readonly IBrush IdleForeground = Brushes.White;
    private bool _isHovered;
    private bool _isPressed;

    public PclTabButton()
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
        ShapeLogo.IsVisible = !string.IsNullOrWhiteSpace(data);
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
        var foreground = IsSelected ? SelectedForeground : IdleForeground;
        var background = IsSelected
            ? SelectedBackground
            : _isPressed
                ? PressedBackground
                : _isHovered
                    ? HoverBackground
                    : IdleBackground;

        PanBack.Background = background;
        ShapeLogo.Background = foreground;
        LabText.Foreground = foreground;
        PanBack.Opacity = 1.0;
        PanBack.RenderTransform = TransformOperations.Parse("scale(1)");
    }
}

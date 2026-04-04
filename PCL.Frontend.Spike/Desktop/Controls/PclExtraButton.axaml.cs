using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclExtraButton : UserControl
{
    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclExtraButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclExtraButton, ICommand?>(nameof(Command));

    private static readonly IBrush HoverBackground = Brush.Parse("#4890F5");
    private static readonly IBrush IdleBackground = Brush.Parse("#1370F3");
    private bool _isHovered;
    private bool _isPressed;

    public PclExtraButton()
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
        IconPathElement.IsVisible = !string.IsNullOrWhiteSpace(IconData);
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
        }
    }

    private void RefreshVisualState()
    {
        PanColor.Background = _isHovered ? HoverBackground : IdleBackground;
        PanColor.Opacity = _isPressed ? 0.88 : 1.0;
        PanColor.RenderTransform = _isPressed
            ? new ScaleTransform(0.85, 0.85)
            : _isHovered
                ? new ScaleTransform(1.02, 1.02)
                : new ScaleTransform(1, 1);
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclOutlineButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclOutlineButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclOutlineButton, ICommand?>(nameof(Command));

    private static readonly IBrush HoverBorder = Brush.Parse("#1370F3");
    private static readonly IBrush IdleBorder = Brush.Parse("#404040");
    private static readonly IBrush HoverBackground = Brush.Parse("#E0EAFD");
    private static readonly IBrush IdleBackground = Brush.Parse("#55FFFFFF");
    private bool _isHovered;
    private bool _isPressed;

    public PclOutlineButton()
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            LabText.Text = change.GetNewValue<string>();
        }
    }

    private void RefreshVisualState()
    {
        PanFore.BorderBrush = _isHovered ? HoverBorder : IdleBorder;
        PanFore.Background = _isHovered ? HoverBackground : IdleBackground;
        PanFore.Opacity = _isPressed ? 0.88 : 1.0;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
        LabText.Foreground = PanFore.BorderBrush;
    }
}

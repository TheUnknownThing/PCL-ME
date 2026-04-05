using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclLaunchButton : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclLaunchButton, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<PclLaunchButton, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclLaunchButton, ICommand?>(nameof(Command));

    private static readonly IBrush HoverBorder = Brush.Parse("#1370F3");
    private static readonly IBrush IdleBorder = Brush.Parse("#0B5BCB");
    private static readonly IBrush HoverBackground = Brush.Parse("#E0EAFD");
    private static readonly IBrush IdleBackground = Brush.Parse("#55FFFFFF");
    private bool _isHovered;
    private bool _isPressed;

    public PclLaunchButton()
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
        TitleBlock.Text = Title;
        SubtitleBlock.Text = Subtitle;
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
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
        else if (change.Property == SubtitleProperty)
        {
            SubtitleBlock.Text = change.GetNewValue<string>();
        }
    }

    private void RefreshVisualState()
    {
        PanFore.BorderBrush = _isHovered ? HoverBorder : IdleBorder;
        PanFore.Background = _isHovered ? HoverBackground : IdleBackground;
        PanFore.Opacity = _isPressed ? 0.9 : 1.0;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
    }
}

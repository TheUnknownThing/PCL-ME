using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclOutlineButton : UserControl
{
    public static readonly StyledProperty<Thickness> TextMarginProperty =
        AvaloniaProperty.Register<PclOutlineButton, Thickness>(nameof(TextMargin), new Thickness(0, -1, 0, 0));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclOutlineButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclOutlineButton, ICommand?>(nameof(Command));

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

    public Thickness TextMargin
    {
        get => GetValue(TextMarginProperty);
        set => SetValue(TextMarginProperty, value);
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
        PanFore.BorderBrush = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush3")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushGray1");
        PanFore.Background = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush7")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushHalfWhite");
        PanFore.Opacity = _isPressed ? 0.88 : 1.0;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
        LabText.Foreground = PanFore.BorderBrush;
    }
}

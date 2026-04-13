using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclPromptOption : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PclPromptOption, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> DetailProperty =
        AvaloniaProperty.Register<PclPromptOption, string>(nameof(Detail), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclPromptOption, ICommand?>(nameof(Command));

    private bool _isHovered;
    private bool _isPressed;

    public PclPromptOption()
    {
        InitializeComponent();

        ButtonHost.PointerEntered += (_, _) =>
        {
            _isHovered = true;
            RefreshState();
        };
        ButtonHost.PointerExited += (_, _) =>
        {
            _isHovered = false;
            _isPressed = false;
            RefreshState();
        };
        ButtonHost.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(ButtonHost).Properties.IsLeftButtonPressed)
            {
                _isPressed = true;
                RefreshState();
            }
        };
        ButtonHost.PointerReleased += (_, _) =>
        {
            _isPressed = false;
            RefreshState();
        };

        RefreshState();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void RefreshState()
    {
        OptionBorder.Background = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush8")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiWhite");
        OptionBorder.BorderBrush = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush6")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush6");
        OptionBorder.Opacity = _isPressed ? 0.9 : 1.0;
        OptionBorder.RenderTransform = _isPressed
            ? new ScaleTransform(0.985, 0.985)
            : _isHovered
                ? new ScaleTransform(1.01, 1.01)
                : new ScaleTransform(1, 1);
        LabelBlock.Foreground = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush2")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush1");
        DetailBlock.Foreground = _isHovered
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondarySelected")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondaryIdle");
    }
}

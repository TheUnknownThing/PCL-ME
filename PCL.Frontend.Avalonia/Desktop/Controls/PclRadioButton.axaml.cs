using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclRadioButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclRadioButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclRadioButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PclRadioButton, bool>(nameof(IsChecked));

    private bool _isHovered;
    private bool _isPressed;

    public PclRadioButton()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) => RefreshVisualState();
        DataContextChanged += (_, _) => RefreshVisualState();

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

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCheckedProperty || change.Property == IsEnabledProperty)
        {
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        if (!IsEnabled)
        {
            PanBack.Background = FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent");
            LabText.Foreground = FrontendThemeResourceResolver.GetBrush("ColorBrushGray5");
            PanBack.RenderTransform = new ScaleTransform(1, 1);
            return;
        }

        if (IsChecked)
        {
            PanBack.Background = _isPressed
                ? FrontendThemeResourceResolver.GetBrush("ColorBrush2")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush3");
            LabText.Foreground = Brushes.White;
        }
        else
        {
            PanBack.Background = _isPressed
                ? FrontendThemeResourceResolver.GetBrush("ColorBrush6")
                : _isHovered
                    ? FrontendThemeResourceResolver.GetBrush("ColorBrush7")
                    : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent");
            LabText.Foreground = FrontendThemeResourceResolver.GetBrush("ColorBrush3");
        }

        PanBack.RenderTransform = _isPressed
            ? new ScaleTransform(0.97, 0.97)
            : new ScaleTransform(1, 1);
    }
}

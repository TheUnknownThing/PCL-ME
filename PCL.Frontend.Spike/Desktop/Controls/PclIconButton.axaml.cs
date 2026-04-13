using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclIconButton : UserControl
{
    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<PclIconButton, string>(nameof(IconData), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclIconButton, ICommand?>(nameof(Command));

    private static readonly IBrush IdleFill = Brush.Parse("#EAF2FE");
    private static readonly IBrush HoverBack = Brush.Parse("#32FFFFFF");
    private static readonly IBrush IdleBack = Brush.Parse("#00FFFFFF");
    private bool _isHovered;
    private bool _isPressed;
    private DispatcherTimer? _releaseBounceTimer;

    public event EventHandler<RoutedEventArgs>? Click;

    public PclIconButton()
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
            StartReleaseBounce();
        };
        ButtonHost.Click += (_, args) => Click?.Invoke(this, args);

        UpdateIcon(IconData);
        RefreshVisualState();
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
            UpdateIcon(change.GetNewValue<string>());
        }
    }

    private void UpdateIcon(string data)
    {
        var hasIcon = !string.IsNullOrWhiteSpace(data);
        ShapeIcon.IsVisible = hasIcon;
        ShapeIcon.Data = hasIcon ? Geometry.Parse(data) : null;
    }

    private void RefreshVisualState()
    {
        ShapeIcon.Fill = IdleFill;
        PanBack.Background = _isHovered ? HoverBack : IdleBack;
        PanBack.Opacity = 1.0;
        PanBack.RenderTransform = _isPressed ? new ScaleTransform(0.8, 0.8) : new ScaleTransform(1, 1);
    }

    private async void StartReleaseBounce()
    {
        _releaseBounceTimer?.Stop();
        PanBack.RenderTransform = new ScaleTransform(1.05, 1.05);
        _releaseBounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
        _releaseBounceTimer.Tick += (_, _) =>
        {
            _releaseBounceTimer?.Stop();
            _releaseBounceTimer = null;
            if (_isPressed)
            {
                return;
            }

            RefreshVisualState();
        };
        _releaseBounceTimer.Start();
    }
}

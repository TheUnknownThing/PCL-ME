using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclLaunchButton : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclLaunchButton, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<PclLaunchButton, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclLaunchButton, ICommand?>(nameof(Command));

    private bool _isHovered;
    private bool _isPressed;
    private bool _isAppearanceSubscribed;
    private ICommand? _subscribedCommand;

    public PclLaunchButton()
    {
        InitializeComponent();
        new ButtonTextOverflowToolTipController(ButtonHost, TitleBlock, SubtitleBlock);

        AttachedToVisualTree += (_, _) =>
        {
            SubscribeAppearance();
            UpdateCommandSubscription();
            RefreshVisualState();
            QueueRefreshVisualState();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            UnsubscribeAppearance();
            UnsubscribeCommand();
        };
        DataContextChanged += (_, _) =>
        {
            RefreshVisualState();
            QueueRefreshVisualState();
        };

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
        else if (change.Property == CommandProperty)
        {
            UpdateCommandSubscription();
            RefreshVisualState();
        }
    }

    private void UpdateCommandSubscription()
    {
        var command = Command;
        if (ReferenceEquals(_subscribedCommand, command))
        {
            return;
        }

        if (_subscribedCommand is not null)
        {
            _subscribedCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
        }

        _subscribedCommand = command;
        if (_subscribedCommand is not null)
        {
            _subscribedCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        }
    }

    private void UnsubscribeCommand()
    {
        if (_subscribedCommand is null)
        {
            return;
        }

        _subscribedCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
        _subscribedCommand = null;
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (Command is { } command && !command.CanExecute(null))
        {
            PanFore.BorderBrush = PclTheme.GetBrush("ColorBrushGray4");
            PanFore.Background = PclTheme.GetBrush("ColorBrushHalfWhite");
            PanFore.Opacity = 1.0;
            PanFore.RenderTransform = new ScaleTransform(1, 1);
            TitleBlock.Foreground = PclTheme.GetBrush("ColorBrushGray4");
            return;
        }

        PanFore.BorderBrush = _isHovered
            ? PclTheme.GetBrush("ColorBrush3")
            : PclTheme.GetBrush("ColorBrush2");
        PanFore.Background = _isHovered
            ? PclTheme.GetBrush("ColorBrush7")
            : PclTheme.GetBrush("ColorBrushHalfWhite");
        PanFore.Opacity = _isPressed ? 0.9 : 1.0;
        PanFore.RenderTransform = _isPressed
            ? new ScaleTransform(0.955, 0.955)
            : new ScaleTransform(1, 1);
        TitleBlock.Foreground = PclTheme.GetBrush("ColorBrush3");
    }

    private void QueueRefreshVisualState()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }

    private void SubscribeAppearance()
    {
        if (_isAppearanceSubscribed)
        {
            return;
        }

        FrontendAppearanceService.AppearanceChanged += OnAppearanceChanged;
        _isAppearanceSubscribed = true;
    }

    private void UnsubscribeAppearance()
    {
        if (!_isAppearanceSubscribed)
        {
            return;
        }

        FrontendAppearanceService.AppearanceChanged -= OnAppearanceChanged;
        _isAppearanceSubscribed = false;
    }

    private void OnAppearanceChanged()
    {
        Dispatcher.UIThread.Post(RefreshVisualState, DispatcherPriority.Render);
    }
}

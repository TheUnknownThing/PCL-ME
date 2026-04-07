using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Spike.Icons;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclLaunchRightPanel : UserControl
{
    private static readonly TimeSpan EnterOpacityDuration = TimeSpan.FromMilliseconds(170);
    private static readonly TimeSpan EnterTranslateDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan ExitOpacityDuration = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan ExitTranslateDuration = TimeSpan.FromMilliseconds(170);
    private static readonly Easing EnterOpacityEasing = new CubicEaseOut();
    private static readonly Easing EnterTranslateEasing = new BackEaseOut();
    private static readonly Easing ExitEasing = new CubicEaseOut();
    private const double EnterOffsetY = -16d;
    private const double ExitOffsetY = -10d;

    private FrontendShellViewModel? _shell;
    private int _launchHintAnimationVersion;
    private bool _isLaunchHintRendered;
    private bool _isDismissAnimationPending;

    public PclLaunchRightPanel()
    {
        InitializeComponent();
        DismissLaunchCommunityHintButton.IconData = FrontendIconCatalog.Close.Data;
        DismissLaunchCommunityHintButton.Click += OnDismissLaunchCommunityHintClick;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        QueueLaunchHintSync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = DataContext as FrontendShellViewModel;
        if (_shell is not null)
        {
            _shell.PropertyChanged += OnShellPropertyChanged;
        }

        QueueLaunchHintSync();
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontendShellViewModel.ShowLaunchCommunityHint))
        {
            QueueLaunchHintSync();
        }
    }

    private void QueueLaunchHintSync()
    {
        var version = ++_launchHintAnimationVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _launchHintAnimationVersion || VisualRoot is null)
                {
                    return;
                }

                await SyncLaunchHintAsync(version);
            },
            DispatcherPriority.Render);
    }

    private async Task SyncLaunchHintAsync(int version)
    {
        var shouldShow = _shell?.ShowLaunchCommunityHint == true;
        if (shouldShow == _isLaunchHintRendered && !_isDismissAnimationPending)
        {
            return;
        }

        if (shouldShow)
        {
            _isDismissAnimationPending = false;
            _isLaunchHintRendered = true;
            LaunchCommunityHintHost.IsVisible = true;
            PrimeLaunchHintHost();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
            if (version != _launchHintAnimationVersion || !_isLaunchHintRendered)
            {
                return;
            }

            PlayLaunchHintEnter();
            return;
        }

        if (!_isLaunchHintRendered)
        {
            LaunchCommunityHintHost.IsVisible = false;
            _isDismissAnimationPending = false;
            return;
        }

        _isDismissAnimationPending = false;
        await PlayLaunchHintExitAsync();
        if (version != _launchHintAnimationVersion)
        {
            return;
        }

        _isLaunchHintRendered = false;
        LaunchCommunityHintHost.IsVisible = false;
        PrimeLaunchHintHost();
    }

    private void PrimeLaunchHintHost()
    {
        LaunchCommunityHintHost.Transitions = null;
        LaunchCommunityHintHost.Opacity = 0;
        LaunchCommunityHintHost.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        if (LaunchCommunityHintHost.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            LaunchCommunityHintHost.RenderTransform = transform;
        }

        transform.Transitions = null;
        transform.Y = EnterOffsetY;
    }

    private void PlayLaunchHintEnter()
    {
        if (LaunchCommunityHintHost.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform(0, EnterOffsetY);
            LaunchCommunityHintHost.RenderTransform = transform;
        }

        LaunchCommunityHintHost.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = EnterOpacityDuration,
                Easing = EnterOpacityEasing
            }
        ];

        transform.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = EnterTranslateDuration,
                Easing = EnterTranslateEasing
            }
        ];

        LaunchCommunityHintHost.Opacity = 1;
        transform.Y = 0;
    }

    private async Task PlayLaunchHintExitAsync()
    {
        if (LaunchCommunityHintHost.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            LaunchCommunityHintHost.RenderTransform = transform;
        }

        LaunchCommunityHintHost.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = ExitOpacityDuration,
                Easing = ExitEasing
            }
        ];

        transform.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = ExitTranslateDuration,
                Easing = ExitEasing
            }
        ];

        LaunchCommunityHintHost.Opacity = 0;
        transform.Y = ExitOffsetY;
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(ExitOpacityDuration.TotalMilliseconds, ExitTranslateDuration.TotalMilliseconds)));
    }

    private async void OnDismissLaunchCommunityHintClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDismissAnimationPending || _shell is null || !_isLaunchHintRendered)
        {
            return;
        }

        _isDismissAnimationPending = true;
        var version = ++_launchHintAnimationVersion;
        await PlayLaunchHintExitAsync();
        if (version != _launchHintAnimationVersion)
        {
            return;
        }

        _isLaunchHintRendered = false;
        LaunchCommunityHintHost.IsVisible = false;
        PrimeLaunchHintHost();
        if (_shell.DismissLaunchCommunityHintCommand.CanExecute(null))
        {
            _shell.DismissLaunchCommunityHintCommand.Execute(null);
        }
    }
}

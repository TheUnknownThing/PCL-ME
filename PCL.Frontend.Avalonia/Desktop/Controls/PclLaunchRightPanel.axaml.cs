using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclLaunchRightPanel : UserControl
{
    private static readonly Easing RouteEnterOpacityEasing = new CubicEaseOut();
    private static readonly Easing RouteEnterTranslateEasing = new BackEaseOut();
    private const double EnterOffsetY = -16d;
    private const double ExitOffsetY = -10d;

    private FrontendShellViewModel? _shell;
    private int _launchHintAnimationVersion;
    private int _routeEnterAnimationVersion;
    private bool _isLaunchHintRendered;

    public PclLaunchRightPanel()
    {
        InitializeComponent();
        ConfigureLaunchHintMotion();
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
            () =>
            {
                if (version != _launchHintAnimationVersion || VisualRoot is null)
                {
                    return;
                }

                SyncLaunchHint();
            },
            DispatcherPriority.Render);
    }

    private void SyncLaunchHint()
    {
        var shouldShow = _shell?.ShowLaunchCommunityHint == true;
        if (shouldShow == _isLaunchHintRendered)
        {
            return;
        }

        if (shouldShow)
        {
            _isLaunchHintRendered = true;
            LaunchCommunityHintHost.IsVisible = true;
            return;
        }

        if (!_isLaunchHintRendered)
        {
            LaunchCommunityHintHost.IsVisible = false;
            return;
        }

        _isLaunchHintRendered = false;
        LaunchCommunityHintHost.IsVisible = false;
    }

    private void ConfigureLaunchHintMotion()
    {
        Motion.SetAnimateOnVisible(LaunchCommunityHintHost, true);
        Motion.SetInitialOpacity(LaunchCommunityHintHost, 0);
        Motion.SetOffsetX(LaunchCommunityHintHost, 0);
        Motion.SetOffsetY(LaunchCommunityHintHost, EnterOffsetY);
        Motion.SetOvershootTranslation(LaunchCommunityHintHost, true);
        Motion.SetExitOffsetX(LaunchCommunityHintHost, 0);
        Motion.SetExitOffsetY(LaunchCommunityHintHost, ExitOffsetY);
    }

    internal void QueueRouteEnterAnimation()
    {
        if (!_isLaunchHintRendered)
        {
            return;
        }

        var version = ++_routeEnterAnimationVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _routeEnterAnimationVersion || VisualRoot is null || !_isLaunchHintRendered)
                {
                    return;
                }

                await PlayRouteEnterAnimationAsync(version);
            },
            DispatcherPriority.Loaded);
    }

    private async Task PlayRouteEnterAnimationAsync(int version)
    {
        LaunchCommunityHintHost.Transitions = null;
        LaunchCommunityHintHost.Opacity = 0;
        LaunchCommunityHintHost.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        var transform = LaunchCommunityHintHost.RenderTransform as TranslateTransform ?? new TranslateTransform();
        transform.Transitions = null;
        transform.X = 0;
        transform.Y = EnterOffsetY;
        LaunchCommunityHintHost.RenderTransform = transform;

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _routeEnterAnimationVersion || !_isLaunchHintRendered || !LaunchCommunityHintHost.IsVisible)
        {
            return;
        }

        LaunchCommunityHintHost.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = MotionDurations.EntranceFade,
                Easing = RouteEnterOpacityEasing
            }
        ];

        transform.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = MotionDurations.EntranceTranslateOvershoot,
                Easing = RouteEnterTranslateEasing
            }
        ];

        LaunchCommunityHintHost.Opacity = 1;
        transform.Y = 0;
    }
}

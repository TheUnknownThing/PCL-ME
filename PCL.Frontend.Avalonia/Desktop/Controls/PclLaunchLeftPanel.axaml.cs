using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclLaunchLeftPanel : UserControl
{
    private static readonly Easing FadeEasing = new CubicEaseOut();
    private static readonly Easing ScaleInEasing = new BackEaseOut();
    private static readonly Easing ScaleOutEasing = new CubicEaseOut();

    private LauncherViewModel? _launcher;
    private int _launchStateAnimationVersion;
    private bool _isShowingLaunchPage;
    private bool _hasSynchronizedInitialState;
    private ScaleTransform PanInputScaleTransform => (ScaleTransform)PanInput.RenderTransform!;
    private ScaleTransform PanLaunchingScaleTransform => (ScaleTransform)PanLaunching.RenderTransform!;

    public PclLaunchLeftPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        QueueLaunchStateSync(animate: false);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_launcher is not null)
        {
            _launcher.PropertyChanged -= OnLauncherPropertyChanged;
        }

        _launcher = DataContext as LauncherViewModel;
        if (_launcher is not null)
        {
            _launcher.PropertyChanged += OnLauncherPropertyChanged;
        }

        QueueLaunchStateSync(animate: false);
    }

    private void OnLauncherPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.IsLaunchDialogVisible))
        {
            QueueLaunchStateSync(animate: true);
        }
    }

    private void QueueLaunchStateSync(bool animate)
    {
        var version = ++_launchStateAnimationVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _launchStateAnimationVersion || VisualRoot is null)
                {
                    return;
                }

                await SyncLaunchStateAsync(animate, version);
            },
            animate ? DispatcherPriority.Loaded : DispatcherPriority.Render);
    }

    private async Task SyncLaunchStateAsync(bool animate, int version)
    {
        var shouldShowLaunchPage = _launcher?.IsLaunchDialogVisible == true;
        if (!_hasSynchronizedInitialState)
        {
            ApplyLaunchStateImmediately(shouldShowLaunchPage);
            _hasSynchronizedInitialState = true;
            return;
        }

        if (shouldShowLaunchPage == _isShowingLaunchPage)
        {
            return;
        }

        if (!animate)
        {
            ApplyLaunchStateImmediately(shouldShowLaunchPage);
            return;
        }

        if (shouldShowLaunchPage)
        {
            await TransitionToLaunchPageAsync(version);
            return;
        }

        await TransitionToInputPageAsync(version);
    }

    private void ApplyLaunchStateImmediately(bool showLaunchPage)
    {
        ResetTransitions();
        if (showLaunchPage)
        {
            PanInput.IsVisible = false;
            PanInput.IsHitTestVisible = false;
            PanInput.Opacity = 0d;
            PanInputScaleTransform.ScaleX = 1.2d;
            PanInputScaleTransform.ScaleY = 1.2d;

            PanLaunching.IsVisible = true;
            PanLaunching.IsHitTestVisible = true;
            PanLaunching.Opacity = 1d;
            PanLaunchingScaleTransform.ScaleX = 1d;
            PanLaunchingScaleTransform.ScaleY = 1d;
        }
        else
        {
            PanInput.IsVisible = true;
            PanInput.IsHitTestVisible = true;
            PanInput.Opacity = 1d;
            PanInputScaleTransform.ScaleX = 1d;
            PanInputScaleTransform.ScaleY = 1d;

            PanLaunching.IsVisible = false;
            PanLaunching.IsHitTestVisible = false;
            PanLaunching.Opacity = 0d;
            PanLaunchingScaleTransform.ScaleX = 0.8d;
            PanLaunchingScaleTransform.ScaleY = 0.8d;
        }

        _isShowingLaunchPage = showLaunchPage;
    }

    private async Task TransitionToLaunchPageAsync(int version)
    {
        ResetTransitions();
        PanInput.IsVisible = true;
        PanLaunching.IsVisible = true;
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        PanInput.Opacity = 1d;
        PanInputScaleTransform.ScaleX = 1d;
        PanInputScaleTransform.ScaleY = 1d;
        PanLaunching.Opacity = 0d;
        PanLaunchingScaleTransform.ScaleX = 0.8d;
        PanLaunchingScaleTransform.ScaleY = 0.8d;

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _launchStateAnimationVersion)
        {
            return;
        }

        ConfigureInputTransitions(entering: false);
        ConfigureLaunchingTransitions(entering: true);

        PanInput.Opacity = 0d;
        PanInputScaleTransform.ScaleX = 1.2d;
        PanInputScaleTransform.ScaleY = 1.2d;
        PanLaunching.Opacity = 1d;
        PanLaunchingScaleTransform.ScaleX = 1d;
        PanLaunchingScaleTransform.ScaleY = 1d;

        await WaitForTransitionAsync();
        if (version != _launchStateAnimationVersion)
        {
            return;
        }

        PanInput.IsVisible = false;
        PanLaunching.IsHitTestVisible = true;
        _isShowingLaunchPage = true;
    }

    private async Task TransitionToInputPageAsync(int version)
    {
        ResetTransitions();
        PanInput.IsVisible = true;
        PanLaunching.IsVisible = true;
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        PanInput.Opacity = 0d;
        PanInputScaleTransform.ScaleX = 1.2d;
        PanInputScaleTransform.ScaleY = 1.2d;
        PanLaunching.Opacity = 1d;
        PanLaunchingScaleTransform.ScaleX = 1d;
        PanLaunchingScaleTransform.ScaleY = 1d;

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _launchStateAnimationVersion)
        {
            return;
        }

        ConfigureInputTransitions(entering: true);
        ConfigureLaunchingTransitions(entering: false);

        PanInput.Opacity = 1d;
        PanInputScaleTransform.ScaleX = 1d;
        PanInputScaleTransform.ScaleY = 1d;
        PanLaunching.Opacity = 0d;
        PanLaunchingScaleTransform.ScaleX = 0.8d;
        PanLaunchingScaleTransform.ScaleY = 0.8d;

        await WaitForTransitionAsync();
        if (version != _launchStateAnimationVersion)
        {
            return;
        }

        PanLaunching.IsVisible = false;
        PanInput.IsHitTestVisible = true;
        _isShowingLaunchPage = false;
    }

    private void ResetTransitions()
    {
        PanInput.Transitions = null;
        PanLaunching.Transitions = null;
        PanInputScaleTransform.Transitions = null;
        PanLaunchingScaleTransform.Transitions = null;
    }

    private void ConfigureInputTransitions(bool entering)
    {
        PanInput.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = entering ? MotionDurations.RouteTransition : MotionDurations.ExitFade,
                Easing = FadeEasing
            }
        ];
        PanInputScaleTransform.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = entering ? MotionDurations.RouteTransition : MotionDurations.SurfaceState,
                Easing = entering ? ScaleInEasing : ScaleOutEasing
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = entering ? MotionDurations.RouteTransition : MotionDurations.SurfaceState,
                Easing = entering ? ScaleInEasing : ScaleOutEasing
            }
        ];
    }

    private void ConfigureLaunchingTransitions(bool entering)
    {
        PanLaunching.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = entering ? MotionDurations.EntranceFade : MotionDurations.ExitFade,
                Easing = FadeEasing
            }
        ];
        PanLaunchingScaleTransform.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = entering ? MotionDurations.RouteTransition : MotionDurations.ExitFade,
                Easing = entering ? ScaleInEasing : ScaleOutEasing
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = entering ? MotionDurations.RouteTransition : MotionDurations.ExitFade,
                Easing = entering ? ScaleInEasing : ScaleOutEasing
            }
        ];
    }

    private static Task WaitForTransitionAsync()
    {
        var duration = MotionDurations.RouteTransition > MotionDurations.SurfaceState
            ? MotionDurations.RouteTransition
            : MotionDurations.SurfaceState;
        return Task.Delay(duration + MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(40)));
    }
}

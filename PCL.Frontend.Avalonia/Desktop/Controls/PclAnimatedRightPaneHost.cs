using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed class PclAnimatedRightPaneHost : Grid
{
    private const int EnterDelayMilliseconds = 48;
    private const int StaggerStepMilliseconds = 42;
    private const double EnterOffsetY = -18d;
    private const double ExitOffsetY = -12d;

    public static readonly StyledProperty<object?> PaneContentProperty =
        AvaloniaProperty.Register<PclAnimatedRightPaneHost, object?>(nameof(PaneContent));

    private ContentControl? _currentPresenter;
    private object? _currentContent;
    private int _transitionVersion;

    static PclAnimatedRightPaneHost()
    {
        PaneContentProperty.Changed.AddClassHandler<PclAnimatedRightPaneHost>(OnPaneContentChanged);
    }

    public object? PaneContent
    {
        get => GetValue(PaneContentProperty);
        set => SetValue(PaneContentProperty, value);
    }

    public PclAnimatedRightPaneHost()
    {
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        QueueTransition(PaneContent);
    }

    private static void OnPaneContentChanged(PclAnimatedRightPaneHost host, AvaloniaPropertyChangedEventArgs change)
    {
        host.QueueTransition(change.GetNewValue<object?>());
    }

    private void QueueTransition(object? newContent)
    {
        var version = ++_transitionVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _transitionVersion || VisualRoot is null)
                {
                    return;
                }

                await TransitionToAsync(newContent, version);
            },
            DispatcherPriority.Render);
    }

    private async Task TransitionToAsync(object? newContent, int version)
    {
        if (ReferenceEquals(_currentContent, newContent) && _currentPresenter is not null)
        {
            return;
        }

        var previousPresenter = _currentPresenter;
        ContentControl? nextPresenter = null;

        _currentContent = newContent;
        if (newContent is not null)
        {
            nextPresenter = CreatePresenter(newContent);
            ConfigureMotion(nextPresenter, previousPresenter is null ? 0 : EnterDelayMilliseconds);
            AttachPresenter(nextPresenter, zIndex: 1, opacity: 0);
            _currentPresenter = nextPresenter;
        }
        else
        {
            _currentPresenter = null;
        }

        if (previousPresenter is not null)
        {
            previousPresenter.SetValue(ZIndexProperty, 0);
            _ = RemovePresenterAfterExitAsync(previousPresenter);
        }

        if (nextPresenter is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _transitionVersion || !ReferenceEquals(nextPresenter, _currentPresenter))
        {
            return;
        }

        Motion.PrimeEnter(nextPresenter);
        nextPresenter.Opacity = 1;
        if (!nextPresenter.IsVisible)
        {
            Motion.SetAnimateOnVisible(nextPresenter, true);
            return;
        }

        await Motion.PlayEnterAsync(nextPresenter);
    }

    private async Task RemovePresenterAfterExitAsync(ContentControl presenter)
    {
        presenter.IsHitTestVisible = false;
        await Motion.PlayExitAsync(presenter);
        DetachPresenter(presenter);
    }

    private static ContentControl CreatePresenter(object content)
    {
        return new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = content
        };
    }

    private static void ConfigureMotion(Control control, int enterDelay)
    {
        Motion.SetStaggerChildren(control, true);
        Motion.SetStaggerStep(control, StaggerStepMilliseconds);
        Motion.SetDelay(control, enterDelay);
        Motion.SetInitialOpacity(control, 0);
        Motion.SetOffsetX(control, 0);
        Motion.SetOffsetY(control, EnterOffsetY);
        Motion.SetOvershootTranslation(control, true);
        Motion.SetExitOffsetX(control, 0);
        Motion.SetExitOffsetY(control, ExitOffsetY);
    }

    private void AttachPresenter(ContentControl presenter, int zIndex, double opacity)
    {
        presenter.Opacity = opacity;
        presenter.SetValue(ZIndexProperty, zIndex);
        Children.Add(presenter);
    }

    private void DetachPresenter(ContentControl presenter)
    {
        if (ReferenceEquals(presenter, _currentPresenter))
        {
            return;
        }

        Children.Remove(presenter);
    }
}

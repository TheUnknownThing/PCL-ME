using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using System.Numerics;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed class PclAnimatedRightPaneHost : Grid
{
    private const int MaxCachedPresenters = 4;
    private const double EnterOffsetY = -18d;

    public static readonly StyledProperty<object?> PaneContentProperty =
        AvaloniaProperty.Register<PclAnimatedRightPaneHost, object?>(nameof(PaneContent));

    private ContentControl? _currentPresenter;
    private object? _currentContent;
    private int _transitionVersion;
    private readonly Dictionary<object, ContentControl> _cachedPresenters = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<object> _presenterUsage = [];

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
        DataContextChanged += (_, _) =>
        {
            _cachedPresenters.Clear();
            _presenterUsage.Clear();
        };
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
            () =>
            {
                if (version != _transitionVersion || VisualRoot is null)
                {
                    return;
                }

                TransitionTo(newContent);
            },
            DispatcherPriority.Render);
    }

    private void TransitionTo(object? newContent)
    {
        if (ReferenceEquals(_currentContent, newContent) && _currentPresenter is not null)
        {
            return;
        }

        var previousPresenter = _currentPresenter;
        ContentControl? nextPresenter = null;

        _currentPresenter = null;

        // Route swaps should not render two full pane trees at once.
        DetachPresenter(previousPresenter);
        _currentContent = newContent;
        if (newContent is not null)
        {
            nextPresenter = GetOrCreatePresenter(newContent);
            AttachPresenter(nextPresenter);
            _currentPresenter = nextPresenter;
        }
        else
        {
            _currentPresenter = null;
        }

        if (nextPresenter is null)
        {
            return;
        }

        PlayEnterAnimation(nextPresenter);
    }

    private ContentControl GetOrCreatePresenter(object content)
    {
        if (_cachedPresenters.TryGetValue(content, out var presenter))
        {
            _presenterUsage.Remove(content);
            _presenterUsage.AddFirst(content);
            return presenter;
        }

        presenter = CreatePresenter(content);
        if (content is RightPaneViewModel { Descriptor.Kind:
                StandardRightPaneKind.SetupLaunch or StandardRightPaneKind.SetupUi
                or StandardRightPaneKind.SetupGameManage or StandardRightPaneKind.SetupLauncherMisc
                or StandardRightPaneKind.SetupAbout })
        {
            // Keep the view as control content: data templates may recreate their child
            // when the presenter is reattached. Its data context still flows from the pane.
            if (this.FindDataTemplate(content) is { } template)
            {
                presenter.DataContext = content;
                presenter.Content = template.Build(content);
            }

            // Dynamic resource/instance pages are deliberately excluded from this cache.
            _cachedPresenters.Add(content, presenter);
            _presenterUsage.AddFirst(content);
            if (_cachedPresenters.Count > MaxCachedPresenters)
            {
                var oldest = _presenterUsage.Last!;
                _cachedPresenters.Remove(oldest.Value);
                _presenterUsage.RemoveLast();
            }
        }

        return presenter;
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

    private static void PlayEnterAnimation(Control control)
    {
        var visual = ElementComposition.GetElementVisual(control);
        if (visual is null)
        {
            return;
        }

        var compositor = visual.Compositor;
        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.Duration = MotionDurations.EntranceTranslateOvershoot;
        offset.InsertKeyFrame(0f, new Vector3(0f, (float)EnterOffsetY, 0f));
        offset.InsertKeyFrame(1f, Vector3.Zero, new BackEaseOut());

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = MotionDurations.EntranceFade;
        opacity.InsertKeyFrame(0f, 0f);
        opacity.InsertKeyFrame(1f, 1f, new CubicEaseOut());

        // The presenter is a stable wrapper around the page, including its text.
        // Animate that wrapper on the render thread without changing layout each frame.
        visual.StartAnimation("Offset", offset);
        visual.StartAnimation("Opacity", opacity);
    }

    private void AttachPresenter(ContentControl presenter)
    {
        Children.Add(presenter);
    }

    private void DetachPresenter(ContentControl? presenter)
    {
        if (presenter is null || ReferenceEquals(presenter, _currentPresenter))
        {
            return;
        }

        var visual = ElementComposition.GetElementVisual(presenter);
        visual?.StopAnimation("Offset");
        visual?.StopAnimation("Opacity");
        Children.Remove(presenter);
    }
}

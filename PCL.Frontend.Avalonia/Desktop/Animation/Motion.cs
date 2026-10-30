using System.Runtime.CompilerServices;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class Motion
{
    public static readonly AttachedProperty<bool> AnimateOnVisibleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("AnimateOnVisible", typeof(Motion));

    public static readonly AttachedProperty<bool> StaggerChildrenProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("StaggerChildren", typeof(Motion));

    public static readonly AttachedProperty<int> StaggerStepProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("StaggerStep", typeof(Motion), 32);

    public static readonly AttachedProperty<int> DelayProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("Delay", typeof(Motion), 0);

    public static readonly AttachedProperty<bool> OvershootTranslationProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("OvershootTranslation", typeof(Motion), false);

    public static readonly AttachedProperty<double> OffsetXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("OffsetX", typeof(Motion), -18d);

    public static readonly AttachedProperty<double> OffsetYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("OffsetY", typeof(Motion), 0d);

    public static readonly AttachedProperty<double> InitialOpacityProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("InitialOpacity", typeof(Motion), 0d);

    public static readonly AttachedProperty<double> ExitOffsetXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ExitOffsetX", typeof(Motion), double.NaN);

    public static readonly AttachedProperty<double> ExitOffsetYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ExitOffsetY", typeof(Motion), double.NaN);

    private static readonly ConditionalWeakTable<Control, MotionState> States = [];

    static Motion()
    {
        AnimateOnVisibleProperty.Changed.AddClassHandler<Control>(OnAnimateOnVisibleChanged);
    }

    public static bool GetAnimateOnVisible(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(AnimateOnVisibleProperty);
    }

    public static void SetAnimateOnVisible(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(AnimateOnVisibleProperty, value);
    }

    public static bool GetStaggerChildren(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(StaggerChildrenProperty);
    }

    public static void SetStaggerChildren(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(StaggerChildrenProperty, value);
    }

    public static int GetStaggerStep(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(StaggerStepProperty);
    }

    public static void SetStaggerStep(Control control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(StaggerStepProperty, value);
    }

    public static int GetDelay(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(DelayProperty);
    }

    public static void SetDelay(Control control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(DelayProperty, value);
    }

    public static bool GetOvershootTranslation(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(OvershootTranslationProperty);
    }

    public static void SetOvershootTranslation(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(OvershootTranslationProperty, value);
    }

    public static double GetOffsetX(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(OffsetXProperty);
    }

    public static void SetOffsetX(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(OffsetXProperty, value);
    }

    public static double GetOffsetY(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(OffsetYProperty);
    }

    public static void SetOffsetY(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(OffsetYProperty, value);
    }

    public static double GetInitialOpacity(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(InitialOpacityProperty);
    }

    public static void SetInitialOpacity(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(InitialOpacityProperty, value);
    }

    public static double GetExitOffsetX(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(ExitOffsetXProperty);
    }

    public static void SetExitOffsetX(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ExitOffsetXProperty, value);
    }

    public static double GetExitOffsetY(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(ExitOffsetYProperty);
    }

    public static void SetExitOffsetY(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ExitOffsetYProperty, value);
    }

    private static void OnAnimateOnVisibleChanged(Control control, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.GetNewValue<bool>())
        {
            Attach(control);
            PrimeEnterState(control);
            QueueAnimation(control);
            return;
        }

        Detach(control);
    }

    private static void Attach(Control control)
    {
        var state = States.GetOrCreateValue(control);
        if (state.IsAttached)
        {
            return;
        }

        state.AttachedHandler = (_, _) =>
        {
            if (GetAnimateOnVisible(control))
            {
                PrimeEnterState(control);
                QueueAnimation(control);
            }
        };
        state.DetachedHandler = (_, _) => CancelAnimation(control);
        state.PropertyChangedHandler = (_, args) =>
        {
            if (args.Property != Visual.IsVisibleProperty)
            {
                return;
            }

            if (args.GetNewValue<bool>())
            {
                if (GetAnimateOnVisible(control))
                {
                    PrimeEnterState(control);
                    QueueAnimation(control);
                }
            }
            else
            {
                CancelAnimation(control);
            }
        };

        control.AttachedToVisualTree += state.AttachedHandler;
        control.DetachedFromVisualTree += state.DetachedHandler;
        control.PropertyChanged += state.PropertyChangedHandler;
        state.IsAttached = true;
    }

    private static void Detach(Control control)
    {
        if (!States.TryGetValue(control, out var state) || !state.IsAttached)
        {
            return;
        }

        if (state.AttachedHandler is not null)
        {
            control.AttachedToVisualTree -= state.AttachedHandler;
        }

        if (state.DetachedHandler is not null)
        {
            control.DetachedFromVisualTree -= state.DetachedHandler;
        }

        if (state.PropertyChangedHandler is not null)
        {
            control.PropertyChanged -= state.PropertyChangedHandler;
        }

        CancelAnimation(control);
        state.IsAttached = false;
    }

    private static void QueueAnimation(Control control)
    {
        if (!control.IsVisible)
        {
            return;
        }

        var state = States.GetOrCreateValue(control);
        state.QueuedVersion = state.Version;
        if (state.IsQueued)
        {
            return;
        }

        state.IsQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                state.IsQueued = false;
                if (state.QueuedVersion == state.Version && GetAnimateOnVisible(control))
                {
                    _ = RunAnimationAsync(control);
                }
            },
            DispatcherPriority.Loaded);
    }

    private static async Task RunAnimationAsync(Control control, Func<bool>? isCurrent = null)
    {
        if (!control.IsVisible || control.GetVisualRoot() is null)
        {
            return;
        }

        Attach(control);
        var state = States.GetOrCreateValue(control);
        if (state.PrimedTargets.Count == 0)
        {
            PrimeEnterState(control);
        }
        var version = ++state.Version;

        // Keep the host hidden while newly visible item templates realize their
        // cards. Collecting before layout can animate an empty host instead.
        await Dispatcher.UIThread.InvokeAsync(control.UpdateLayout, DispatcherPriority.Render);
        if (state.Version != version || !control.IsVisible || control.GetVisualRoot() is null
            || isCurrent?.Invoke() == false)
        {
            return;
        }

        var targets = CollectTargets(control);
        var cts = state.Cancellation = new CancellationTokenSource();
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, GetDelay(control)));
        var stagger = Math.Max(0, GetStaggerStep(control));
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            SetOpacity(target, 1);
            StartAnimation(target, control, entering: true, delay + TimeSpan.FromMilliseconds(index * stagger), state);
        }

        // The child animations and the host's restored opacity are committed
        // together, so no fully visible frame precedes the entrance.
        foreach (var primed in state.PrimedTargets)
        {
            SetOpacity(primed, 1);
        }
        state.PrimedTargets.Clear();
        var lastDelay = delay + TimeSpan.FromMilliseconds(Math.Max(0, targets.Count - 1) * stagger);
        _ = CompleteAnimationAsync(control, state, version, lastDelay + EnterTranslationDuration(control), entering: true, cts.Token);
        // Callers historically wait until the last staggered item starts, rather
        // than waiting for all entrance animations to finish.
        try
        {
            if (lastDelay > TimeSpan.Zero)
            {
                await Task.Delay(lastDelay, cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
    }

    private static bool HasTransparentStaggerTargets(Control control)
    {
        if (!GetStaggerChildren(control))
        {
            return false;
        }

        // A superseded exit can leave the item cards transparent while the host
        // itself is already visible, which looks like an empty list.
        return CollectTargets(control).Any(target => !ReferenceEquals(target, control) && target.Opacity <= 0);
    }

    private static List<Control> CollectTargets(Control control)
    {
        if (!GetStaggerChildren(control))
        {
            return [control];
        }

        return CollectStaggerTargets(control) switch
        {
            { Count: > 0 } targets => targets,
            _ => [control]
        };
    }

    private static List<Control> CollectStaggerTargets(Control control)
    {
        var descendantCards = CollectVisibleDescendantCards(control, out var firstPanelChildren, out var firstMultiChildPanel);
        if (descendantCards.Count > 0)
        {
            return descendantCards;
        }

        switch (control)
        {
            case Panel panel:
                {
                    var panelChildren = panel.Children.OfType<Control>().Where(child => child.IsVisible).ToList();
                    if (panelChildren.Count > 0)
                    {
                        return panelChildren;
                    }

                    break;
                }
            case Border border when border.Child is Control child:
                return CollectStaggerTargets(child);
            case ContentControl contentControl when contentControl.Content is Control content:
                return CollectStaggerTargets(content);
            case Decorator decorator when decorator.Child is Control child:
                return CollectStaggerTargets(child);
        }

        if (firstMultiChildPanel is not null)
        {
            return firstMultiChildPanel;
        }

        if (firstPanelChildren is not null)
        {
            return firstPanelChildren;
        }

        return [];
    }

    private static List<Control> CollectVisibleDescendantCards(
        Control control,
        out List<Control>? firstPanelChildren,
        out List<Control>? firstMultiChildPanel)
    {
        List<(PclCard Card, Point Origin)> cards = [];
        firstPanelChildren = null;
        firstMultiChildPanel = null;

        foreach (var descendant in EnumerateAnimationDescendants(control))
        {
            if (descendant is PclCard { IsVisible: true } card)
            {
                cards.Add((card, GetRelativeCardOrigin(card, control)));
            }

            if (firstMultiChildPanel is not null || descendant is not Panel panel)
            {
                continue;
            }

            var visibleChildren = CollectVisiblePanelChildren(panel);
            if (visibleChildren is null)
            {
                continue;
            }

            firstPanelChildren ??= visibleChildren;
            if (visibleChildren.Count > 1)
            {
                firstMultiChildPanel = visibleChildren;
            }
        }

        if (cards.Count == 0)
        {
            return [];
        }

        cards.Sort(static (left, right) =>
        {
            var yComparison = left.Origin.Y.CompareTo(right.Origin.Y);
            return yComparison != 0
                ? yComparison
                : left.Origin.X.CompareTo(right.Origin.X);
        });

        var targets = new List<Control>(cards.Count);
        foreach (var item in cards)
        {
            targets.Add(item.Card);
        }

        return targets;
    }

    private static IEnumerable<Visual> EnumerateAnimationDescendants(Visual parent)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control { IsVisible: false })
            {
                continue;
            }

            yield return child;
            // A card is one animation target. Animating its nested cards too
            // applies the same translation twice to their contents.
            if (child is PclCard)
            {
                continue;
            }

            foreach (var descendant in EnumerateAnimationDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static List<Control>? CollectVisiblePanelChildren(Panel panel)
    {
        List<Control>? children = null;
        foreach (var child in panel.Children)
        {
            if (child is Control { IsVisible: true } control)
            {
                children ??= [];
                children.Add(control);
            }
        }

        return children;
    }

    private static Point GetRelativeCardOrigin(Control target, Visual ancestor)
    {
        return target.TranslatePoint(default, ancestor)
               ?? new Point(target.Bounds.Left, target.Bounds.Top);
    }

    public static Task PlayEnterAsync(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return RunAnimationAsync(control);
    }

    public static void PrimeEnter(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Attach(control);
        PrimeEnterState(control);
    }

    public static bool IsAnimating(Control control)
    {
        return States.TryGetValue(control, out var state)
               && (state.PrimedTargets.Count > 0 || state.Cancellation is not null || state.IsExiting);
    }

    public static Task ShowAsync(Control control, Func<bool> isCurrent, bool replayWhenVisible = false)
    {
        if (!isCurrent())
        {
            return Task.CompletedTask;
        }

        var state = States.GetOrCreateValue(control);
        var needsReveal = control.IsVisible && (control.Opacity <= 0 || HasTransparentStaggerTargets(control));
        if (control.IsVisible && !state.IsExiting && state.PrimedTargets.Count == 0
            && !needsReveal && !replayWhenVisible)
        {
            return Task.CompletedTask;
        }

        if (state.PrimedTargets.Count == 0 || state.IsExiting || replayWhenVisible)
        {
            PrimeEnter(control);
        }
        control.IsVisible = true;
        return RunAnimationAsync(control, isCurrent);
    }

    public static async Task HideAsync(Control control, Func<bool> isCurrent)
    {
        if (!isCurrent() || !control.IsVisible)
        {
            return;
        }

        await PlayExitAsync(control);
        if (isCurrent())
        {
            control.IsVisible = false;
        }
    }

    public static async Task PlayExitAsync(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!control.IsVisible || control.GetVisualRoot() is null)
        {
            return;
        }

        Attach(control);
        CancelAnimation(control);
        var state = States.GetOrCreateValue(control);
        state.IsExiting = true;
        var version = state.Version;
        var cts = state.Cancellation = new CancellationTokenSource();
        var targets = CollectTargets(control);
        var stagger = Math.Max(0, GetStaggerStep(control));
        for (var index = 0; index < targets.Count; index++)
        {
            StartAnimation(targets[index], control, entering: false, TimeSpan.FromMilliseconds(index * stagger), state);
        }

        var duration = TimeSpan.FromMilliseconds(Math.Max(0, targets.Count - 1) * stagger) + MotionDurations.EntranceFade;
        await CompleteAnimationAsync(control, state, version, duration, entering: false, cts.Token);
    }

    private static void PrimeEnterState(Control control)
    {
        CancelAnimation(control);
        var state = States.GetOrCreateValue(control);
        SetOpacity(control, GetInitialOpacity(control));
        state.PrimedTargets.Add(control);
    }

    private static void StartAnimation(Control target, Control source, bool entering, TimeSpan delay, MotionState owner)
    {
        var visual = ElementComposition.GetElementVisual(target);
        if (visual is null)
        {
            SetOpacity(target, entering ? 1 : 0);
            return;
        }

        var targetState = States.GetOrCreateValue(target);
        var visualVersion = ++targetState.VisualVersion;
        var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
        offset.Duration = entering ? EnterTranslationDuration(source) : MotionDurations.EntranceFade;
        offset.DelayTime = delay;
        offset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        offset.SetVector3Parameter("shift", new Vector3(
            (float)(entering ? GetOffsetX(source) : ResolveExitOffsetX(source)),
            (float)(entering ? GetOffsetY(source) : ResolveExitOffsetY(source)), 0));
        // Capture the settled layout offset before committing. Explicit
        // animations do not receive this.FinalValue from the layout setter.
        // The control's hover/press RenderTransform remains independent.
        offset.InsertExpressionKeyFrame(0f, entering ? "rest + shift" : "rest");
        offset.InsertExpressionKeyFrame(1f, entering ? "rest" : "rest + shift",
            entering && GetOvershootTranslation(source) ? new BackEaseOut() : new CubicEaseOut());

        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = entering ? MotionDurations.EntranceFade : MotionDurations.ExitFade;
        opacity.DelayTime = delay;
        opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        opacity.InsertKeyFrame(0f, entering ? (float)GetInitialOpacity(source) : 1f);
        opacity.InsertKeyFrame(1f, entering ? 1f : 0f, new CubicEaseOut());
        var offsetVersion = CompositionOffsetAnimation.Start(target, offset, () =>
            offset.SetVector3Parameter("rest", new Vector3((float)target.Bounds.X, (float)target.Bounds.Y, 0)));
        visual.Compositor.RequestCompositionUpdate(() =>
        {
            if (targetState.VisualVersion == visualVersion && target.GetVisualRoot() is not null)
            {
                visual.StartAnimation("Opacity", opacity);
            }
        });
        owner.AnimatedTargets.Add(new TargetAnimation(target, visualVersion, offsetVersion));
    }

    private static TimeSpan EnterTranslationDuration(Control source) => GetOvershootTranslation(source)
        ? MotionDurations.EntranceTranslateOvershoot
        : MotionDurations.EntranceTranslate;

    private static async Task CompleteAnimationAsync(Control control, MotionState state, int version,
        TimeSpan duration, bool entering, CancellationToken cancellationToken)
    {
        try
        {
            if (ElementComposition.GetElementVisual(control) is { } visual)
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                visual.Compositor.RequestCompositionUpdate(() => started.TrySetResult());
                await started.Task.WaitAsync(cancellationToken);
            }
            await Task.Delay(duration, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (state.Version != version)
        {
            return;
        }
        foreach (var target in state.AnimatedTargets)
        {
            if (States.GetOrCreateValue(target.Control).VisualVersion != target.Version)
            {
                continue;
            }
            SetOpacity(target.Control, entering ? 1 : 0);
            ResetVisual(target);
        }
        state.AnimatedTargets.Clear();
        state.Cancellation?.Dispose();
        state.Cancellation = null;
    }

    private static void CancelAnimation(Control control)
    {
        var state = States.GetOrCreateValue(control);
        state.Version++;
        state.Cancellation?.Cancel();
        state.Cancellation?.Dispose();
        state.Cancellation = null;
        state.IsExiting = false;
        foreach (var target in state.PrimedTargets)
        {
            SetOpacity(target, 1);
            ResetOpacity(target);
        }
        state.PrimedTargets.Clear();
        foreach (var target in state.AnimatedTargets)
        {
            if (States.GetOrCreateValue(target.Control).VisualVersion != target.Version)
            {
                continue;
            }
            States.GetOrCreateValue(target.Control).VisualVersion++;
            SetOpacity(target.Control, 1);
            ResetVisual(target);
        }
        state.AnimatedTargets.Clear();
    }

    private static void ResetVisual(TargetAnimation target)
    {
        var control = target.Control;
        CompositionOffsetAnimation.Stop(control, target.OffsetVersion);
        ResetOpacity(control);
    }

    private static void ResetOpacity(Control control)
    {
        if (ElementComposition.GetElementVisual(control) is not { } visual)
        {
            return;
        }
        visual.StopAnimation("Opacity");
        visual.Opacity = control.Opacity == 0 ? 1f : 0f;
        visual.Opacity = (float)control.Opacity;
    }

    private static void SetOpacity(Control control, double opacity)
    {
        if (control.Opacity == opacity)
        {
            return;
        }
        // Preserve the caller's transitions, but do not start a UI-thread opacity
        // transition while preparing or settling a render-thread animation.
        var transitions = control.Transitions;
        control.Transitions = null;
        control.SetCurrentValue(Visual.OpacityProperty, opacity);
        control.Transitions = transitions;
    }

    private static double ResolveExitOffsetX(Control control)
    {
        var configured = GetExitOffsetX(control);
        return double.IsNaN(configured)
            ? Math.Max(14, Math.Abs(GetOffsetX(control)))
            : configured;
    }

    private static double ResolveExitOffsetY(Control control)
    {
        var configured = GetExitOffsetY(control);
        return double.IsNaN(configured)
            ? GetOffsetY(control)
            : configured;
    }

    private readonly record struct TargetAnimation(Control Control, int Version, int OffsetVersion);

    private sealed class MotionState
    {
        public EventHandler<VisualTreeAttachmentEventArgs>? AttachedHandler { get; set; }

        public EventHandler<VisualTreeAttachmentEventArgs>? DetachedHandler { get; set; }

        public EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChangedHandler { get; set; }

        public CancellationTokenSource? Cancellation { get; set; }

        public List<Control> PrimedTargets { get; } = [];

        public List<TargetAnimation> AnimatedTargets { get; } = [];

        public bool IsAttached { get; set; }

        public bool IsQueued { get; set; }

        public int QueuedVersion { get; set; }

        public bool IsExiting { get; set; }

        public int Version { get; set; }

        public int VisualVersion { get; set; }
    }
}

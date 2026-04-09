using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
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
            PrimeEnterState(control);
            QueueAnimation(control);
        };
        state.PropertyChangedHandler = (_, args) =>
        {
            if (args.Property == Visual.IsVisibleProperty && args.GetNewValue<bool>())
            {
                PrimeEnterState(control);
                QueueAnimation(control);
            }
        };

        control.AttachedToVisualTree += state.AttachedHandler;
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

        if (state.PropertyChangedHandler is not null)
        {
            control.PropertyChanged -= state.PropertyChangedHandler;
        }

        state.IsAttached = false;
    }

    private static void QueueAnimation(Control control)
    {
        if (!control.IsVisible)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _ = RunAnimationAsync(control), DispatcherPriority.Loaded);
    }

    private static async Task RunAnimationAsync(Control control)
    {
        if (!control.IsVisible)
        {
            return;
        }

        var state = States.GetOrCreateValue(control);
        var version = ++state.Version;
        var targets = CollectTargets(control);
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            PrepareInitialState(target, control);
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (state.Version != version)
        {
            return;
        }

        var delay = Math.Max(0, GetDelay(control));
        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        if (state.Version != version || !control.IsVisible)
        {
            return;
        }

        var staggerStep = Math.Max(0, GetStaggerStep(control));
        for (var index = 0; index < targets.Count; index++)
        {
            if (staggerStep > 0 && index > 0)
            {
                await Task.Delay(staggerStep);
            }

            if (state.Version != version)
            {
                return;
            }

            PlayFinalState(targets[index]);
        }
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
        var descendantCards = control
            .GetVisualDescendants()
            .OfType<PclCard>()
            .Where(card => card.IsVisible)
            .OrderBy(card => GetRelativeCardOrigin(card, control).Y)
            .ThenBy(card => GetRelativeCardOrigin(card, control).X)
            .Cast<Control>()
            .ToList();
        if (descendantCards.Count > 1)
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

        var descendantPanels = control
            .GetVisualDescendants()
            .OfType<Panel>()
            .Select(panel => panel.Children.OfType<Control>().Where(child => child.IsVisible).ToList())
            .Where(children => children.Count > 0)
            .ToList();

        var multiChildPanel = descendantPanels.FirstOrDefault(children => children.Count > 1);
        if (multiChildPanel is not null)
        {
            return multiChildPanel;
        }

        var singlePanel = descendantPanels.FirstOrDefault();
        if (singlePanel is not null)
        {
            return singlePanel;
        }

        return [];
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
        PrimeEnterState(control);
    }

    public static async Task PlayExitAsync(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!control.IsVisible)
        {
            return;
        }

        var state = States.GetOrCreateValue(control);
        var version = ++state.Version;
        var targets = CollectTargets(control);
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            PrepareExitState(target);
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);

        var staggerStep = Math.Max(0, GetStaggerStep(control));
        List<Task> exitTasks = [];
        for (var index = 0; index < targets.Count; index++)
        {
            if (staggerStep > 0 && index > 0)
            {
                await Task.Delay(staggerStep);
            }

            if (state.Version != version)
            {
                return;
            }

            exitTasks.Add(PlayExitTargetAsync(targets[index], control));
        }

        await Task.WhenAll(exitTasks);
    }

    private static void PrimeEnterState(Control control)
    {
        if (!GetAnimateOnVisible(control))
        {
            return;
        }

        foreach (var target in CollectTargets(control))
        {
            PrepareInitialState(target, control);
        }
    }

    private static void PrepareInitialState(Control target, Control motionSource)
    {
        var state = States.GetOrCreateValue(target);
        state.MotionTransitions ??= BuildTransitions(target.Transitions, entering: true);
        state.MotionTransform ??= new TranslateTransform();

        target.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        target.Transitions = null;
        state.MotionTransform.Transitions = null;
        target.Opacity = GetInitialOpacity(motionSource);
        state.MotionTransform.X = GetOffsetX(motionSource);
        state.MotionTransform.Y = GetOffsetY(motionSource);
        target.RenderTransform = state.MotionTransform;
        target.Transitions = state.MotionTransitions;
        state.MotionTransform.Transitions = BuildTransformTransitions(GetOvershootTranslation(motionSource), entering: true);
    }

    private static void PlayFinalState(Control control)
    {
        control.Opacity = 1;
        if (control.RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }
        else
        {
            control.RenderTransform = new TranslateTransform(0, 0);
        }
    }

    private static void PrepareExitState(Control control)
    {
        var state = States.GetOrCreateValue(control);
        state.MotionTransform ??= new TranslateTransform();

        var existingTransitions = control.Transitions;
        control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        control.Transitions = null;
        control.Transitions = BuildTransitions(existingTransitions, entering: false);
        state.MotionTransform.Transitions = BuildTransformTransitions(overshoot: false, entering: false);
        control.RenderTransform = state.MotionTransform;
    }

    private static async Task PlayExitTargetAsync(Control control, Control motionSource)
    {
        control.Opacity = 0;
        if (control.RenderTransform is TranslateTransform transform)
        {
            transform.X = ResolveExitOffsetX(motionSource);
            transform.Y = ResolveExitOffsetY(motionSource);
        }
        else
        {
            control.RenderTransform = new TranslateTransform(ResolveExitOffsetX(motionSource), ResolveExitOffsetY(motionSource));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(MotionDurations.ExitFade.TotalMilliseconds, MotionDurations.EntranceFade.TotalMilliseconds)));
    }

    private static Transitions BuildTransitions(Transitions? original, bool entering)
    {
        var transitions = new Transitions();
        if (original is not null)
        {
            foreach (var transition in original)
            {
                transitions.Add(transition);
            }
        }

        transitions.Add(new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = entering ? MotionDurations.EntranceFade : MotionDurations.ExitFade,
            Easing = new CubicEaseOut()
        });

        return transitions;
    }

    private static Transitions BuildTransformTransitions(bool overshoot, bool entering)
    {
        var duration = entering
            ? overshoot
                ? MotionDurations.EntranceTranslateOvershoot
                : MotionDurations.EntranceTranslate
            : MotionDurations.EntranceFade;
        Easing easing = entering && overshoot
            ? new BackEaseOut()
            : new CubicEaseOut();

        return
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = duration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = duration,
                Easing = easing
            }
        ];
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

    private sealed class MotionState
    {
        public EventHandler<VisualTreeAttachmentEventArgs>? AttachedHandler { get; set; }

        public EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChangedHandler { get; set; }

        public Transitions? MotionTransitions { get; set; }

        public TranslateTransform? MotionTransform { get; set; }

        public bool IsAttached { get; set; }

        public int Version { get; set; }
    }
}

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Frontend.Spike.Desktop.Animation;

internal static class Motion
{
    public static readonly AttachedProperty<bool> AnimateOnVisibleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("AnimateOnVisible", typeof(Motion));

    public static readonly AttachedProperty<bool> StaggerChildrenProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("StaggerChildren", typeof(Motion));

    public static readonly AttachedProperty<int> StaggerStepProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("StaggerStep", typeof(Motion), 32);

    public static readonly AttachedProperty<double> OffsetXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("OffsetX", typeof(Motion), -18d);

    public static readonly AttachedProperty<double> OffsetYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("OffsetY", typeof(Motion), 0d);

    public static readonly AttachedProperty<double> InitialOpacityProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("InitialOpacity", typeof(Motion), 0d);

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

    private static void OnAnimateOnVisibleChanged(Control control, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.GetNewValue<bool>())
        {
            Attach(control);
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

        state.AttachedHandler = (_, _) => QueueAnimation(control);
        state.PropertyChangedHandler = (_, args) =>
        {
            if (args.Property == Visual.IsVisibleProperty && args.GetNewValue<bool>())
            {
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
            PrepareInitialState(target);
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (state.Version != version)
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

        List<Control> targets = [];
        switch (control)
        {
            case Panel panel:
                targets.AddRange(panel.Children.OfType<Control>().Where(child => child.IsVisible));
                break;
            case Border border when border.Child is Control child:
                targets.Add(child);
                break;
            case ContentControl contentControl when contentControl.Content is Control content:
                targets.Add(content);
                break;
            case Decorator decorator when decorator.Child is Control child:
                targets.Add(child);
                break;
        }

        return targets.Count == 0 ? [control] : targets;
    }

    private static void PrepareInitialState(Control control)
    {
        var state = States.GetOrCreateValue(control);
        state.MotionTransitions ??= BuildTransitions(control.Transitions);

        control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        control.Transitions = null;
        control.Opacity = GetInitialOpacity(control);
        control.RenderTransform = new TranslateTransform(GetOffsetX(control), GetOffsetY(control));
        control.Transitions = state.MotionTransitions;
    }

    private static void PlayFinalState(Control control)
    {
        control.Opacity = 1;
        control.RenderTransform = new TranslateTransform(0, 0);
    }

    private static Transitions BuildTransitions(Transitions? original)
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
            Duration = TimeSpan.FromMilliseconds(170),
            Easing = new CubicEaseOut()
        });

        transitions.Add(new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(220),
            Easing = new CubicEaseOut()
        });

        return transitions;
    }

    private sealed class MotionState
    {
        public EventHandler<VisualTreeAttachmentEventArgs>? AttachedHandler { get; set; }

        public EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChangedHandler { get; set; }

        public Transitions? MotionTransitions { get; set; }

        public bool IsAttached { get; set; }

        public int Version { get; set; }
    }
}

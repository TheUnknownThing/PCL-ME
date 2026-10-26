using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

/// <summary>
/// Prevents an entrance animation's delayed cleanup from stopping a newer
/// layout animation on the same visual.
/// </summary>
internal static class CompositionOffsetAnimation
{
    private static readonly ConditionalWeakTable<Control, AnimationVersion> Versions = new();

    public static bool IsRunning(Control control) => Versions.TryGetValue(control, out var current) && current.IsRunning;

    public static int Start(Control control, CompositionAnimation animation, Action? prepare = null)
    {
        var current = Versions.GetOrCreateValue(control);
        var version = ++current.Value;
        current.IsRunning = true;
        if (ElementComposition.GetElementVisual(control) is { } visual)
        {
            // Layout may still update Offset before this batch is serialized.
            visual.Compositor.RequestCompositionUpdate(() =>
            {
                if (Versions.GetOrCreateValue(control).Value == version && control.GetVisualRoot() is not null
                    && ReferenceEquals(ElementComposition.GetElementVisual(control), visual))
                {
                    prepare?.Invoke();
                    visual.StartAnimation("Offset", animation);
                }
            });
        }
        return version;
    }

    public static void Stop(Control control, int version)
    {
        if (!Versions.TryGetValue(control, out var current) || current.Value != version)
        {
            return;
        }

        current.Value++;
        current.IsRunning = false;
        if (ElementComposition.GetElementVisual(control) is not { } visual)
        {
            return;
        }
        visual.StopAnimation("Offset");
        // Force a direct update even if the backing value already equals the
        // layout offset, clearing animations still waiting for serialization.
        visual.Offset = new Vector3D(control.Bounds.X, control.Bounds.Y, 1);
        visual.Offset = new Vector3D(control.Bounds.X, control.Bounds.Y, 0);
    }

    private sealed class AnimationVersion
    {
        public int Value;
        public bool IsRunning;
    }
}

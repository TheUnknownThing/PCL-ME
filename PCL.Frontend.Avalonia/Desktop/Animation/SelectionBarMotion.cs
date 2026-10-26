using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class SelectionBarMotion
{
    public static void Initialize(Border selectionBar)
    {
        selectionBar.RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
        selectionBar.RenderTransform = new ScaleTransform(1, 0);
        selectionBar.Transitions = CreateOpacityTransitions(isSelecting: true);
    }

    public static void Apply(Border selectionBar, ref bool? lastState, bool isSelected, double selectedHeight)
    {
        // The layout height stays constant when selection changes. A render
        // transform grows the bar from its center without remeasuring the row.
        selectionBar.Height = Math.Max(0, selectedHeight);
        if (lastState == isSelected)
        {
            return;
        }

        if (lastState is null)
        {
            var transitions = selectionBar.Transitions;
            var scale = (ScaleTransform)selectionBar.RenderTransform!;
            scale.Transitions = null;
            selectionBar.Transitions = null;
            scale.ScaleY = isSelected ? 1d : 0d;
            selectionBar.Opacity = isSelected ? 1d : 0d;
            selectionBar.Transitions = transitions;
        }
        else
        {
            var scale = (ScaleTransform)selectionBar.RenderTransform!;
            scale.Transitions =
            [
                new DoubleTransition
                {
                    Property = ScaleTransform.ScaleYProperty,
                    Duration = MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(isSelected ? 300 : 120)),
                    Easing = isSelected ? new BackEaseOut() : new CubicEaseIn()
                }
            ];
            selectionBar.Transitions = CreateOpacityTransitions(isSelected);
            scale.ScaleY = isSelected ? 1d : 0d;
            selectionBar.Opacity = isSelected ? 1d : 0d;
        }

        lastState = isSelected;
    }

    private static Transitions CreateOpacityTransitions(bool isSelecting)
    {
        return
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(isSelecting ? 30 : 70)),
                Easing = new CubicEaseOut()
            }
        ];
    }
}

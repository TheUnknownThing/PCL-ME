using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class SelectionBarMotion
{
    public static void Initialize(Border selectionBar)
    {
        selectionBar.Transitions = CreateTransitions(isSelecting: true);
    }

    public static void Apply(Border selectionBar, ref bool? lastState, bool isSelected, double selectedHeight)
    {
        if (lastState == isSelected)
        {
            return;
        }

        if (lastState is null)
        {
            var transitions = selectionBar.Transitions;
            selectionBar.Transitions = null;
            selectionBar.Height = isSelected ? selectedHeight : 0d;
            selectionBar.Opacity = isSelected ? 1d : 0d;
            selectionBar.Transitions = transitions;
        }
        else
        {
            selectionBar.Transitions = CreateTransitions(isSelected);
            selectionBar.Height = isSelected ? selectedHeight : 0d;
            selectionBar.Opacity = isSelected ? 1d : 0d;
        }

        lastState = isSelected;
    }

    private static Transitions CreateTransitions(bool isSelecting)
    {
        return
        [
            new DoubleTransition
            {
                Property = Layoutable.HeightProperty,
                Duration = MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(isSelecting ? 300 : 120)),
                Easing = isSelecting ? new BackEaseOut() : new CubicEaseIn()
            },
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(isSelecting ? 30 : 70)),
                Easing = new CubicEaseOut()
            }
        ];
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class PclModalMotion
{
    private const double EnterOvershootScale = 1.035;
    private const double ExitScale = 0.97;
    private const string IdentityTransform = "scale(1)";
    private const string ClosedTransform = "scale(0.94)";
    private static readonly string EnterOvershootTransform = $"scale({EnterOvershootScale:0.###})";
    private static readonly string ExitTransform = $"scale({ExitScale:0.###})";

    public static TimeSpan EnterOvershootDuration => MotionDurations.ModalOvershoot;

    public static TimeSpan ExitDuration => MotionDurations.ModalExit;

    public static void ResetToClosedState(Control overlay, Control panel)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(panel);

        overlay.Opacity = 0;
        panel.Opacity = 0;
        panel.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        panel.RenderTransform = TransformOperations.Parse(ClosedTransform);
    }

    public static async Task PlayOpenAsync(Control overlay, Control panel, Func<bool>? shouldContinue = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(panel);

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (shouldContinue is not null && !shouldContinue())
        {
            return;
        }

        overlay.Opacity = 1;
        panel.Opacity = 1;
        panel.RenderTransform = TransformOperations.Parse(EnterOvershootTransform);

        await Task.Delay(EnterOvershootDuration);
        if (shouldContinue is not null && !shouldContinue())
        {
            return;
        }

        panel.RenderTransform = TransformOperations.Parse(IdentityTransform);
    }

    public static async Task PlayCloseAsync(Control overlay, Control panel, Func<bool>? shouldContinue = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(panel);

        overlay.Opacity = 0;
        panel.Opacity = 0;
        panel.RenderTransform = TransformOperations.Parse(ExitTransform);

        await Task.Delay(ExitDuration);
        if (shouldContinue is not null && !shouldContinue())
        {
            return;
        }
    }
}

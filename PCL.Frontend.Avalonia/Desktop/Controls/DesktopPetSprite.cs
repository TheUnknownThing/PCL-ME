using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

/// <summary>A small pixel mascot drawn without decoded frames or per-frame bitmap allocations.</summary>
internal sealed class DesktopPetSprite : Control, IDisposable
{
    private static readonly IBrush Outline = new SolidColorBrush(Color.Parse("#253D29"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#78C850"));
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#A6DD72"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#4C943D"));
    private static readonly IBrush Face = new SolidColorBrush(Color.Parse("#203728"));
    private static readonly IBrush Shadow = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private int _frame;
    private int _interactionFrames;
    private int Bob => _interactionFrames > 0 ? -2 : _frame % 8 is 3 or 4 ? -1 : 0;
    private bool _disposed;

    public DesktopPetSprite()
    {
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => SyncAnimation();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    internal bool IsAnimationRunning => _timer.IsEnabled;

    public void SyncAnimation()
    {
        if (!_disposed && IsEffectivelyVisible && TopLevel.GetTopLevel(this) is Window { IsVisible: true, WindowState: not WindowState.Minimized })
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void Interact()
    {
        _interactionFrames = 5;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 20, Bounds.Height / 26));
        context.DrawEllipse(Shadow, null, new Point(10, 24), 7, 1);
        using var translate = context.PushTransform(Matrix.CreateTranslation(0, Bob));
        context.DrawRectangle(Outline, null, new Rect(4, 2, 12, 12));
        context.DrawRectangle(Green, null, new Rect(5, 3, 10, 10));
        context.DrawRectangle(Light, null, new Rect(5, 3, 10, 2));
        context.DrawRectangle(Dark, null, new Rect(12, 5, 3, 8));
        context.DrawRectangle(Light, null, new Rect(6, 6, 2, 1));
        context.DrawRectangle(Face, null, new Rect(6, 7, 3, 2));
        context.DrawRectangle(Face, null, new Rect(11, 7, 3, 2));
        context.DrawRectangle(Face, null, new Rect(9, 9, 2, 3));
        context.DrawRectangle(Face, null, new Rect(8, 10, 1, 3));
        context.DrawRectangle(Face, null, new Rect(11, 10, 1, 3));
        context.DrawRectangle(Outline, null, new Rect(6, 14, 8, 7));
        context.DrawRectangle(Green, null, new Rect(7, 14, 6, 6));
        context.DrawRectangle(Dark, null, new Rect(11, 14, 2, 6));
        context.DrawRectangle(Outline, null, new Rect(3, 20, 6, 4));
        context.DrawRectangle(Outline, null, new Rect(11, 20, 6, 4));
        context.DrawRectangle(Green, null, new Rect(4, 20, 4, 2));
        context.DrawRectangle(Dark, null, new Rect(12, 20, 4, 2));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var previousBob = Bob;
        _frame = (_frame + 1) % 8;
        if (_interactionFrames > 0) _interactionFrames--;
        if (previousBob != Bob) InvalidateVisual();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}

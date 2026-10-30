using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

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
    private static readonly IBrush Dirt = new SolidColorBrush(Color.Parse("#8B5B36"));
    private static readonly IBrush DirtLight = new SolidColorBrush(Color.Parse("#B08050"));
    private static readonly IBrush Ender = new SolidColorBrush(Color.Parse("#26232E"));
    private static readonly IBrush Purple = new SolidColorBrush(Color.Parse("#DC8AFF"));
    private static readonly IBrush Heart = new SolidColorBrush(Color.Parse("#FF7195"));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private int _frame;
    private int _interactionFrames;
    private int Bob => _interactionFrames > 0 ? (_interactionFrames % 4 is 0 or 1 ? -2 : 0) : _frame % 8 is 3 or 4 ? -1 : 0;
    private bool _disposed;
    private string _imagePath = string.Empty;
    private Bitmap? _customImage;
    private string _appearance = "creeper";

    public DesktopPetSprite()
    {
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => SyncAnimation();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    internal bool IsAnimationRunning => _timer.IsEnabled;
    internal Bitmap? CustomImage => _customImage;
    internal string Appearance => _appearance;
    internal bool IsReacting => _interactionFrames > 0;

    public void SetAppearance(string appearance)
    {
        var normalized = FrontendDesktopSettingsService.NormalizeAppearance(appearance);
        if (_appearance == normalized) return;
        _appearance = normalized;
        InvalidateVisual();
    }

    public void SetImagePath(string path)
    {
        if (_disposed || string.Equals(path, _imagePath, StringComparison.Ordinal)
            && (_customImage is not null || string.IsNullOrWhiteSpace(path))) return;
        var nextImage = FrontendDesktopPetImageService.TryLoad(path);
        var previous = _customImage;
        _imagePath = path;
        _customImage = nextImage;
        InvalidateVisual();
        previous?.Dispose();
    }

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
        _interactionFrames = 8;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 20, Bounds.Height / 26));
        context.DrawEllipse(Shadow, null, new Point(10, 24), 7, 1);
        if (_interactionFrames > 0)
        {
            DrawHeart(context, 1, 3);
            DrawHeart(context, 15, 1);
        }
        using var translate = context.PushTransform(Matrix.CreateTranslation(0, Bob));
        if (_customImage is { } image)
        {
            var source = new Rect(image.Size);
            var fit = Math.Min(18 / source.Width, 22 / source.Height);
            var width = source.Width * fit;
            var height = source.Height * fit;
            context.DrawImage(image, source, new Rect((20 - width) / 2, 24 - height, width, height));
            return;
        }
        switch (_appearance)
        {
            case "slime": DrawSlime(context); return;
            case "grass_block": DrawGrassBlock(context); return;
            case "enderman": DrawEnderman(context); return;
        }
        DrawCreeper(context);
    }

    private static void DrawCreeper(DrawingContext context)
    {
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

    private static void DrawSlime(DrawingContext context)
    {
        context.DrawRectangle(Outline, null, new Rect(2, 8, 16, 16));
        context.DrawRectangle(Green, null, new Rect(3, 9, 14, 14));
        context.DrawRectangle(Light, null, new Rect(3, 9, 14, 3));
        context.DrawRectangle(Dark, null, new Rect(14, 12, 3, 11));
        context.DrawRectangle(Light, null, new Rect(4, 13, 2, 6));
        context.DrawRectangle(Face, null, new Rect(6, 15, 3, 3));
        context.DrawRectangle(Face, null, new Rect(12, 15, 3, 3));
        context.DrawRectangle(Face, null, new Rect(9, 20, 3, 1));
    }

    private static void DrawGrassBlock(DrawingContext context)
    {
        context.DrawRectangle(Outline, null, new Rect(2, 7, 16, 17));
        context.DrawRectangle(Dirt, null, new Rect(3, 8, 14, 15));
        context.DrawRectangle(Light, null, new Rect(3, 8, 14, 3));
        context.DrawRectangle(Green, null, new Rect(3, 11, 14, 3));
        context.DrawRectangle(Green, null, new Rect(5, 14, 3, 2));
        context.DrawRectangle(Green, null, new Rect(13, 14, 2, 3));
        context.DrawRectangle(DirtLight, null, new Rect(4, 18, 2, 2));
        context.DrawRectangle(DirtLight, null, new Rect(14, 21, 2, 1));
        context.DrawRectangle(Face, null, new Rect(6, 16, 2, 2));
        context.DrawRectangle(Face, null, new Rect(12, 16, 2, 2));
        context.DrawRectangle(Face, null, new Rect(9, 20, 3, 1));
    }

    private static void DrawEnderman(DrawingContext context)
    {
        context.DrawRectangle(Ender, null, new Rect(4, 2, 12, 9));
        context.DrawRectangle(Ender, null, new Rect(7, 11, 6, 8));
        context.DrawRectangle(Ender, null, new Rect(4, 11, 2, 10));
        context.DrawRectangle(Ender, null, new Rect(14, 11, 2, 10));
        context.DrawRectangle(Ender, null, new Rect(7, 19, 2, 5));
        context.DrawRectangle(Ender, null, new Rect(11, 19, 2, 5));
        context.DrawRectangle(Purple, null, new Rect(5, 6, 4, 2));
        context.DrawRectangle(Purple, null, new Rect(11, 6, 4, 2));
        context.DrawRectangle(Heart, null, new Rect(2, 14, 1, 1));
        context.DrawRectangle(Purple, null, new Rect(17, 10, 1, 1));
    }

    private static void DrawHeart(DrawingContext context, int x, int y)
    {
        context.DrawRectangle(Heart, null, new Rect(x, y, 1, 1));
        context.DrawRectangle(Heart, null, new Rect(x + 2, y, 1, 1));
        context.DrawRectangle(Heart, null, new Rect(x, y + 1, 3, 1));
        context.DrawRectangle(Heart, null, new Rect(x + 1, y + 2, 1, 1));
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
        _customImage?.Dispose();
        _customImage = null;
    }
}

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

/// <summary>A vector mascot or an animated preset, with animation only while visible.</summary>
internal sealed class DesktopPetSprite : Control, IDisposable
{
    private static readonly IBrush Outline = new SolidColorBrush(Color.Parse("#253D29"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#78C850"));
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#A6DD72"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#4C943D"));
    private static readonly IBrush Face = new SolidColorBrush(Color.Parse("#203728"));
    private static readonly IBrush Shadow = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
    private static readonly IBrush Ender = new SolidColorBrush(Color.Parse("#26232E"));
    private static readonly IBrush Purple = new SolidColorBrush(Color.Parse("#DC8AFF"));
    private static readonly IBrush Heart = new SolidColorBrush(Color.Parse("#FF7195"));
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly Stopwatch _clock = new();
    private readonly bool _animate;
    private TimeSpan _idleElapsed;
    private TimeSpan _elapsed;
    private TimeSpan? _nextFrame;
    private FrontendDesktopPetImage? _idleAnimation;
    private FrontendDesktopPetImage? _clickAnimation;
    private Bitmap? _customImage;
    private Bitmap? _currentImage;
    private bool _showingClick;
    private bool _clickPending;
    private int _frame;
    private int _interactionFrames;
    private int Bob => _interactionFrames > 0 ? (_interactionFrames % 4 is 0 or 1 ? -2 : 0) : _frame % 8 is 3 or 4 ? -1 : 0;
    private bool _disposed;
    private string _imagePath = string.Empty;
    private PetPreset? _preset;

    public DesktopPetSprite(bool animateCustomImage = true)
    {
        _animate = animateCustomImage;
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => SyncAnimation();
        DetachedFromVisualTree += (_, _) => StopAnimation();
    }

    internal bool IsAnimationRunning => _timer.IsEnabled;
    internal Bitmap? CustomImage => _idleAnimation?.FirstFrame ?? _customImage;
    internal Bitmap? CurrentImage => _currentImage;
    internal bool IsReacting => _interactionFrames > 0 || _showingClick;
    internal PetPreset? Preset => _preset;

    /// <summary>Shows a user-provided static image instead of a preset.</summary>
    public void SetImagePath(string path)
    {
        if (_disposed || _preset is null && string.Equals(path, _imagePath, StringComparison.Ordinal)
            && (_customImage is not null || string.IsNullOrWhiteSpace(path))) return;
        var nextImage = FrontendDesktopPetImageService.TryLoad(path);
        DisposeAnimations();
        _preset = null;
        _imagePath = path;
        _customImage = nextImage;
        _currentImage = nextImage;
        ResetAnimationState();
        InvalidateVisual();
    }

    /// <summary>Switches to a bundled preset, optionally animating it.</summary>
    public void SetPreset(PetPreset preset, int maximumSide, bool animate = true)
    {
        if (_disposed) return;
        DisposeAnimations();
        DisposeCustomImage();
        _preset = preset;
        _imagePath = string.Empty;
        if (preset.Kind == PetPresetKind.VectorMascot)
        {
            _currentImage = null;
            ResetAnimationState();
            InvalidateVisual();
            return;
        }

        _idleAnimation = PetPresetCatalog.LoadAnimation(preset, click: false, maximumSide);
        if (animate && !preset.RepeatsIdleOnClick)
            _clickAnimation = PetPresetCatalog.LoadAnimation(preset, click: true, maximumSide);
        _currentImage = _idleAnimation?.FirstFrame;
        ResetAnimationState();
        if (_animate && _idleAnimation is not null) SyncAnimation();
        UpdateTimerInterval();
        InvalidateVisual();
    }

    public void SyncAnimation()
    {
        var needsAnimation = _idleAnimation is not null
                              || _preset?.Kind == PetPresetKind.VectorMascot;
        if (!_disposed && _animate && needsAnimation && IsEffectivelyVisible
            && TopLevel.GetTopLevel(this) is Window { IsVisible: true, WindowState: not WindowState.Minimized })
        {
            if (!_timer.IsEnabled)
            {
                _clock.Restart();
                _timer.Start();
            }
        }
        else
        {
            StopAnimation();
        }
    }

    /// <summary>Plays the preset interaction animation, or reacts for vector mascots.</summary>
    public void PlayClickAnimation()
    {
        if (_disposed) return;
        if (_idleAnimation is null)
        {
            if (_preset?.Kind == PetPresetKind.VectorMascot)
            {
                _interactionFrames = 8;
                if (!_timer.IsEnabled && _animate) SyncAnimation();
                InvalidateVisual();
            }
            return;
        }
        if (!_animate) return;
        if (_clickAnimation is null || _preset is null)
        {
            RestartAnimation();
            return;
        }
        switch (_preset.ClickMode)
        {
            case PetClickAnimationMode.WaitIdleLoopEnd:
                _clickPending = true;
                return;
            case PetClickAnimationMode.Immediate:
                StartClickAnimation();
                return;
            default:
                RestartAnimation();
                return;
        }
    }

    /// <summary>Restarts the idle animation from its first frame.</summary>
    public void RestartAnimation()
    {
        if (_idleAnimation is null) return;
        _showingClick = false;
        _clickPending = false;
        _elapsed = TimeSpan.Zero;
        _currentImage = _idleAnimation.FirstFrame;
        UpdateTimerInterval();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_idleAnimation is not null || _customImage is not null)
        {
            if (_currentImage is { } animationFrame)
            {
                var source = new Rect(animationFrame.Size);
                var fit = Math.Min(Bounds.Width / source.Width, Bounds.Height / source.Height);
                var width = source.Width * fit;
                var height = source.Height * fit;
                context.DrawImage(animationFrame, source,
                    new Rect((Bounds.Width - width) / 2, Bounds.Height - height, width, height));
            }
            return;
        }

        if (_preset?.Kind != PetPresetKind.VectorMascot) return;
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 20, Bounds.Height / 26));
        context.DrawEllipse(Shadow, null, new Point(10, 24), 7, 1);
        if (_interactionFrames > 0)
        {
            DrawHeart(context, 1, 3);
            DrawHeart(context, 15, 1);
        }
        using var translate = context.PushTransform(Matrix.CreateTranslation(0, Bob));
        switch (_preset.Id)
        {
            case "slime": DrawSlime(context); return;
            case "enderman": DrawEnderman(context); return;
        }
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
        var elapsed = _clock.Elapsed;
        _clock.Restart();
        AdvanceAnimation(elapsed);
    }

    internal void AdvanceAnimation(TimeSpan elapsed)
    {
        if (_disposed) return;
        if (_idleAnimation is not null)
        {
            AdvancePreset(elapsed);
            return;
        }

        var previousBob = Bob;
        var wasReacting = IsReacting;
        _idleElapsed += elapsed;
        var steps = (int)(_idleElapsed.Ticks / TimeSpan.FromMilliseconds(180).Ticks);
        _idleElapsed -= TimeSpan.FromMilliseconds(180 * steps);
        _frame = (_frame + steps) % 8;
        _interactionFrames = Math.Max(0, _interactionFrames - steps);
        if (previousBob != Bob || wasReacting != IsReacting) InvalidateVisual();
        UpdateTimerInterval();
    }

    private void AdvancePreset(TimeSpan elapsed)
    {
        var animation = _showingClick ? _clickAnimation! : _idleAnimation!;
        _elapsed += elapsed;
        if (_showingClick)
        {
            if (_elapsed >= animation.TotalDuration)
            {
                _showingClick = false;
                _elapsed = TimeSpan.FromTicks(_elapsed.Ticks % animation.TotalDuration.Ticks);
                animation = _idleAnimation!;
            }
        }
        else if (_elapsed >= _idleAnimation!.TotalDuration)
        {
            _elapsed = TimeSpan.FromTicks(_elapsed.Ticks % _idleAnimation.TotalDuration.Ticks);
            if (_clickPending && _clickAnimation is not null)
            {
                StartClickAnimation();
                animation = _clickAnimation;
            }
        }

        var (image, remaining) = animation.GetFrame(_elapsed);
        _nextFrame = remaining;
        var changed = !ReferenceEquals(image, _currentImage);
        _currentImage = image;
        UpdateTimerInterval();
        if (changed) InvalidateVisual();
    }

    private void StartClickAnimation()
    {
        if (_clickAnimation is null) return;
        _showingClick = true;
        _clickPending = false;
        _elapsed = TimeSpan.Zero;
        _currentImage = _clickAnimation.FirstFrame;
        UpdateTimerInterval();
        InvalidateVisual();
    }

    private void UpdateTimerInterval()
    {
        TimeSpan next;
        if (_idleAnimation is not null)
        {
            next = _nextFrame ?? TimeSpan.FromMilliseconds(100);
        }
        else
        {
            next = TimeSpan.FromMilliseconds(180) - _idleElapsed;
        }
        if (next < TimeSpan.FromMilliseconds(1)) next = TimeSpan.FromMilliseconds(1);
        _timer.Interval = next;
    }

    private void ResetAnimationState()
    {
        _idleElapsed = TimeSpan.Zero;
        _elapsed = TimeSpan.Zero;
        _nextFrame = null;
        _showingClick = false;
        _clickPending = false;
        _frame = 0;
        _interactionFrames = 0;
        _clock.Restart();
    }

    private void StopAnimation()
    {
        _timer.Stop();
        _clock.Reset();
    }

    private void DisposeCustomImage()
    {
        _customImage?.Dispose();
        _customImage = null;
    }

    private void DisposeAnimations()
    {
        _idleAnimation?.Dispose();
        _clickAnimation?.Dispose();
        _idleAnimation = null;
        _clickAnimation = null;
    }

    public void Dispose()
    {
        _disposed = true;
        StopAnimation();
        _timer.Tick -= OnTick;
        DisposeAnimations();
        DisposeCustomImage();
        _currentImage = null;
    }
}

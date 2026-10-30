using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed class DesktopPetWindow : Window
{
    private const double BaseHeightDip = 104;
    private const double CustomAspectRatio = 80d / 104d;

    private readonly DesktopPetSprite _sprite = new();
    private FrontendDesktopSettings? _appliedSettings;
    private string? _spriteKey;
    private PixelPoint _pointerStart;
    private PixelPoint _windowStart;
    private IPointer? _pointer;
    private bool _dragged;
    private int _pressClickCount;

    public DesktopPetWindow()
    {
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Width = 80;
        Height = BaseHeightDip;
        Content = _sprite;
        FrontendWindowIdentity.Apply(this);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty || e.Property == WindowStateProperty) _sprite.SyncAnimation();
        };
        Closed += (_, _) => _sprite.Dispose();
        // RenderScaling is only reliable after the window is opened: reload once at the real DPI.
        Opened += (_, _) =>
        {
            if (_appliedSettings is not { } settings) return;
            _appliedSettings = null;
            ApplySettings(settings);
        };
        Screens.Changed += OnScreensChanged;
        Closed += (_, _) => Screens.Changed -= OnScreensChanged;
    }

    /// <summary>Raised for a left click that did not turn into a drag.</summary>
    public event Action? SingleClicked;

    /// <summary>Raised when the second click of a double click completes.</summary>
    public event Action? DoubleClicked;

    public event Action<PixelPoint>? PositionSaved;

    internal DesktopPetSprite Sprite => _sprite;

    public void ApplySettings(FrontendDesktopSettings settings)
    {
        Topmost = settings.PetTopmost;
        if (_appliedSettings == settings) return;
        _appliedSettings = settings;

        var preset = PetPresetCatalog.Find(settings.PetPresetId);
        var aspect = settings.PetUseCustom ? CustomAspectRatio : preset.AspectRatio;
        PixelPoint? anchor = IsVisible ? GetBottomCenter() : null;
        Height = Math.Max(24, preset.BaseHeightDip * settings.PetScale);
        Width = Math.Max(24, Height * aspect);

        var decodeSide = PetPresetCatalog.GetDecodeSide(Height, RenderScaling);
        var spriteKey = string.Join('|', settings.PetUseCustom, settings.PetPresetId, settings.PetImagePath, decodeSide);
        if (_spriteKey != spriteKey)
        {
            _spriteKey = spriteKey;
            if (settings.PetUseCustom)
            {
                _sprite.SetImagePath(settings.PetImagePath);
            }
            else
            {
                _sprite.SetPreset(preset, decodeSide);
            }
        }

        if (anchor is { } point)
        {
            Position = new PixelPoint(
                (int)Math.Round(point.X - Width * RenderScaling / 2),
                (int)Math.Round(point.Y - Height * RenderScaling));
        }
        if (IsVisible) RestorePosition(Position);
        _sprite.SyncAnimation();
    }

    public void SetText(string title, string hint)
    {
        Title = title;
        AutomationProperties.SetName(_sprite, title);
        ToolTip.SetTip(_sprite, hint);
    }

    public void RestorePosition(PixelPoint? saved)
    {
        var screen = Screens.ScreenFromPoint(saved ?? Position) ?? Screens.Primary;
        if (screen is null) return;
        var size = PixelSize.FromSize(new Size(Width, Height), screen.Scaling);
        Position = FrontendDesktopSettingsService.RestorePosition(
            saved, size, Screens.All.Select(s => s.WorkingArea).ToArray(), screen.WorkingArea);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pointerStart = this.PointToScreen(e.GetPosition(this));
        _windowStart = Position;
        _dragged = false;
        _pressClickCount = e.ClickCount;
        _pointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer != e.Pointer) return;
        var delta = this.PointToScreen(e.GetPosition(this)) - _pointerStart;
        if (!_dragged && Math.Abs(delta.X) + Math.Abs(delta.Y) < 4 * RenderScaling) return;
        _dragged = true;
        Position = _windowStart + delta;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
        _pointer = null;
        e.Pointer.Capture(null);
        if (_dragged)
        {
            RestorePosition(Position);
            PositionSaved?.Invoke(Position);
        }
        else if (_pressClickCount >= 2)
        {
            DoubleClicked?.Invoke();
        }
        else
        {
            SingleClicked?.Invoke();
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pointer = null;
        base.OnPointerCaptureLost(e);
    }

    private PixelPoint GetBottomCenter()
    {
        var size = PixelSize.FromSize(new Size(Width, Height), RenderScaling <= 0 ? 1 : RenderScaling);
        return new PixelPoint(Position.X + size.Width / 2, Position.Y + size.Height);
    }

    private void OnScreensChanged(object? sender, EventArgs e) => RestorePosition(Position);
}

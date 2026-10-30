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
    private readonly DesktopPetSprite _sprite = new();
    private PixelPoint _pointerStart;
    private PixelPoint _windowStart;
    private IPointer? _pointer;
    private bool _dragged;

    public DesktopPetWindow()
    {
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Width = 80;
        Height = 104;
        Content = _sprite;
        FrontendWindowIdentity.Apply(this);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty || e.Property == WindowStateProperty) _sprite.SyncAnimation();
        };
        Closed += (_, _) => _sprite.Dispose();
        Screens.Changed += OnScreensChanged;
        Closed += (_, _) => Screens.Changed -= OnScreensChanged;
    }

    public event Action? RestoreRequested;
    public event Action<PixelPoint>? PositionSaved;

    internal DesktopPetSprite Sprite => _sprite;

    public void ApplySettings(FrontendDesktopSettings settings)
    {
        Topmost = settings.PetTopmost;
        Width = 80 * settings.PetScale;
        Height = 104 * settings.PetScale;
        _sprite.SetImagePath(settings.PetImagePath);
        _sprite.SetAppearance(settings.PetAppearance);
        if (IsVisible) RestorePosition(Position);
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
        else
        {
            _sprite.Interact();
            RestoreRequested?.Invoke();
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pointer = null;
        base.OnPointerCaptureLost(e);
    }

    private void OnScreensChanged(object? sender, EventArgs e) => RestorePosition(Position);
}

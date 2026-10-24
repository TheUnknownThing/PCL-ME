using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

/// <summary>
/// Advances UI-thread animations once per render tick, pausing while their owner is hidden.
/// </summary>
internal sealed class FrameAnimation : IDisposable
{
    private readonly Control _owner;
    private readonly Action<TimeSpan> _update;
    private readonly Action<TimeSpan> _onFrame;
    private readonly Stopwatch _elapsed = new();
    private readonly List<Visual> _observedVisuals = [];
    private TopLevel? _topLevel;
    private bool _isEnabled;
    private bool _isFrameQueued;
    private bool _isDisposed;

    public FrameAnimation(Control owner, Action<TimeSpan> update)
    {
        _owner = owner;
        _update = update;
        _onFrame = OnFrame;
        owner.AttachedToVisualTree += OnAttached;
        owner.DetachedFromVisualTree += OnDetached;
    }

    public bool IsRunning { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value || _isDisposed)
            {
                return;
            }

            _isEnabled = value;
            if (value && _owner.GetVisualRoot() is not null)
            {
                ObserveVisuals();
            }
            else if (!value)
            {
                UnobserveVisuals();
                _elapsed.Reset();
            }

            SyncState();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _owner.AttachedToVisualTree -= OnAttached;
        _owner.DetachedFromVisualTree -= OnDetached;
        UnobserveVisuals();
        _elapsed.Reset();
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isEnabled)
        {
            ObserveVisuals();
        }

        SyncState();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnobserveVisuals();
    }

    private void ObserveVisuals()
    {
        UnobserveVisuals();
        _topLevel = TopLevel.GetTopLevel(_owner);
        // Effective visibility has no change event in Avalonia 11. Observe ancestor
        // visibility too, so hiding a parent stops the loop without polling.
        for (Visual? visual = _owner; visual is not null; visual = visual.GetVisualParent())
        {
            _observedVisuals.Add(visual);
            visual.PropertyChanged += OnVisualPropertyChanged;
        }
    }

    private void UnobserveVisuals()
    {
        foreach (var visual in _observedVisuals)
        {
            visual.PropertyChanged -= OnVisualPropertyChanged;
        }

        _observedVisuals.Clear();
        _topLevel = null;
        IsRunning = false;
        _elapsed.Stop();
    }

    private void OnVisualPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty || e.Property == Window.WindowStateProperty)
        {
            SyncState();
        }
    }

    private void SyncState()
    {
        IsRunning = !_isDisposed && _isEnabled && _topLevel is not null
            && _owner.IsEffectivelyVisible
            && _topLevel is not Window { WindowState: WindowState.Minimized };
        if (IsRunning)
        {
            _elapsed.Start();
            QueueFrame();
        }
        else
        {
            _elapsed.Stop();
        }
    }

    private void QueueFrame()
    {
        if (!_isFrameQueued)
        {
            _isFrameQueued = true;
            _topLevel!.RequestAnimationFrame(_onFrame);
        }
    }

    private void OnFrame(TimeSpan timestamp)
    {
        _isFrameQueued = false;
        if (!IsRunning)
        {
            return;
        }

        _update(_elapsed.Elapsed);
        if (IsRunning)
        {
            QueueFrame();
        }
    }
}

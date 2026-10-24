using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Runtime.CompilerServices;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

/// <summary>
/// Reserves space for a section and builds its controls near the viewport.
/// Built content is retained when scrolling away or caching the page.
/// </summary>
internal sealed class PclDeferredContentControl : ContentControl
{
    public static readonly StyledProperty<IDataTemplate?> DeferredTemplateProperty =
        AvaloniaProperty.Register<PclDeferredContentControl, IDataTemplate?>(nameof(DeferredTemplate));
    public static readonly StyledProperty<double> EstimatedHeightProperty =
        AvaloniaProperty.Register<PclDeferredContentControl, double>(nameof(EstimatedHeight), 200);
    public static readonly StyledProperty<bool> BuildOnNextFrameProperty =
        AvaloniaProperty.Register<PclDeferredContentControl, bool>(nameof(BuildOnNextFrame));

    private static readonly ConditionalWeakTable<TopLevel, ContentBuildQueue> BuildQueues = new();
    private IScrollAnchorProvider? _scrollAnchorProvider;
    private bool _isNearViewport;
    private bool _isBuildQueued;
    private int _buildVersion;

    static PclDeferredContentControl()
    {
        AffectsMeasure<PclDeferredContentControl>(EstimatedHeightProperty);
    }

    public PclDeferredContentControl()
    {
        EffectiveViewportChanged += (_, e) =>
        {
            // Prepare a short distance ahead of scrolling without building the
            // entire page. Ordinary stack layout keeps a stable scroll origin.
            var viewport = e.EffectiveViewport;
            var buffer = Math.Max(0, viewport.Height) * .05;
            _isNearViewport = new Rect(Bounds.Size).Inflate(new Thickness(0, buffer)).Intersects(viewport);
            if (_isNearViewport) EnsureContent();
        };
    }

    public event EventHandler? ContentCreated;

    public IDataTemplate? DeferredTemplate
    {
        get => GetValue(DeferredTemplateProperty);
        set => SetValue(DeferredTemplateProperty, value);
    }

    public double EstimatedHeight
    {
        get => GetValue(EstimatedHeightProperty);
        set => SetValue(EstimatedHeightProperty, value);
    }

    public bool BuildOnNextFrame
    {
        get => GetValue(BuildOnNextFrameProperty);
        set => SetValue(BuildOnNextFrameProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scrollAnchorProvider = this.FindAncestorOfType<IScrollAnchorProvider>();
        _scrollAnchorProvider?.RegisterAnchorCandidate(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scrollAnchorProvider?.UnregisterAnchorCandidate(this);
        _scrollAnchorProvider = null;
        _isNearViewport = false;
        CancelQueuedBuild();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return Content is null && DeferredTemplate is not null
            ? new Size(0, Math.Max(0, EstimatedHeight))
            : base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DeferredTemplateProperty)
        {
            CancelQueuedBuild();
            Content = null;
        }

        if (_isNearViewport && (change.Property == DeferredTemplateProperty || change.Property == IsVisibleProperty))
        {
            EnsureContent();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Tab
            || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not { } focused
            || this.GetVisualAncestors().OfType<StackPanel>().FirstOrDefault() is not { } panel)
        {
            return;
        }

        var backwards = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (!ReferenceEquals(focused, GetTabBoundary(this, last: !backwards)))
        {
            return;
        }

        // Default tab traversal only sees built controls. At a section boundary,
        // build the adjacent section before choosing its first/last tab stop.
        // Item templates add presenters between a group and its stack panel.
        var sections = panel.Children.Select(child => child as PclDeferredContentControl
                ?? child.GetVisualDescendants().OfType<PclDeferredContentControl>().FirstOrDefault())
            .OfType<PclDeferredContentControl>().ToArray();
        var step = backwards ? -1 : 1;
        for (var index = Array.IndexOf(sections, this) + step; index >= 0 && index < sections.Length; index += step)
        {
            var section = sections[index];
            if (!section.IsEffectivelyVisible || !section.IsEffectivelyEnabled)
            {
                continue;
            }

            section.EnsureContent(immediate: true);
            section.UpdateLayout();
            if (GetTabBoundary(section, last: backwards) is not { } target)
            {
                continue;
            }

            e.Handled = target.Focus(NavigationMethod.Tab, e.KeyModifiers);
            return;
        }
    }

    private static IInputElement? GetTabBoundary(PclDeferredContentControl section, bool last)
    {
        var target = KeyboardNavigationHandler.GetNext(section, NavigationDirection.Next);
        if (target is not Visual visual || !section.IsVisualAncestorOf(visual))
        {
            return null;
        }

        if (last)
        {
            var visited = new HashSet<IInputElement> { target };
            while (KeyboardNavigationHandler.GetNext(target, NavigationDirection.Next) is { } following
                && following is Visual followingVisual && section.IsVisualAncestorOf(followingVisual)
                && visited.Add(following))
            {
                target = following;
            }
        }

        return target;
    }

    private void EnsureContent(bool immediate = false)
    {
        if (VisualRoot is not null && IsVisible && Content is null && DeferredTemplate is { } template)
        {
            if (!immediate && BuildOnNextFrame && TopLevel.GetTopLevel(this) is { } topLevel)
            {
                if (!_isBuildQueued)
                {
                    _isBuildQueued = true;
                    BuildQueues.GetValue(topLevel, static top => new ContentBuildQueue(top))
                        .Enqueue(this, _buildVersion);
                }
                return;
            }

            CancelQueuedBuild();
            // Direct control content survives reattachment without rebuilding a data template.
            Content = template.Build(DataContext);
            ContentCreated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CancelQueuedBuild()
    {
        _buildVersion++;
        _isBuildQueued = false;
    }

    private bool BuildQueuedContent(TopLevel topLevel, int version)
    {
        if (version != _buildVersion) return false;
        _isBuildQueued = false;
        if (!_isNearViewport || !IsEffectivelyVisible || VisualRoot != topLevel || Content is not null) return false;
        EnsureContent(immediate: true);
        return Content is not null;
    }

    private sealed class ContentBuildQueue(TopLevel topLevel)
    {
        private readonly Queue<(PclDeferredContentControl Section, int Version)> _pending = new();
        private bool _isScheduled;

        public void Enqueue(PclDeferredContentControl section, int version)
        {
            _pending.Enqueue((section, version));
            if (!_isScheduled)
            {
                _isScheduled = true;
                topLevel.RequestAnimationFrame(BuildNext);
            }
        }

        private void BuildNext(TimeSpan _)
        {
            // Spread visible groups across frames as well as deferring offscreen
            // groups. Scrolling away or detaching a page cancels stale requests.
            while (_pending.TryDequeue(out var entry))
            {
                if (entry.Section.BuildQueuedContent(topLevel, entry.Version)) break;
            }

            if (_pending.Count > 0) topLevel.RequestAnimationFrame(BuildNext);
            else _isScheduled = false;
        }
    }
}

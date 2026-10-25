using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using System.ComponentModel;
using System.Windows.Input;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclCard : UserControl
{
    private static readonly Thickness IdleBorderThickness = new(1);
    private static readonly Thickness HoverBorderThickness = new(1.4);
    private static readonly Thickness StandardHeaderTextMargin = new(15, 12, 0, 0);
    private static readonly Thickness CollapsibleHeaderTextMargin = new(15, 0, 40, 0);
    private static readonly Thickness StandardChevronMargin = new(0, 17, 16, 0);
    private static readonly Thickness CollapsibleChevronMargin = new(0, 12, 16, 0);
    private const double ShortHeightAnimationThreshold = 800;
    private const double LongHeightAnimationThreshold = 3000;
    private const double HeightAnimationEaseDistance = 150;
    private const double ExpandHeightPixelsPerSecond = 4000;
    private const double CollapseHeightPixelsPerSecond = 6500;
    private const double LongExpandHeightPixelsPerSecond = 6000;
    private const double LongCollapseHeightPixelsPerSecond = 8000;
    private readonly RotateTransform _chevronTransform = new();

    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<PclCard, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<bool> ShowChevronProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(ShowChevron));

    public static readonly StyledProperty<bool> IsChevronExpandedProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(IsChevronExpanded), true);

    public static readonly StyledProperty<Thickness> ContentMarginProperty =
        AvaloniaProperty.Register<PclCard, Thickness>(nameof(ContentMargin), new Thickness(20, 38, 20, 18));

    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<PclCard, object?>(nameof(CardContent));

    public static readonly StyledProperty<IDataTemplate?> CardContentTemplateProperty =
        AvaloniaProperty.Register<PclCard, IDataTemplate?>(nameof(CardContentTemplate));

    public static readonly StyledProperty<object?> HeaderOverlayContentProperty =
        AvaloniaProperty.Register<PclCard, object?>(nameof(HeaderOverlayContent));

    public static readonly StyledProperty<bool> ContentFillProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(ContentFill));

    public static readonly StyledProperty<ICommand?> HeaderCommandProperty =
        AvaloniaProperty.Register<PclCard, ICommand?>(nameof(HeaderCommand));

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<PclCard, bool>(nameof(IsClosable));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<PclCard, ICommand?>(nameof(CloseCommand));

    private bool _isHovered;
    private bool _isCloseAnimationPending;
    private bool _isContentRenderedVisible = true;
    private int _contentAnimationVersion;
    private bool _isMotionDurationSubscribed;
    private CancellationTokenSource? _contentAnimationCts;

    private readonly record struct HeightAnimationPlan(
        double TotalDistance,
        double ConstantDistance,
        TimeSpan ConstantDuration,
        double EaseDistance,
        TimeSpan EaseDuration,
        double EaseInitialPixelsPerSecond,
        bool UseSimpleEase)
    {
        public TimeSpan TotalDuration => ConstantDuration + EaseDuration;
    }

    public PclCard()
    {
        InitializeComponent();

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        HeaderButton.Click += OnHeaderButtonClick;
        CloseButton.IconData = FrontendIconCatalog.Close.Data;
        CloseButton.Click += OnCloseButtonClick;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        ConfigureChevronTransitions();
        ChevronPath.RenderTransform = _chevronTransform;
        RefreshHeaderLayout();
        RefreshHeaderMetrics();
        RefreshChevronState();
        RefreshContentState(animate: false);
        RefreshState();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isMotionDurationSubscribed)
        {
            return;
        }

        MotionDurations.Changed += OnMotionDurationsChanged;
        _isMotionDurationSubscribed = true;
        ConfigureChevronTransitions();
        RefreshContentState(animate: false);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopContentAnimation();
        if (!_isMotionDurationSubscribed)
        {
            return;
        }

        MotionDurations.Changed -= OnMotionDurationsChanged;
        _isMotionDurationSubscribed = false;
    }

    private void OnMotionDurationsChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(ConfigureChevronTransitions);
    }

    private void ConfigureChevronTransitions()
    {
        _chevronTransform.Transitions =
        [
            new DoubleTransition
            {
                Property = RotateTransform.AngleProperty,
                Duration = MotionDurations.SurfaceState,
                Easing = new CubicEaseOut()
            }
        ];
    }

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowChevron
    {
        get => GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    public bool IsChevronExpanded
    {
        get => GetValue(IsChevronExpandedProperty);
        set => SetValue(IsChevronExpandedProperty, value);
    }

    public Thickness ContentMargin
    {
        get => GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public IDataTemplate? CardContentTemplate
    {
        get => GetValue(CardContentTemplateProperty);
        set => SetValue(CardContentTemplateProperty, value);
    }

    public object? HeaderOverlayContent
    {
        get => GetValue(HeaderOverlayContentProperty);
        set => SetValue(HeaderOverlayContentProperty, value);
    }

    public bool ContentFill
    {
        get => GetValue(ContentFillProperty);
        set => SetValue(ContentFillProperty, value);
    }

    public ICommand? HeaderCommand
    {
        get => GetValue(HeaderCommandProperty);
        set => SetValue(HeaderCommandProperty, value);
    }

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public bool HasHeader => !string.IsNullOrWhiteSpace(Header);

    public bool HasHeaderOverlayContent => HeaderOverlayContent is not null;

    public bool IsContentVisible => !ShowChevron || _isContentRenderedVisible;

    public bool ShowCloseButton => IsClosable && !ShowChevron && CloseCommand is not null;

    public Thickness EffectiveContentMargin
    {
        get
        {
            if (!HasHeader)
            {
                return ContentMargin;
            }

            if (ShowChevron)
            {
                return new Thickness(
                    ContentMargin.Left,
                    Math.Max(0, ContentMargin.Top - 40),
                    ContentMargin.Right,
                    ContentMargin.Bottom);
            }

            if (ContentMargin.Top < 30)
            {
                return ContentMargin;
            }

            // Most migrated cards copied the original absolute top inset,
            // so in Avalonia they end up counting both the header row and the old inset.
            return new Thickness(
                ContentMargin.Left,
                Math.Max(10, ContentMargin.Top - 28),
                ContentMargin.Right,
                ContentMargin.Bottom);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HeaderProperty)
        {
            HeaderTextBlock.Text = change.GetNewValue<string>() ?? string.Empty;
            RaisePropertyChanged(HasHeaderProperty, false, HasHeader);
            RefreshHeaderLayout();
            RefreshHeaderMetrics();
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == IsChevronExpandedProperty)
        {
            RefreshChevronState();
            RefreshContentState();
        }
        else if (change.Property == ShowChevronProperty)
        {
            RefreshHeaderLayout();
            RefreshHeaderMetrics();
            RefreshChevronState();
            RefreshContentState(animate: false);
            RefreshState();
            RaisePropertyChanged(ShowCloseButtonProperty, false, ShowCloseButton);
            RaisePropertyChanged(IsContentVisibleProperty, false, IsContentVisible);
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == ContentFillProperty)
        {
            RefreshContentState(animate: false);
        }
        else if (change.Property == ContentMarginProperty)
        {
            RaisePropertyChanged(EffectiveContentMarginProperty, default, EffectiveContentMargin);
        }
        else if (change.Property == HeaderCommandProperty)
        {
            RefreshState();
        }
        else if (change.Property == IsClosableProperty || change.Property == CloseCommandProperty)
        {
            RaisePropertyChanged(ShowCloseButtonProperty, false, ShowCloseButton);
        }
        else if (change.Property == HeaderOverlayContentProperty)
        {
            RaisePropertyChanged(HasHeaderOverlayContentProperty, false, HasHeaderOverlayContent);
        }
        else if (change.Property == IsVisibleProperty && change.GetNewValue<bool>() && !_isCloseAnimationPending)
        {
            ResetCloseVisualState();
        }
    }

    private static readonly DirectProperty<PclCard, bool> HasHeaderProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(HasHeader), x => x.HasHeader);

    private static readonly DirectProperty<PclCard, bool> HasHeaderOverlayContentProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(HasHeaderOverlayContent), x => x.HasHeaderOverlayContent);

    private static readonly DirectProperty<PclCard, bool> IsContentVisibleProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(IsContentVisible), x => x.IsContentVisible);

    private static readonly DirectProperty<PclCard, Thickness> EffectiveContentMarginProperty =
        AvaloniaProperty.RegisterDirect<PclCard, Thickness>(nameof(EffectiveContentMargin), x => x.EffectiveContentMargin);

    private static readonly DirectProperty<PclCard, bool> ShowCloseButtonProperty =
        AvaloniaProperty.RegisterDirect<PclCard, bool>(nameof(ShowCloseButton), x => x.ShowCloseButton);

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        RefreshState();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        RefreshState();
    }

    private void RefreshState()
    {
        var isHeaderInteractive = ShowChevron || HeaderCommand is not null;
        var idleBorderBrush = GetBrush("ColorBrushTransparent");
        var hoverBorderBrush = GetBrush("ColorBrushMyCardBorderMouseOver");
        var idleSurfaceBrush = GetBrush("ColorBrushMyCard");
        var hoverSurfaceBrush = GetBrush("ColorBrushMyCardMouseOver");
        var idleHeaderBrush = GetBrush("ColorBrush1");
        var hoverHeaderBrush = GetBrush("ColorBrush2");

        HoverOutlineBorder.BorderBrush = _isHovered
            ? hoverBorderBrush
            : idleBorderBrush;
        HoverOutlineBorder.BorderThickness = _isHovered
            ? HoverBorderThickness
            : IdleBorderThickness;
        CardBorder.Background = _isHovered
            ? hoverSurfaceBrush
            : idleSurfaceBrush;
        CardBorder.BoxShadow = _isHovered
            ? CreateHoverBoxShadow()
            : CreateIdleBoxShadow();
        HeaderTextBlock.Foreground = _isHovered
            ? hoverHeaderBrush
            : idleHeaderBrush;
        ChevronPath.Fill = HeaderTextBlock.Foreground;
        HeaderButton.Cursor = isHeaderInteractive ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
        HeaderButton.IsHitTestVisible = isHeaderInteractive;
    }

    private static IBrush GetBrush(string resourceKey)
    {
        return FrontendThemeResourceResolver.GetBrush(resourceKey);
    }

    private static Color GetColor(string resourceKey)
    {
        return FrontendThemeResourceResolver.GetColor(resourceKey);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static BoxShadows CreateIdleBoxShadow()
    {
        var edgeColor = WithAlpha(GetColor("ColorObject1"), 0x10);
        var shadowColor = WithAlpha(GetColor("ColorObject1"), 0x14);

        return new BoxShadows(
            new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 0,
                Blur = 0,
                Spread = 1,
                Color = edgeColor
            },
            [
                new BoxShadow
                {
                    OffsetX = 0,
                    OffsetY = 6,
                    Blur = 18,
                    Spread = 0,
                    Color = shadowColor
                }
            ]);
    }

    private static BoxShadows CreateHoverBoxShadow()
    {
        var edgeColor = WithAlpha(GetColor("ColorObject5"), 0x78);
        var shadowColor = WithAlpha(GetColor("ColorObject3"), 0x34);

        return new BoxShadows(
            new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 0,
                Blur = 2,
                Spread = 1,
                Color = edgeColor
            },
            [
                new BoxShadow
                {
                    OffsetX = 0,
                    OffsetY = 10,
                    Blur = 24,
                    Spread = 0,
                    Color = shadowColor
                }
            ]);
    }

    private void RefreshHeaderLayout()
    {
        LayoutRoot.RowDefinitions[0].Height = HasHeader
            ? (ShowChevron ? new GridLength(40) : GridLength.Auto)
            : new GridLength(0);
        CardBorder.MinHeight = HasHeader && ShowChevron ? 40 : 0;
    }

    private void RefreshHeaderMetrics()
    {
        HeaderTextBlock.Margin = ShowChevron ? CollapsibleHeaderTextMargin : StandardHeaderTextMargin;
        HeaderTextBlock.VerticalAlignment = ShowChevron ? global::Avalonia.Layout.VerticalAlignment.Center : global::Avalonia.Layout.VerticalAlignment.Top;
        ChevronPath.Margin = ShowChevron ? CollapsibleChevronMargin : StandardChevronMargin;
        ChevronPath.VerticalAlignment = ShowChevron ? global::Avalonia.Layout.VerticalAlignment.Center : global::Avalonia.Layout.VerticalAlignment.Top;
    }

    private void RefreshChevronState()
    {
        _chevronTransform.Angle = IsChevronExpanded ? 180 : 270;
    }

    private void RefreshContentState(bool animate = true)
    {
        // Fill cards contain a long list that scrolls internally. When expanded
        // they stretch to fill the host's available height (the catalog section's
        // * row) so the card fills the displayed window; when collapsed they stay
        // top-anchored at the header height. Non-fill cards are top-aligned and
        // sized (or animated) to their content.
        ApplyContentFillVerticalAlignment();

        if (!ShowChevron || ContentFill)
        {
            StopContentAnimation();
            _contentAnimationVersion++;
            SetContentRenderedVisible(ShowChevron ? IsChevronExpanded : true);
            ContentHost.Height = double.NaN;
            Height = double.NaN;
            return;
        }

        if (!animate || VisualRoot is null)
        {
            StopContentAnimation();
            _contentAnimationVersion++;
            SetContentRenderedVisible(IsChevronExpanded);
            ContentHost.Height = double.NaN;
            Height = IsChevronExpanded ? double.NaN : GetCollapsedHeight();
            return;
        }

        AnimateContentState();
    }

    private void ApplyContentFillVerticalAlignment()
    {
        if (!ContentFill)
        {
            return;
        }

        var isFilled = IsChevronExpanded;
        var alignment = isFilled
            ? global::Avalonia.Layout.VerticalAlignment.Stretch
            : global::Avalonia.Layout.VerticalAlignment.Top;
        if (VerticalAlignment != alignment)
        {
            VerticalAlignment = alignment;
        }

        // When the card fills its host, the content row must also stretch so the inner
        // list is bounded by the card's height and can scroll. Otherwise the content row
        // sizes to its (unbounded) natural height and the list overflows the card.
        var fillRow = isFilled ? GridLength.Star : GridLength.Auto;
        if (LayoutRoot.RowDefinitions[1].Height != fillRow)
        {
            LayoutRoot.RowDefinitions[1].Height = fillRow;
        }
    }

    private void AnimateContentState()
    {
        StopContentAnimation();
        var cts = new CancellationTokenSource();
        _contentAnimationCts = cts;
        var version = ++_contentAnimationVersion;
        if (IsChevronExpanded)
        {
            _ = ExpandContentAsync(version, cts.Token);
            return;
        }

        _ = CollapseContentAsync(version, cts.Token);
    }

    private void StopContentAnimation()
    {
        if (_contentAnimationCts is null)
        {
            return;
        }

        _contentAnimationCts.Cancel();
        _contentAnimationCts.Dispose();
        _contentAnimationCts = null;
    }

    private void SetContentRenderedVisible(bool isVisible)
    {
        var oldValue = IsContentVisible;
        _isContentRenderedVisible = isVisible;
        ContentHost.IsVisible = isVisible;
        ContentHost.IsHitTestVisible = isVisible;
        // Fill cards reveal their (already-present) content with a fade instead of
        // a height animation, since the card height is fixed and top-anchored.
        if (ContentFill)
        {
            ContentHost.Opacity = isVisible ? 1 : 0;
        }
        var newValue = IsContentVisible;
        if (oldValue != newValue)
        {
            RaisePropertyChanged(IsContentVisibleProperty, oldValue, newValue);
        }
    }

    private async Task ExpandContentAsync(int version, CancellationToken cancellationToken)
    {
        SetContentRenderedVisible(true);
        ContentHost.Height = double.NaN;

        var startHeight = Math.Max(GetCollapsedHeight(), Bounds.Height);
        var targetHeight = Math.Max(startHeight, MeasureExpandedHeight());
        if (targetHeight <= 0)
        {
            ContentHost.Height = double.NaN;
            Height = double.NaN;
            return;
        }

        await AnimateCardHeightAsync(startHeight, targetHeight, version, cancellationToken);
        if (!IsAnimationCurrent(version, cancellationToken) || !IsChevronExpanded)
        {
            return;
        }

        Height = double.NaN;
    }

    private async Task CollapseContentAsync(int version, CancellationToken cancellationToken)
    {
        SetContentRenderedVisible(true);
        ContentHost.Height = double.NaN;

        var startHeight = Math.Max(GetCollapsedHeight(), Bounds.Height);
        if (startHeight <= 0)
        {
            Height = GetCollapsedHeight();
            SetContentRenderedVisible(false);
            return;
        }

        await AnimateCardHeightAsync(startHeight, GetCollapsedHeight(), version, cancellationToken);
        if (!IsAnimationCurrent(version, cancellationToken) || IsChevronExpanded)
        {
            return;
        }

        SetContentRenderedVisible(false);
    }

    private async Task AnimateCardHeightAsync(double startHeight, double targetHeight, int version, CancellationToken cancellationToken)
    {
        await Dispatcher.UIThread.InvokeAsync(() => Height = startHeight, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (!IsAnimationCurrent(version, cancellationToken))
        {
            return;
        }

        var plan = CreateHeightAnimationPlan(targetHeight - startHeight);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var animation = new FrameAnimation(this, elapsed =>
        {
            if (!IsAnimationCurrent(version, cancellationToken))
            {
                completion.TrySetResult();
                return;
            }

            Height = startHeight + GetAnimatedDelta(plan, elapsed) * Math.Sign(targetHeight - startHeight);
            if (elapsed >= plan.TotalDuration)
            {
                completion.TrySetResult();
            }
        });
        using var registration = cancellationToken.Register(() => completion.TrySetResult());
        animation.IsEnabled = true;
        await completion.Task;

        if (!IsAnimationCurrent(version, cancellationToken))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => Height = targetHeight, DispatcherPriority.Render);
    }

    private static HeightAnimationPlan CreateHeightAnimationPlan(double delta)
    {
        var absDelta = Math.Abs(delta);
        var isCollapsing = delta < 0;
        if (absDelta <= ShortHeightAnimationThreshold)
        {
            return new HeightAnimationPlan(
                absDelta,
                0,
                TimeSpan.Zero,
                absDelta,
                isCollapsing ? MotionDurations.QuickState : MotionDurations.HintSettle,
                0,
                UseSimpleEase: true);
        }

        if (absDelta > LongHeightAnimationThreshold)
        {
            return CreateTravelHeightAnimationPlan(
                absDelta,
                isCollapsing ? LongCollapseHeightPixelsPerSecond : LongExpandHeightPixelsPerSecond,
                isCollapsing ? 180 : 250);
        }

        return CreateTravelHeightAnimationPlan(
            absDelta,
            isCollapsing ? CollapseHeightPixelsPerSecond : ExpandHeightPixelsPerSecond,
            isCollapsing ? 140 : 200);
    }

    private static HeightAnimationPlan CreateTravelHeightAnimationPlan(
        double distance,
        double pixelsPerSecond,
        double easeMilliseconds)
    {
        var easeSegmentDistance = Math.Min(HeightAnimationEaseDistance, distance);
        var constantSegmentDistance = Math.Max(0, distance - easeSegmentDistance);
        return new HeightAnimationPlan(
            distance,
            constantSegmentDistance,
            MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(constantSegmentDistance / pixelsPerSecond * 1000)),
            easeSegmentDistance,
            MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(easeMilliseconds)),
            pixelsPerSecond,
            UseSimpleEase: false);
    }

    private static double GetAnimatedDelta(HeightAnimationPlan plan, TimeSpan elapsed)
    {
        if (plan.TotalDistance <= 0 || plan.TotalDuration <= TimeSpan.Zero)
        {
            return plan.TotalDistance;
        }

        if (plan.UseSimpleEase)
        {
            var progress = Math.Clamp(elapsed.TotalMilliseconds / plan.TotalDuration.TotalMilliseconds, 0, 1);
            return plan.TotalDistance * (1 - Math.Pow(1 - progress, 3));
        }

        if (elapsed <= plan.ConstantDuration)
        {
            if (plan.ConstantDuration <= TimeSpan.Zero || plan.ConstantDistance <= 0)
            {
                return 0;
            }

            return plan.ConstantDistance * Math.Clamp(elapsed.TotalMilliseconds / plan.ConstantDuration.TotalMilliseconds, 0, 1);
        }

        var easedElapsed = elapsed - plan.ConstantDuration;
        var easedProgress = plan.EaseDuration <= TimeSpan.Zero
            ? 1
            : Math.Clamp(easedElapsed.TotalMilliseconds / plan.EaseDuration.TotalMilliseconds, 0, 1);
        return plan.ConstantDistance + (plan.EaseDistance * EvaluateInitialVelocityEase(
            easedProgress,
            plan.EaseInitialPixelsPerSecond,
            plan.EaseDuration,
            plan.EaseDistance));
    }

    private static double EvaluateInitialVelocityEase(
        double progress,
        double initialPixelsPerSecond,
        TimeSpan duration,
        double distance)
    {
        if (distance <= 0)
        {
            return 1;
        }

        var clampedProgress = Math.Clamp(progress, 0, 1);
        var normalizedInitialSpeed = initialPixelsPerSecond * duration.TotalSeconds / distance;
        var alpha = Math.Max(0, normalizedInitialSpeed - 1);
        if (alpha <= 0)
        {
            return clampedProgress;
        }

        return ((alpha + 1) * clampedProgress) / (1 + (alpha * clampedProgress));
    }

    private bool IsAnimationCurrent(int version, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && version == _contentAnimationVersion && VisualRoot is not null;
    }

    private double MeasureExpandedHeight()
    {
        var originalCardHeight = Height;
        var originalContentHeight = ContentHost.Height;
        try
        {
            Height = double.NaN;
            ContentHost.Height = double.NaN;
            var availableWidth = Math.Max(0, CardBorder.Bounds.Width - CardBorder.BorderThickness.Left - CardBorder.BorderThickness.Right);
            ContentHost.Measure(new Size(availableWidth > 0 ? availableWidth : double.PositiveInfinity, double.PositiveInfinity));
            return GetCollapsedHeight() + Math.Max(0, ContentHost.DesiredSize.Height);
        }
        finally
        {
            Height = originalCardHeight;
            ContentHost.Height = originalContentHeight;
        }
    }

    private static double GetCollapsedHeight()
    {
        return 40;
    }

    private void OnHeaderButtonClick(object? sender, RoutedEventArgs e)
    {
        if (HeaderCommand is not null)
        {
            if (HeaderCommand.CanExecute(null))
            {
                HeaderCommand.Execute(null);
            }

            return;
        }

        if (ShowChevron)
        {
            SetCurrentValue(IsChevronExpandedProperty, !IsChevronExpanded);
        }
    }

    private async void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        var closeCommand = CloseCommand;
        if (_isCloseAnimationPending || !ShowCloseButton || closeCommand?.CanExecute(null) != true)
        {
            return;
        }

        _isCloseAnimationPending = true;
        try
        {
            Motion.SetExitOffsetX(this, 0);
            Motion.SetExitOffsetY(this, -10);
            await Motion.PlayExitAsync(this);
            if (closeCommand.CanExecute(null))
            {
                closeCommand.Execute(null);
            }
        }
        finally
        {
            _isCloseAnimationPending = false;
            if (IsVisible)
            {
                ResetCloseVisualState();
            }
        }
    }

    private void ResetCloseVisualState()
    {
        Opacity = 1;
        if (RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }
        else
        {
            RenderTransform = new TranslateTransform();
        }
    }
}

using System.Diagnostics;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class PclLoading : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclLoading, string>(nameof(Text), "加载中");

    public static readonly StyledProperty<bool> ShowTextProperty =
        AvaloniaProperty.Register<PclLoading, bool>(nameof(ShowText), true);

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<PclLoading, bool>(nameof(IsRunning), true);

    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<PclLoading, bool>(nameof(IsError), false);

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<PclLoading, IBrush?>(nameof(IndicatorBrush));

    private static readonly IBrush ErrorBrush = Brush.Parse("#D33232");
    private static TimeSpan AnimationTickInterval => MotionDurations.FrameInterval;
    private static TimeSpan StrikeLeadIn => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(250));
    private static TimeSpan StrikeDownDuration => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(500));
    private static TimeSpan StrikeRecoveryDuration => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(900));
    private static TimeSpan StrikeImpactTime => StrikeLeadIn + StrikeDownDuration;
    private static TimeSpan CycleDuration => StrikeImpactTime + StrikeRecoveryDuration;
    private static TimeSpan DebrisFadeDelay => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(50));
    private static TimeSpan DebrisFadeDuration => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(100));
    private static TimeSpan DebrisFlightDuration => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(180));
    private static TimeSpan ErrorGlyphDuration => MotionDurations.ScaleAnimationDuration(TimeSpan.FromMilliseconds(300));
    private static readonly Easing StandardEase = new CubicEaseOut();
    private static readonly Easing BounceEase = new BackEaseOut();
    private const double RestAngle = 55d;
    private const double StrikeAngle = -20d;
    private const double FluentRecoveryDelta = 50d;
    private const double ElasticRecoveryDelta = 25d;
    private const double WeakPower = 2d;
    private const int WeakElasticPeriod = 6;
    private const double MiddlePower = 3d;
    private const double HiddenErrorScale = 0.6d;
    private const double VisibleErrorScale = 1d;

    private readonly DispatcherTimer _animationTimer = new() { Interval = AnimationTickInterval };
    private readonly Stopwatch _animationStopwatch = new();
    private bool _isMotionDurationSubscribed;
    private RotateTransform PickaxeRotateTransform => (RotateTransform)PickaxeHost.RenderTransform!;
    private TranslateTransform LeftDebrisTranslateTransform => (TranslateTransform)((TransformGroup)LeftDebris.RenderTransform!).Children[1]!;
    private TranslateTransform RightDebrisTranslateTransform => (TranslateTransform)((TransformGroup)RightDebris.RenderTransform!).Children[1]!;
    private ScaleTransform ErrorGlyphScaleTransform => (ScaleTransform)ErrorGlyph.RenderTransform!;

    public PclLoading()
    {
        InitializeComponent();
        ConfigureTransitions();
        RefreshText();
        RefreshVisualState();
        _animationTimer.Tick += OnAnimationTick;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool ShowText
    {
        get => GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public bool IsError
    {
        get => GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == ShowTextProperty)
        {
            RefreshText();
        }

        if (change.Property == IndicatorBrushProperty || change.Property == IsErrorProperty)
        {
            RefreshVisualState();
        }

        if (change.Property == IsRunningProperty || change.Property == IsErrorProperty)
        {
            SyncAnimationState();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!_isMotionDurationSubscribed)
        {
            MotionDurations.Changed += OnMotionDurationsChanged;
            _isMotionDurationSubscribed = true;
        }

        _animationTimer.Interval = AnimationTickInterval;
        ConfigureTransitions();
        SyncAnimationState();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isMotionDurationSubscribed)
        {
            MotionDurations.Changed -= OnMotionDurationsChanged;
            _isMotionDurationSubscribed = false;
        }

        StopAnimationLoop();
    }

    private void OnMotionDurationsChanged(object? sender, PropertyChangedEventArgs e)
    {
        _animationTimer.Interval = AnimationTickInterval;
        ConfigureTransitions();
    }

    private void ConfigureTransitions()
    {
        ErrorGlyph.Transitions =
        [
            CreateDoubleTransition(OpacityProperty, ErrorGlyphDuration, StandardEase)
        ];
        ErrorGlyphScaleTransform.Transitions =
        [
            CreateDoubleTransition(ScaleTransform.ScaleXProperty, ErrorGlyphDuration, BounceEase),
            CreateDoubleTransition(ScaleTransform.ScaleYProperty, ErrorGlyphDuration, BounceEase)
        ];
    }

    private void RefreshText()
    {
        LabelText.Text = Text;
        LabelText.IsVisible = ShowText && !string.IsNullOrWhiteSpace(Text);
    }

    private void RefreshVisualState()
    {
        var brush = IsError ? ErrorBrush : IndicatorBrush ?? GetBrush("ColorBrush3", "#1370F3");
        PickaxePath.Stroke = brush;
        LeftDebris.Fill = brush;
        RightDebris.Fill = brush;
        FloorLine.Fill = brush;
        LabelText.Foreground = brush;
        ErrorGlyph.Fill = ErrorBrush;
        ErrorGlyph.Opacity = IsError ? 1d : 0d;
        ErrorGlyphScaleTransform.ScaleX = IsError ? VisibleErrorScale : HiddenErrorScale;
        ErrorGlyphScaleTransform.ScaleY = IsError ? VisibleErrorScale : HiddenErrorScale;
        if (!IsError && !_animationTimer.IsEnabled)
        {
            ResetStrikeVisuals();
        }
    }

    private void ResetStrikeVisuals()
    {
        PickaxeRotateTransform.Angle = RestAngle;
        LeftDebris.Opacity = 0d;
        RightDebris.Opacity = 0d;
        LeftDebrisTranslateTransform.X = 0d;
        LeftDebrisTranslateTransform.Y = 0d;
        RightDebrisTranslateTransform.X = 0d;
        RightDebrisTranslateTransform.Y = 0d;
    }

    private void SyncAnimationState()
    {
        var shouldAnimate = VisualRoot is not null && IsRunning && !IsError;
        if (shouldAnimate)
        {
            StartAnimationLoop();
            return;
        }

        StopAnimationLoop();
    }

    private void StartAnimationLoop()
    {
        if (_animationTimer.IsEnabled)
        {
            return;
        }

        _animationStopwatch.Restart();
        _animationTimer.Start();
        UpdateAnimationFrame(TimeSpan.Zero);
    }

    private void StopAnimationLoop()
    {
        if (!_animationTimer.IsEnabled)
        {
            return;
        }

        _animationTimer.Stop();
        _animationStopwatch.Reset();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        UpdateAnimationFrame(_animationStopwatch.Elapsed);
    }

    private void UpdateAnimationFrame(TimeSpan elapsed)
    {
        var cycleElapsed = NormalizeCycleElapsed(elapsed);
        if (cycleElapsed < StrikeLeadIn)
        {
            ResetStrikeVisuals();
            return;
        }

        if (cycleElapsed < StrikeImpactTime)
        {
            var progress = ClampProgress(cycleElapsed - StrikeLeadIn, StrikeDownDuration);
            PickaxeRotateTransform.Angle = RestAngle + (StrikeAngle - RestAngle) * EaseInBack(progress, WeakPower);
            LeftDebris.Opacity = 0d;
            RightDebris.Opacity = 0d;
            LeftDebrisTranslateTransform.X = 0d;
            LeftDebrisTranslateTransform.Y = 0d;
            RightDebrisTranslateTransform.X = 0d;
            RightDebrisTranslateTransform.Y = 0d;
            return;
        }

        var postImpactElapsed = cycleElapsed - StrikeImpactTime;
        var recoveryProgress = ClampProgress(postImpactElapsed, StrikeRecoveryDuration);
        // The legacy WPF control layers a fluent rebound with an elastic settle on the same transform.
        PickaxeRotateTransform.Angle = StrikeAngle
            + FluentRecoveryDelta * EaseOutFluent(recoveryProgress, MiddlePower)
            + ElasticRecoveryDelta * EaseOutElastic(recoveryProgress, WeakElasticPeriod);

        var debrisFlightProgress = ClampProgress(postImpactElapsed, DebrisFlightDuration);
        var debrisOffsetProgress = EaseOutFluent(debrisFlightProgress, MiddlePower);
        var debrisOpacity = 1d;
        if (postImpactElapsed >= DebrisFadeDelay)
        {
            debrisOpacity = 1d - ClampProgress(postImpactElapsed - DebrisFadeDelay, DebrisFadeDuration);
        }

        LeftDebris.Opacity = debrisOpacity;
        RightDebris.Opacity = debrisOpacity;
        LeftDebrisTranslateTransform.X = -5d * debrisOffsetProgress;
        LeftDebrisTranslateTransform.Y = -6d * debrisOffsetProgress;
        RightDebrisTranslateTransform.X = 5d * debrisOffsetProgress;
        RightDebrisTranslateTransform.Y = -6d * debrisOffsetProgress;
    }

    private static DoubleTransition CreateDoubleTransition(
        AvaloniaProperty<double> property,
        TimeSpan duration,
        Easing easing)
    {
        return new DoubleTransition
        {
            Property = property,
            Duration = duration,
            Easing = easing
        };
    }

    private static TimeSpan NormalizeCycleElapsed(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(elapsed.Ticks % CycleDuration.Ticks);
    }

    private static double ClampProgress(TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1d;
        }

        return Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0d, 1d);
    }

    private static double EaseInBack(double progress, double power)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        return Math.Pow(progress, power) * Math.Cos(1.5d * Math.PI * (1d - progress));
    }

    private static double EaseOutFluent(double progress, double power)
    {
        progress = Math.Clamp(progress, 0d, 1d);
        return 1d - Math.Pow(1d - progress, power);
    }

    private static double EaseOutElastic(double progress, int period)
    {
        var remaining = 1d - Math.Clamp(progress, 0d, 1d);
        return 1d - Math.Pow(remaining, (period - 1) * 0.25d)
            * Math.Cos((period - 3.5d) * Math.PI * Math.Pow(1d - remaining, 1.5d));
    }

    private static IBrush GetBrush(string resourceKey, string fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return Brush.Parse(fallback);
    }
}

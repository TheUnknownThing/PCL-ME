using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Frontend.Spike.Desktop.Controls;

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

    public static readonly StyledProperty<IBrush> IndicatorBrushProperty =
        AvaloniaProperty.Register<PclLoading, IBrush>(nameof(IndicatorBrush), Brush.Parse("#4B5968"));

    private static readonly IBrush ErrorBrush = Brush.Parse("#D33232");
    private static readonly TimeSpan StrikeLeadIn = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StrikeDownDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan StrikeReboundDuration = TimeSpan.FromMilliseconds(520);
    private static readonly TimeSpan StrikeResetDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan DebrisFadeDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DebrisFlightDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan AnimationPadding = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan ErrorGlyphDuration = TimeSpan.FromMilliseconds(300);
    private static readonly Easing StandardEase = new CubicEaseOut();
    private static readonly Easing BounceEase = new BackEaseOut();
    private const double RestAngle = 55d;
    private const double StrikeAngle = -20d;
    private const double ReboundAngle = 30d;
    private const double HiddenErrorScale = 0.6d;
    private const double VisibleErrorScale = 1d;

    private CancellationTokenSource? _animationCts;
    private Task? _animationLoop;
    private RotateTransform PickaxeRotateTransform => (RotateTransform)PickaxePath.RenderTransform!;
    private TranslateTransform LeftDebrisTranslateTransform => (TranslateTransform)((TransformGroup)LeftDebris.RenderTransform!).Children[1]!;
    private TranslateTransform RightDebrisTranslateTransform => (TranslateTransform)((TransformGroup)RightDebris.RenderTransform!).Children[1]!;
    private ScaleTransform ErrorGlyphScaleTransform => (ScaleTransform)ErrorGlyph.RenderTransform!;

    public PclLoading()
    {
        InitializeComponent();
        ConfigureTransitions();
        RefreshText();
        RefreshVisualState();
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

    public IBrush IndicatorBrush
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
        SyncAnimationState();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopAnimationLoop();
    }

    private void ConfigureTransitions()
    {
        PickaxeRotateTransform.Transitions =
        [
            CreateDoubleTransition(RotateTransform.AngleProperty, StrikeDownDuration, StandardEase)
        ];

        LeftDebrisTranslateTransform.Transitions =
        [
            CreateDoubleTransition(TranslateTransform.XProperty, DebrisFlightDuration, StandardEase),
            CreateDoubleTransition(TranslateTransform.YProperty, DebrisFlightDuration, StandardEase)
        ];
        RightDebrisTranslateTransform.Transitions =
        [
            CreateDoubleTransition(TranslateTransform.XProperty, DebrisFlightDuration, StandardEase),
            CreateDoubleTransition(TranslateTransform.YProperty, DebrisFlightDuration, StandardEase)
        ];

        LeftDebris.Transitions =
        [
            CreateDoubleTransition(OpacityProperty, DebrisFadeDuration, StandardEase)
        ];
        RightDebris.Transitions =
        [
            CreateDoubleTransition(OpacityProperty, DebrisFadeDuration, StandardEase)
        ];

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
        var brush = IsError ? ErrorBrush : IndicatorBrush;
        PickaxePath.Stroke = brush;
        LeftDebris.Fill = brush;
        RightDebris.Fill = brush;
        FloorLine.Fill = brush;
        LabelText.Foreground = brush;
        ErrorGlyph.Fill = ErrorBrush;
        ErrorGlyph.Opacity = IsError ? 1d : 0d;
        ErrorGlyphScaleTransform.ScaleX = IsError ? VisibleErrorScale : HiddenErrorScale;
        ErrorGlyphScaleTransform.ScaleY = IsError ? VisibleErrorScale : HiddenErrorScale;
        if (!IsError)
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
        if (!IsError)
        {
            ResetStrikeVisuals();
        }
    }

    private void StartAnimationLoop()
    {
        if (_animationLoop is { IsCompleted: false })
        {
            return;
        }

        _animationCts = new CancellationTokenSource();
        _animationLoop = RunAnimationLoopAsync(_animationCts.Token);
    }

    private void StopAnimationLoop()
    {
        if (_animationCts is null)
        {
            return;
        }

        _animationCts.Cancel();
        _animationCts.Dispose();
        _animationCts = null;
        _animationLoop = null;
    }

    private async Task RunAnimationLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(ResetStrikeVisuals);
                await Task.Delay(StrikeLeadIn, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PickaxeRotateTransform.Transitions =
                    [
                        CreateDoubleTransition(RotateTransform.AngleProperty, StrikeDownDuration, StandardEase)
                    ];
                    PickaxeRotateTransform.Angle = StrikeAngle;
                });
                await Task.Delay(StrikeDownDuration + AnimationPadding, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LeftDebris.Opacity = 1d;
                    RightDebris.Opacity = 1d;
                    LeftDebrisTranslateTransform.X = 0d;
                    LeftDebrisTranslateTransform.Y = 0d;
                    RightDebrisTranslateTransform.X = 0d;
                    RightDebrisTranslateTransform.Y = 0d;
                    PickaxeRotateTransform.Transitions =
                    [
                        CreateDoubleTransition(RotateTransform.AngleProperty, StrikeReboundDuration, BounceEase)
                    ];
                    PickaxeRotateTransform.Angle = ReboundAngle;
                });
                await Task.Delay(TimeSpan.FromMilliseconds(70), cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LeftDebris.Opacity = 0d;
                    RightDebris.Opacity = 0d;
                    LeftDebrisTranslateTransform.X = -5d;
                    LeftDebrisTranslateTransform.Y = -6d;
                    RightDebrisTranslateTransform.X = 5d;
                    RightDebrisTranslateTransform.Y = -6d;
                });
                await Task.Delay(StrikeReboundDuration + AnimationPadding, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PickaxeRotateTransform.Transitions =
                    [
                        CreateDoubleTransition(RotateTransform.AngleProperty, StrikeResetDuration, BounceEase)
                    ];
                    PickaxeRotateTransform.Angle = RestAngle;
                });
                await Task.Delay(StrikeResetDuration + AnimationPadding, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Control was detached or animation was stopped.
        }
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
}

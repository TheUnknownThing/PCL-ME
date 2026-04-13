using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Animation;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.ViewModels;
using System.Numerics;
using System.Threading.Tasks;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed partial class MainWindow : Window
{
    private FrontendShellViewModel? _shellViewModel;
    private CompositionVisual? _gridRootVisual;
    private CompositionVisual? _launchLeftContentHostVisual;
    private CompositionVisual? _launchRightContentHostVisual;
    private CompositionVisual? _standardLeftContentHostVisual;
    private CompositionVisual? _standardRightContentHostVisual;
    private Compositor? _compositor;
    private int _promptOverlayAnimationVersion;
    private bool _isPromptOverlayRenderedOpen;
    private readonly Dictionary<string, HintPopupState> _activeHints = new(StringComparer.Ordinal);

    public MainWindow()
    {
        InitializeComponent();
        ConfigureShellDividerTransitions();
        PclModalMotion.ResetToClosedState(PromptOverlayBackdrop, PromptOverlayPanel);
        PromptOverlayBackdrop.IsVisible = false;
        PromptOverlayHost.IsVisible = false;
        PromptOverlayHost.IsHitTestVisible = false;

        BackButton.IconData = FrontendIconCatalog.Back.Data;
        MinimizeButton.IconData = FrontendIconCatalog.Minimize.Data;
        MaximizeButton.IconData = FrontendIconCatalog.Maximize.Data;
        CloseButton.IconData = FrontendIconCatalog.Close.Data;
        PromptQueueButton.IconData = FrontendIconCatalog.PromptQueue.Data;

        if (!OperatingSystem.IsMacOS())
        {
            SetupSide(Left, StandardCursorType.LeftSide, WindowEdge.West);
            SetupSide(Right, StandardCursorType.RightSide, WindowEdge.East);
            SetupSide(Top, StandardCursorType.TopSide, WindowEdge.North);
            SetupSide(Bottom, StandardCursorType.BottomSide, WindowEdge.South);
            SetupSide(TopLeft, StandardCursorType.TopLeftCorner, WindowEdge.NorthWest);
            SetupSide(TopRight, StandardCursorType.TopRightCorner, WindowEdge.NorthEast);
            SetupSide(BottomLeft, StandardCursorType.BottomLeftCorner, WindowEdge.SouthWest);
            SetupSide(BottomRight, StandardCursorType.BottomRightCorner, WindowEdge.SouthEast);
        }

        GridRoot.Opacity = 0;
        AvaloniaHintBus.OnShow += OnHintWrapperShow;
        MotionDurations.Changed += OnMotionDurationsChanged;

        Opened += (_, _) =>
        {
            InitializeCompositionVisuals();
            RunEntranceAnimation();
        };
        Closed += OnWindowClosed;
        Deactivated += OnWindowDeactivated;
        KeyDown += OnWindowKeyDown;
        KeyUp += OnWindowKeyUp;
        PropertyChanged += OnWindowPropertyChanged;
        DataContextChanged += OnDataContextChanged;
        UpdateWindowChromeState();
    }

    private void ConfigureShellDividerTransitions()
    {
        ShellLeftBackdrop.Transitions = CreateShellDividerTransitions();
        StandardLeftHost.Transitions = CreateShellDividerTransitions();
    }

    private static Transitions CreateShellDividerTransitions()
    {
        return new Transitions
        {
            new DoubleTransition
            {
                Property = Layoutable.WidthProperty,
                Duration = MotionDurations.DividerResize,
                Easing = new CubicEaseOut()
            }
        };
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            BeginMoveDrag(e);
        }
        catch (InvalidOperationException)
        {
            // Ignore drag attempts that happen before the window is ready.
        }
    }

    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void MinimizeGlyph_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseGlyph_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private void TitleBar_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void SetupSide(Control control, StandardCursorType cursor, WindowEdge edge)
    {
        control.Cursor = new Cursor(cursor);
        control.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(edge, e);
            }
        };
    }

    private void InitializeCompositionVisuals()
    {
        _gridRootVisual ??= ElementComposition.GetElementVisual(GridRoot);
        _launchLeftContentHostVisual ??= ElementComposition.GetElementVisual(LaunchLeftContentHost);
        _launchRightContentHostVisual ??= ElementComposition.GetElementVisual(LaunchRightContentHost);
        _standardLeftContentHostVisual ??= ElementComposition.GetElementVisual(StandardLeftContentHost);
        _standardRightContentHostVisual ??= ElementComposition.GetElementVisual(StandardRightContentHost);
        _compositor ??= _gridRootVisual?.Compositor
            ?? _launchLeftContentHostVisual?.Compositor
            ?? _standardLeftContentHostVisual?.Compositor;
    }

    private async void RunEntranceAnimation()
    {
        await Task.Delay(MotionDurations.WindowEnterDelay);

        InitializeCompositionVisuals();
        if (_gridRootVisual is null || _compositor is null)
        {
            GridRoot.Opacity = 1;
            return;
        }

        var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = MotionDurations.StartupFade;
        opacityAnimation.InsertKeyFrame(0f, 0f, new CubicEaseOut());
        opacityAnimation.InsertKeyFrame(1f, 1f, new CubicEaseOut());
        opacityAnimation.Target = "Opacity";

        var rotationAnimation = _compositor.CreateScalarKeyFrameAnimation();
        rotationAnimation.Duration = MotionDurations.StartupRotate;
        rotationAnimation.InsertKeyFrame(0f, -0.06f, new CubicEaseOut());
        rotationAnimation.InsertKeyFrame(1f, 0f, new CubicEaseOut());
        rotationAnimation.Target = "RotationAngle";

        var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = MotionDurations.StartupLift;
        offsetAnimation.InsertKeyFrame(0f, new Vector3(0f, 60f, 0f), new CubicEaseOut());
        offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), new CubicEaseOut());
        offsetAnimation.Target = "Offset";

        var group = _compositor.CreateAnimationGroup();
        group.Add(opacityAnimation);
        group.Add(rotationAnimation);
        group.Add(offsetAnimation);

        var size = _gridRootVisual.Size;
        _gridRootVisual.CenterPoint = new Vector3D(
            (float)size.X / 2,
            (float)size.Y / 2,
            (float)_gridRootVisual.CenterPoint.Z);
        _gridRootVisual.StartAnimationGroup(group);
    }

    private void OnWindowPropertyChanged(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            UpdateWindowChromeState();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        AvaloniaHintBus.OnShow -= OnHintWrapperShow;
        MotionDurations.Changed -= OnMotionDurationsChanged;
        Closed -= OnWindowClosed;
        Deactivated -= OnWindowDeactivated;
        KeyDown -= OnWindowKeyDown;
        KeyUp -= OnWindowKeyUp;

        foreach (var state in _activeHints.Values)
        {
            state.LifetimeCancellation.Cancel();
            state.LifetimeCancellation.Dispose();
        }

        _activeHints.Clear();
    }

    private void OnMotionDurationsChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConfigureShellDividerTransitions();
            foreach (var state in _activeHints.Values)
            {
                state.Border.Transitions = CreateHintTransitions();
            }
        });
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        _shellViewModel?.UpdateKeyboardModifiers(e.KeyModifiers);

        if (e.Handled || e.Key != Key.Escape || _shellViewModel is null)
        {
            return;
        }

        if (_shellViewModel.BackCommand.CanExecute(null))
        {
            _shellViewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        _shellViewModel?.UpdateKeyboardModifiers(e.KeyModifiers);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _shellViewModel?.UpdateKeyboardModifiers(KeyModifiers.None);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shellViewModel is not null)
        {
            _shellViewModel.NavigationTransitionRequested -= OnNavigationTransitionRequested;
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        }

        _shellViewModel = DataContext as FrontendShellViewModel;

        if (_shellViewModel is not null)
        {
            _shellViewModel.UpdateKeyboardModifiers(KeyModifiers.None);
            _shellViewModel.NavigationTransitionRequested += OnNavigationTransitionRequested;
            _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
        }

        QueuePromptOverlaySync();
    }

    private void OnNavigationTransitionRequested(object? sender, ShellNavigationTransitionEventArgs e)
    {
        Dispatcher.UIThread.Post(
            () => PlayRouteTransition(e),
            DispatcherPriority.Render);
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontendShellViewModel.IsPromptOverlayVisible))
        {
            QueuePromptOverlaySync();
        }
    }

    private void QueuePromptOverlaySync()
    {
        Dispatcher.UIThread.Post(() => _ = SyncPromptOverlayAsync(), DispatcherPriority.Render);
    }

    private async Task SyncPromptOverlayAsync()
    {
        var shouldShow = _shellViewModel?.IsPromptOverlayVisible == true;
        if (shouldShow == _isPromptOverlayRenderedOpen)
        {
            return;
        }

        var version = ++_promptOverlayAnimationVersion;
        _isPromptOverlayRenderedOpen = shouldShow;

        if (shouldShow)
        {
            PromptOverlayBackdrop.IsVisible = true;
            PromptOverlayHost.IsVisible = true;
            PromptOverlayHost.IsHitTestVisible = true;
            PclModalMotion.ResetToClosedState(PromptOverlayBackdrop, PromptOverlayPanel);
            await PclModalMotion.PlayOpenAsync(
                PromptOverlayBackdrop,
                PromptOverlayPanel,
                () => version == _promptOverlayAnimationVersion && _isPromptOverlayRenderedOpen);
            return;
        }

        PromptOverlayHost.IsHitTestVisible = false;
        await PclModalMotion.PlayCloseAsync(
            PromptOverlayBackdrop,
            PromptOverlayPanel,
            () => version == _promptOverlayAnimationVersion && !_isPromptOverlayRenderedOpen);
        if (version != _promptOverlayAnimationVersion || _isPromptOverlayRenderedOpen)
        {
            return;
        }

        PromptOverlayBackdrop.IsVisible = false;
        PromptOverlayHost.IsVisible = false;
        PclModalMotion.ResetToClosedState(PromptOverlayBackdrop, PromptOverlayPanel);
    }

    private void UpdateWindowChromeState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        GridResize.IsVisible = !isMaximized;
        MainBorder.Margin = isMaximized ? new Thickness(0) : new Thickness(18);
        MainBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(15, 15, 8, 8);
        MainClipBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(6);
    }

    private void OnHintWrapperShow(string message, AvaloniaHintTheme theme)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () => _ = ShowHintAsync(NormalizeHintMessage(message), theme),
            DispatcherPriority.Background);
    }

    private async Task ShowHintAsync(string message, AvaloniaHintTheme theme)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_activeHints.TryGetValue(message, out var existingState))
        {
            existingState.ApplyTheme(theme);
            ResetHintLifetime(existingState);
            await NudgeHintAsync(existingState);
            return;
        }

        if (PanHint.Children.Count >= 20)
        {
            return;
        }

        var border = CreateHintBorder(message, theme);
        var state = new HintPopupState(message, border);
        _activeHints[message] = state;
        PanHint.Children.Add(border);

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        border.Opacity = 1;
        border.RenderTransform = new TranslateTransform(0, 0);
        ResetHintLifetime(state);
    }

    private void ResetHintLifetime(HintPopupState state)
    {
        state.LifetimeCancellation.Cancel();
        state.LifetimeCancellation.Dispose();
        state.LifetimeCancellation = new CancellationTokenSource();
        _ = HideHintLaterAsync(state, state.LifetimeCancellation.Token);
    }

    private async Task HideHintLaterAsync(HintPopupState state, CancellationToken cancellationToken)
    {
        try
        {
            var visibleDuration = TimeSpan.FromMilliseconds(
                MotionDurations.HintVisibleBaseMilliseconds +
                Math.Clamp(
                    state.Message.Length,
                    MotionDurations.HintVisibleMinCharacters,
                    MotionDurations.HintVisibleMaxCharacters) * MotionDurations.HintVisiblePerCharacterMilliseconds);
            await Task.Delay(visibleDuration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || state.IsClosing)
        {
            return;
        }

        state.IsClosing = true;
        state.Border.Opacity = 0;
        state.Border.RenderTransform = new TranslateTransform(48, 0);

        try
        {
            await Task.Delay(MotionDurations.HintSettle, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            state.IsClosing = false;
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            state.IsClosing = false;
            return;
        }

        PanHint.Children.Remove(state.Border);
        _activeHints.Remove(state.Message);
        state.LifetimeCancellation.Dispose();
    }

    private async Task NudgeHintAsync(HintPopupState state)
    {
        if (state.IsClosing)
        {
            return;
        }

        state.Border.RenderTransform = new TranslateTransform(12, 0);
        await Task.Delay(MotionDurations.HintNudge);
        if (!state.IsClosing)
        {
            state.Border.RenderTransform = new TranslateTransform(0, 0);
        }
    }

    private static string NormalizeHintMessage(string message)
    {
        return message
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static Border CreateHintBorder(string message, AvaloniaHintTheme theme)
    {
        var border = new Border
        {
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Right,
            CornerRadius = new CornerRadius(6, 0, 0, 6),
            Padding = new Thickness(14, 6, 14, 6),
            Opacity = 0,
            RenderTransform = new TranslateTransform(48, 0),
            BoxShadow = BoxShadows.Parse("0 6 18 0 #28000000")
        };
        border.Transitions = CreateHintTransitions();
        border.Child = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 332
        };

        ApplyHintTheme(border, theme);
        return border;
    }

    private static Transitions CreateHintTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = MotionDurations.QuickState,
                Easing = new CubicEaseOut()
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = MotionDurations.EntranceTranslate,
                Easing = new CubicEaseOut()
            }
        ];
    }

    private static void ApplyHintTheme(Border border, AvaloniaHintTheme theme)
    {
        var (start, end) = theme switch
        {
            AvaloniaHintTheme.Success => (Color.Parse("#D721B121"), Color.Parse("#D71DA01D")),
            AvaloniaHintTheme.Error => (Color.Parse("#D7FF350B"), Color.Parse("#D7FF2B00")),
            _ => (Color.Parse("#D7259BFC"), Color.Parse("#D70A8EFC"))
        };
        border.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(start, 0),
                new GradientStop(end, 1)
            ]
        };
    }

    private void PlayRouteTransition(ShellNavigationTransitionEventArgs transition)
    {
        InitializeCompositionVisuals();
        if (_compositor is null)
        {
            return;
        }

        var (leftVisual, rightVisual) = transition.IsLaunchRoute
            ? (_launchLeftContentHostVisual, _launchRightContentHostVisual)
            : (_standardLeftContentHostVisual, _standardRightContentHostVisual);

        if (leftVisual is null || rightVisual is null)
        {
            return;
        }

        var leftOffset = transition.Direction == ShellNavigationTransitionDirection.Forward ? -50f : 50f;

        if (transition.AnimateLeftPane)
        {
            StartRouteAnimation(leftVisual, leftOffset);
        }

        if (transition.IsLaunchRoute)
        {
            // The launch right pane is a text-heavy card. Animate the control itself to avoid
            // composition flicker while preserving the vertical drop-in motion.
            LaunchRightPanel.QueueRouteEnterAnimation();
        }
    }

    private void StartRouteAnimation(CompositionVisual visual, float offsetX, float offsetY = 0f, bool useBounce = false)
    {
        if (_compositor is null)
        {
            return;
        }

        Easing easing = useBounce ? new BackEaseOut() : new CubicEaseOut();

        var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = MotionDurations.RouteTransition;
        offsetAnimation.InsertKeyFrame(0f, new Vector3(offsetX, offsetY, 0f), easing);
        offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), easing);
        offsetAnimation.Target = "Offset";

        var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = MotionDurations.RouteTransition;
        opacityAnimation.InsertKeyFrame(0f, 0f, easing);
        opacityAnimation.InsertKeyFrame(1f, 1f, easing);
        opacityAnimation.Target = "Opacity";

        var group = _compositor.CreateAnimationGroup();
        group.Add(offsetAnimation);
        group.Add(opacityAnimation);

        visual.StartAnimationGroup(group);
    }

    private sealed class HintPopupState(string message, Border border)
    {
        public string Message { get; } = message;

        public Border Border { get; } = border;

        public CancellationTokenSource LifetimeCancellation { get; set; } = new();

        public bool IsClosing { get; set; }

        public void ApplyTheme(AvaloniaHintTheme currentTheme)
        {
            ApplyHintTheme(Border, currentTheme);
        }
    }
}

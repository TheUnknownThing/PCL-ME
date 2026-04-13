using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Animation;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.ViewModels;
using System.Numerics;
using System.Threading.Tasks;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed partial class MainWindow : Window
{
    private readonly IBrush _defaultMainBackgroundBrush;
    private readonly IBrush _defaultTitleBarBackgroundBrush;
    private Color? _darkMainBackgroundBaseColor;
    private IBrush? _darkMainBackgroundBrush;
    private const int PromptOverlayMessageRowIndex = 3;
    private const int PromptOverlayChoiceRowIndex = 5;
    private static readonly GridLength PromptOverlayFlexibleRowHeight = new(1, GridUnitType.Star);
    private static readonly GridLength PromptOverlayHiddenRowHeight = new(0);
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
        _defaultMainBackgroundBrush = MainBorder.Background ?? Brushes.Transparent;
        _defaultTitleBarBackgroundBrush = NavBackgroundBorder.Background ?? Brushes.Transparent;
        ConfigureShellDividerTransitions();
        UpdatePromptOverlayRowHeights();
        PclModalMotion.ResetToClosedState(PromptOverlayBackdrop, PromptOverlayPanel);
        PromptOverlayBackdrop.IsVisible = false;
        PromptOverlayHost.IsVisible = false;
        PromptOverlayHost.IsHitTestVisible = false;

        BackButton.IconData = FrontendIconCatalog.Back.Data;
        MinimizeButton.IconData = FrontendIconCatalog.Minimize.Data;
        MaximizeButton.IconData = FrontendIconCatalog.Maximize.Data;
        CloseButton.IconData = FrontendIconCatalog.Close.Data;

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
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ResourcesChanged += OnWindowResourcesChanged;

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
        ApplyMainBackgroundBrush();
        ApplyTitleBarBackgroundBrush();
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
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        ResourcesChanged -= OnWindowResourcesChanged;
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

        if (_shellViewModel?.TryHandlePromptOverlayKey(e.Key) == true)
        {
            e.Handled = true;
            return;
        }

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

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyMainBackgroundBrush();
        ApplyTitleBarBackgroundBrush();
    }

    private void OnWindowResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        ApplyMainBackgroundBrush();
        ApplyTitleBarBackgroundBrush();
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

        UpdatePromptOverlayRowHeights();
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
            UpdatePromptOverlayRowHeights();
            QueuePromptOverlaySync();
            return;
        }

        if (e.PropertyName == nameof(FrontendShellViewModel.IsPromptOverlayWarning))
        {
            UpdatePromptOverlayBackdropBrush();
            return;
        }

        if (e.PropertyName == nameof(FrontendShellViewModel.ShowPromptOverlayTextInput)
            || e.PropertyName == nameof(FrontendShellViewModel.ShowPromptOverlayChoiceList)
            || e.PropertyName == nameof(FrontendShellViewModel.ShowPromptOverlayMessage))
        {
            UpdatePromptOverlayRowHeights();
            QueuePromptOverlayFocus();
        }
    }

    private void QueuePromptOverlaySync()
    {
        Dispatcher.UIThread.Post(() => _ = SyncPromptOverlayAsync(), DispatcherPriority.Render);
    }

    private void QueuePromptOverlayFocus()
    {
        Dispatcher.UIThread.Post(() => _ = FocusPromptOverlayPrimaryControlAsync(), DispatcherPriority.Input);
    }

    private async Task SyncPromptOverlayAsync()
    {
        UpdatePromptOverlayRowHeights();

        var shouldShow = _shellViewModel?.IsPromptOverlayVisible == true;
        if (shouldShow == _isPromptOverlayRenderedOpen)
        {
            if (shouldShow)
            {
                UpdatePromptOverlayBackdropBrush();
            }

            return;
        }

        var version = ++_promptOverlayAnimationVersion;
        _isPromptOverlayRenderedOpen = shouldShow;

        if (shouldShow)
        {
            UpdatePromptOverlayBackdropBrush();
            PromptOverlayBackdrop.IsVisible = true;
            PromptOverlayHost.IsVisible = true;
            PromptOverlayHost.IsHitTestVisible = true;
            PclModalMotion.ResetToClosedState(PromptOverlayBackdrop, PromptOverlayPanel);
            await PclModalMotion.PlayOpenAsync(
                PromptOverlayBackdrop,
                PromptOverlayPanel,
                () => version == _promptOverlayAnimationVersion && _isPromptOverlayRenderedOpen);
            QueuePromptOverlayFocus();
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

    private async Task FocusPromptOverlayPrimaryControlAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);

        if (_shellViewModel?.ShowPromptOverlayTextInput == true && PromptOverlayInputTextBox.IsVisible)
        {
            PromptOverlayInputTextBox.Focus();
            PromptOverlayInputTextBox.SelectionStart = 0;
            PromptOverlayInputTextBox.SelectionEnd = PromptOverlayInputTextBox.Text?.Length ?? 0;
            return;
        }

        if (_shellViewModel?.ShowPromptOverlayChoiceList == true && PromptOverlayChoiceListBox.IsVisible)
        {
            PromptOverlayChoiceListBox.Focus();
        }
    }

    private void UpdatePromptOverlayBackdropBrush()
    {
        var isWarning = _shellViewModel?.IsPromptOverlayWarning == true;
        PromptOverlayBackdrop.Background = isWarning
            ? FrontendThemeResourceResolver.GetBrush("ColorBrushPromptOverlayWarningBackdrop")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushPromptOverlayBackdrop");
    }

    private void ApplyMainBackgroundBrush()
    {
        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            var darkBaseColor = FrontendThemeResourceResolver.GetColor("ColorObjectBg0");
            if (_darkMainBackgroundBrush is null || _darkMainBackgroundBaseColor != darkBaseColor)
            {
                _darkMainBackgroundBaseColor = darkBaseColor;
                _darkMainBackgroundBrush = CreateDarkShellBackgroundBrush(darkBaseColor);
            }

            MainBorder.Background = _darkMainBackgroundBrush;
            return;
        }

        MainBorder.Background = _defaultMainBackgroundBrush;
    }

    private void ApplyTitleBarBackgroundBrush()
    {
        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            NavBackgroundBorder.Background = FrontendThemeResourceResolver.GetBrush("ColorBrushTitleBarBackground");
            return;
        }

        NavBackgroundBorder.Background = _defaultTitleBarBackgroundBrush;
    }

    private void UpdatePromptOverlayRowHeights()
    {
        if (PromptOverlayContentGrid.RowDefinitions.Count <= PromptOverlayChoiceRowIndex)
        {
            return;
        }

        var showMessage = _shellViewModel?.ShowPromptOverlayMessage == true;
        var showTextInput = _shellViewModel?.ShowPromptOverlayTextInput == true;
        var showChoiceList = _shellViewModel?.ShowPromptOverlayChoiceList == true;

        PromptOverlayContentGrid.RowDefinitions[PromptOverlayMessageRowIndex].Height = showMessage
            ? showChoiceList || showTextInput ? GridLength.Auto : PromptOverlayFlexibleRowHeight
            : PromptOverlayHiddenRowHeight;
        PromptOverlayContentGrid.RowDefinitions[PromptOverlayChoiceRowIndex].Height = showChoiceList
            ? PromptOverlayFlexibleRowHeight
            : PromptOverlayHiddenRowHeight;
    }

    private void PromptOverlayChoiceListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        _shellViewModel?.ConfirmPromptOverlayChoice();
    }

    private void UpdateWindowChromeState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        GridResize.IsVisible = !isMaximized;
        MainBorder.Margin = isMaximized ? new Thickness(0) : new Thickness(18);
        MainBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(15, 15, 8, 8);
        MainClipBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(6);
    }

    private static IBrush CreateDarkShellBackgroundBrush(Color baseColor)
    {
        const int textureSize = 512;
        var pixels = new byte[textureSize * textureSize * 4];
        var baseBlue = baseColor.B / 255d;
        var baseGreen = baseColor.G / 255d;
        var baseRed = baseColor.R / 255d;

        for (var y = 0; y < textureSize; y++)
        {
            var normalizedY = textureSize == 1 ? 0d : y / (double)(textureSize - 1);
            var linearRgb = 1d - normalizedY;
            var linearFactor = (0.85d + (0.15d * linearRgb)) * 0.25;

            for (var x = 0; x < textureSize; x++)
            {
                var normalizedX = textureSize == 1 ? 0.5d : x / (double)(textureSize - 1);
                var ellipseDistance = Math.Sqrt(
                    Math.Pow((normalizedX - 0.5d) / 0.5d, 2) +
                    Math.Pow(normalizedY, 2));
                var radialRgb = Math.Clamp(1d - (ellipseDistance / 1.2d), 0d, 1d);
                var radialFactor = 0.6d + (0.4d * radialRgb);
                var blendFactor = radialFactor * linearFactor;
                var blue = (byte)Math.Clamp(Math.Round(255d * baseBlue * blendFactor), byte.MinValue, byte.MaxValue);
                var green = (byte)Math.Clamp(Math.Round(255d * baseGreen * blendFactor), byte.MinValue, byte.MaxValue);
                var red = (byte)Math.Clamp(Math.Round(255d * baseRed * blendFactor), byte.MinValue, byte.MaxValue);

                var pixelIndex = ((y * textureSize) + x) * 4;
                pixels[pixelIndex] = blue;
                pixels[pixelIndex + 1] = green;
                pixels[pixelIndex + 2] = red;
                pixels[pixelIndex + 3] = byte.MaxValue;
            }
        }

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var bitmap = new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(textureSize, textureSize),
                new global::Avalonia.Vector(96, 96),
                textureSize * 4);

            return new ImageBrush(bitmap)
            {
                Stretch = Stretch.Fill
            };
        }
        finally
        {
            handle.Free();
        }
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
            BoxShadow = FrontendThemeResourceResolver.GetBoxShadows("ColorShadowHintPopup")
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
        var (startKey, endKey) = theme switch
        {
            AvaloniaHintTheme.Success => ("ColorObjectHintSuccessStart", "ColorObjectHintSuccessEnd"),
            AvaloniaHintTheme.Error => ("ColorObjectHintErrorStart", "ColorObjectHintErrorEnd"),
            _ => ("ColorObjectHintInfoStart", "ColorObjectHintInfoEnd")
        };
        var start = FrontendThemeResourceResolver.GetColor(startKey);
        var end = FrontendThemeResourceResolver.GetColor(endKey);
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

        PlayRightPaneRouteTransition(transition);
    }

    private void PlayRightPaneRouteTransition(ShellNavigationTransitionEventArgs transition)
    {
        if (!transition.AnimateRightPane)
        {
            return;
        }

        // Standard right panes animate through PclAnimatedRightPaneHost, which preserves the
        // top-to-bottom card reveal. Only the launch pane needs a separate route animation here.
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

using System;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using PCL.Frontend.Spike.Icons;
using PCL.Frontend.Spike.ViewModels;
using System.Numerics;
using System.Threading.Tasks;

namespace PCL.Frontend.Spike.Desktop;

internal sealed partial class MainWindow : Window
{
    private static readonly TimeSpan RouteTransitionDuration = TimeSpan.FromMilliseconds(300);
    private FrontendShellViewModel? _shellViewModel;
    private CompositionVisual? _gridRootVisual;
    private CompositionVisual? _launchLeftContentHostVisual;
    private CompositionVisual? _launchRightContentHostVisual;
    private CompositionVisual? _standardLeftContentHostVisual;
    private CompositionVisual? _standardRightContentHostVisual;
    private Compositor? _compositor;

    public MainWindow()
    {
        InitializeComponent();

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

        Opened += (_, _) =>
        {
            InitializeCompositionVisuals();
            RunEntranceAnimation();
        };
        PropertyChanged += OnWindowPropertyChanged;
        DataContextChanged += OnDataContextChanged;
        UpdateWindowChromeState();
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
        await Task.Delay(100);

        InitializeCompositionVisuals();
        if (_gridRootVisual is null || _compositor is null)
        {
            GridRoot.Opacity = 1;
            return;
        }

        var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);
        opacityAnimation.InsertKeyFrame(0f, 0f, new CubicEaseOut());
        opacityAnimation.InsertKeyFrame(1f, 1f, new CubicEaseOut());
        opacityAnimation.Target = "Opacity";

        var rotationAnimation = _compositor.CreateScalarKeyFrameAnimation();
        rotationAnimation.Duration = TimeSpan.FromMilliseconds(500);
        rotationAnimation.InsertKeyFrame(0f, -0.06f, new CubicEaseOut());
        rotationAnimation.InsertKeyFrame(1f, 0f, new CubicEaseOut());
        rotationAnimation.Target = "RotationAngle";

        var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(600);
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

    private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            UpdateWindowChromeState();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shellViewModel is not null)
        {
            _shellViewModel.NavigationTransitionRequested -= OnNavigationTransitionRequested;
        }

        _shellViewModel = DataContext as FrontendShellViewModel;

        if (_shellViewModel is not null)
        {
            _shellViewModel.NavigationTransitionRequested += OnNavigationTransitionRequested;
        }
    }

    private void OnNavigationTransitionRequested(object? sender, ShellNavigationTransitionEventArgs e)
    {
        Dispatcher.UIThread.Post(
            () => PlayRouteTransition(e),
            DispatcherPriority.Render);
    }

    private void UpdateWindowChromeState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        GridResize.IsVisible = !isMaximized;
        MainBorder.Margin = isMaximized ? new Thickness(0) : new Thickness(18);
        MainBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(15, 15, 8, 8);
        MainClipBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(6);
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
        var rightOffset = transition.Direction == ShellNavigationTransitionDirection.Forward ? 50f : -50f;

        if (transition.AnimateLeftPane)
        {
            StartRouteAnimation(leftVisual, leftOffset);
        }

        if (transition.AnimateRightPane)
        {
            StartRouteAnimation(rightVisual, rightOffset);
        }
    }

    private void StartRouteAnimation(CompositionVisual visual, float offsetX)
    {
        if (_compositor is null)
        {
            return;
        }

        var easing = new CubicEaseOut();

        var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = RouteTransitionDuration;
        offsetAnimation.InsertKeyFrame(0f, new Vector3(offsetX, 0f, 0f), easing);
        offsetAnimation.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), easing);
        offsetAnimation.Target = "Offset";

        var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = RouteTransitionDuration;
        opacityAnimation.InsertKeyFrame(0f, 0f, easing);
        opacityAnimation.InsertKeyFrame(1f, 1f, easing);
        opacityAnimation.Target = "Opacity";

        var group = _compositor.CreateAnimationGroup();
        group.Add(offsetAnimation);
        group.Add(opacityAnimation);

        visual.StartAnimationGroup(group);
    }
}

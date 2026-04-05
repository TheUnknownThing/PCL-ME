using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PCL.Frontend.Spike.Icons;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop;

internal sealed partial class MainWindow : Window
{
    private static readonly TimeSpan RouteTransitionDuration = TimeSpan.FromMilliseconds(300);
    private static readonly CubicEaseOut RouteTransitionEasing = new();
    private FrontendShellViewModel? _shellViewModel;

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
        GridRoot.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        GridRoot.RenderTransform = TransformOperations.Parse("translateY(60px) rotate(-3deg)");

        Opened += (_, _) => RunEntranceAnimation();
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

    private void RunEntranceAnimation()
    {
        Dispatcher.UIThread.Post(() =>
        {
            GridRoot.Transitions =
            [
                new Avalonia.Animation.DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(250),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                },
                new Avalonia.Animation.TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(600),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                }
            ];

            GridRoot.Opacity = 1;
            GridRoot.RenderTransform = TransformOperations.Parse("translateY(0px) rotate(0deg)");
        }, DispatcherPriority.Loaded);
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
            () => _ = PlayRouteTransitionAsync(e),
            DispatcherPriority.Loaded);
    }

    private void UpdateWindowChromeState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        GridResize.IsVisible = !isMaximized;
        MainBorder.Margin = isMaximized ? new Thickness(0) : new Thickness(18);
        MainBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(15, 15, 8, 8);
        MainClipBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(6);
    }

    private async Task PlayRouteTransitionAsync(ShellNavigationTransitionEventArgs transition)
    {
        var (leftHost, rightHost) = transition.IsLaunchRoute
            ? (LaunchLeftHost as Control, LaunchRightHost as Control)
            : (StandardLeftHost as Control, StandardRightHost as Control);

        if (leftHost is null || rightHost is null || !leftHost.IsVisible || !rightHost.IsVisible)
        {
            return;
        }

        var leftOffset = transition.Direction == ShellNavigationTransitionDirection.Forward ? -50d : 50d;
        var rightOffset = transition.Direction == ShellNavigationTransitionDirection.Forward ? 50d : -50d;

        PrepareRouteAnimation(leftHost, leftOffset);
        PrepareRouteAnimation(rightHost, rightOffset);

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);

        leftHost.Opacity = 1;
        leftHost.RenderTransform = TransformOperations.Parse("translateX(0px)");

        rightHost.Opacity = 1;
        rightHost.RenderTransform = TransformOperations.Parse("translateX(0px)");
    }

    private static void PrepareRouteAnimation(Control control, double offsetX)
    {
        control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        control.Transitions = null;
        control.Opacity = 0;
        control.RenderTransform = TransformOperations.Parse($"translateX({offsetX}px)");
        control.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = RouteTransitionDuration,
                Easing = RouteTransitionEasing
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = RouteTransitionDuration,
                Easing = RouteTransitionEasing
            }
        ];
    }
}

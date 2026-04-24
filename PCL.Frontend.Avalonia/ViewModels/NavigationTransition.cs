namespace PCL.Frontend.Avalonia.ViewModels;

internal enum NavigationTransitionDirection
{
    Forward = 0,
    Backward = 1
}

internal sealed class NavigationTransitionEventArgs(
    NavigationTransitionDirection direction,
    bool isLaunchRoute,
    bool animateLeftPane,
    bool animateRightPane) : EventArgs
{
    public NavigationTransitionDirection Direction { get; } = direction;

    public bool IsLaunchRoute { get; } = isLaunchRoute;

    public bool AnimateLeftPane { get; } = animateLeftPane;

    public bool AnimateRightPane { get; } = animateRightPane;
}

namespace PCL.Frontend.Avalonia.ViewModels;

internal enum ShellNavigationTransitionDirection
{
    Forward = 0,
    Backward = 1
}

internal sealed class ShellNavigationTransitionEventArgs(
    ShellNavigationTransitionDirection direction,
    bool isLaunchRoute,
    bool animateLeftPane,
    bool animateRightPane) : EventArgs
{
    public ShellNavigationTransitionDirection Direction { get; } = direction;

    public bool IsLaunchRoute { get; } = isLaunchRoute;

    public bool AnimateLeftPane { get; } = animateLeftPane;

    public bool AnimateRightPane { get; } = animateRightPane;
}

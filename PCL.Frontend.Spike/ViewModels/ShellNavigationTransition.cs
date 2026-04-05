namespace PCL.Frontend.Spike.ViewModels;

internal enum ShellNavigationTransitionDirection
{
    Forward = 0,
    Backward = 1
}

internal sealed class ShellNavigationTransitionEventArgs(
    ShellNavigationTransitionDirection direction,
    bool isLaunchRoute) : EventArgs
{
    public ShellNavigationTransitionDirection Direction { get; } = direction;

    public bool IsLaunchRoute { get; } = isLaunchRoute;
}

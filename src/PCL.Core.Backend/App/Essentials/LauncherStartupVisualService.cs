namespace PCL.Core.App.Essentials;

public static class LauncherStartupVisualService
{
    public static LauncherStartupVisualPlan GetVisualPlan(bool showStartupLogo)
    {
        return new LauncherStartupVisualPlan(
            showStartupLogo ? new LauncherSplashScreenPlan(@"Images\icon.ico") : null,
            new LauncherTooltipPresentationDefaults(
                InitialShowDelayMilliseconds: 300,
                BetweenShowDelayMilliseconds: 400,
                ShowDurationMilliseconds: 9_999_999,
                Placement: LauncherTooltipPlacement.Bottom,
                HorizontalOffset: 8.0,
                VerticalOffset: 4.0));
    }
}

public sealed record LauncherStartupVisualPlan(
    LauncherSplashScreenPlan? SplashScreen,
    LauncherTooltipPresentationDefaults TooltipDefaults)
{
    public bool ShouldShowSplashScreen => SplashScreen is not null;
}

public sealed record LauncherSplashScreenPlan(string IconPath);

public sealed record LauncherTooltipPresentationDefaults(
    int InitialShowDelayMilliseconds,
    int BetweenShowDelayMilliseconds,
    int ShowDurationMilliseconds,
    LauncherTooltipPlacement Placement,
    double HorizontalOffset,
    double VerticalOffset);

public enum LauncherTooltipPlacement
{
    Bottom = 0
}

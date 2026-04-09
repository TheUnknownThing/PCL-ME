namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class MotionDurations
{
    public static TimeSpan WindowEnterDelay { get; } = TimeSpan.FromMilliseconds(100);

    public static TimeSpan QuickState { get; } = TimeSpan.FromMilliseconds(110);

    public static TimeSpan InteractiveState { get; } = TimeSpan.FromMilliseconds(120);

    public static TimeSpan HintSettle { get; } = TimeSpan.FromMilliseconds(150);

    public static TimeSpan SurfaceState { get; } = TimeSpan.FromMilliseconds(200);

    public static TimeSpan EmphasizedSurfaceState { get; } = TimeSpan.FromMilliseconds(240);

    public static TimeSpan EntranceFade { get; } = TimeSpan.FromMilliseconds(170);

    public static TimeSpan ExitFade { get; } = TimeSpan.FromMilliseconds(130);

    public static TimeSpan EntranceTranslate { get; } = TimeSpan.FromMilliseconds(220);

    public static TimeSpan EntranceTranslateOvershoot { get; } = TimeSpan.FromMilliseconds(280);

    public static TimeSpan DividerResize { get; } = TimeSpan.FromMilliseconds(260);

    public static TimeSpan RouteTransition { get; } = TimeSpan.FromMilliseconds(300);

    public static TimeSpan StartupFade { get; } = TimeSpan.FromMilliseconds(250);

    public static TimeSpan StartupRotate { get; } = TimeSpan.FromMilliseconds(500);

    public static TimeSpan StartupLift { get; } = TimeSpan.FromMilliseconds(600);

    public static TimeSpan ModalOvershoot { get; } = TimeSpan.FromMilliseconds(92);

    public static TimeSpan ModalExit { get; } = TimeSpan.FromMilliseconds(96);

    public static TimeSpan HintNudge { get; } = TimeSpan.FromMilliseconds(70);

    public static int HintVisibleBaseMilliseconds { get; } = 800;

    public static int HintVisiblePerCharacterMilliseconds { get; } = 180;

    public static int HintVisibleMinCharacters { get; } = 5;

    public static int HintVisibleMaxCharacters { get; } = 23;
}

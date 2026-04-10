using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PCL.Frontend.Avalonia.Desktop.Animation;

internal static class MotionDurations
{
    public static MotionDurationSource Source { get; } = new();

    public static TimeSpan WindowEnterDelay => Source.WindowEnterDelay;

    public static TimeSpan QuickState => Source.QuickState;

    public static TimeSpan InteractiveState => Source.InteractiveState;

    public static TimeSpan HintSettle => Source.HintSettle;

    public static TimeSpan SurfaceState => Source.SurfaceState;

    public static TimeSpan EmphasizedSurfaceState => Source.EmphasizedSurfaceState;

    public static TimeSpan EntranceFade => Source.EntranceFade;

    public static TimeSpan ExitFade => Source.ExitFade;

    public static TimeSpan EntranceTranslate => Source.EntranceTranslate;

    public static TimeSpan EntranceTranslateOvershoot => Source.EntranceTranslateOvershoot;

    public static TimeSpan DividerResize => Source.DividerResize;

    public static TimeSpan RouteTransition => Source.RouteTransition;

    public static TimeSpan StartupFade => Source.StartupFade;

    public static TimeSpan StartupRotate => Source.StartupRotate;

    public static TimeSpan StartupLift => Source.StartupLift;

    public static TimeSpan ModalOvershoot => Source.ModalOvershoot;

    public static TimeSpan ModalExit => Source.ModalExit;

    public static TimeSpan HintNudge => Source.HintNudge;

    public static int HintVisibleBaseMilliseconds => Source.HintVisibleBaseMilliseconds;

    public static int HintVisiblePerCharacterMilliseconds => Source.HintVisiblePerCharacterMilliseconds;

    public static int HintVisibleMinCharacters => Source.HintVisibleMinCharacters;

    public static int HintVisibleMaxCharacters => Source.HintVisibleMaxCharacters;

    public static TimeSpan FrameInterval => Source.FrameInterval;

    public static int AnimationFps => Source.AnimationFps;

    public static event PropertyChangedEventHandler? Changed
    {
        add => Source.PropertyChanged += value;
        remove => Source.PropertyChanged -= value;
    }

    public static void ApplyRuntimePreferences(int animationFpsLimit, double debugAnimationSpeed)
    {
        Source.Apply(animationFpsLimit, debugAnimationSpeed);
    }

    public static TimeSpan ScaleAnimationDuration(TimeSpan baseDuration)
    {
        return Source.ScaleAnimationDuration(baseDuration);
    }

    public static int NormalizeFpsSetting(int storedFpsLimit)
    {
        return Math.Max(1, storedFpsLimit + 1);
    }

    public static double NormalizeSpeedSetting(double storedSpeedSetting)
    {
        return Math.Round(storedSpeedSetting) >= 30
            ? 200d
            : Math.Clamp(storedSpeedSetting / 10d + 0.1d, 0.1d, 3d);
    }

    internal sealed class MotionDurationSource : INotifyPropertyChanged
    {
        private static readonly string[] DurationPropertyNames =
        [
            nameof(WindowEnterDelay),
            nameof(QuickState),
            nameof(InteractiveState),
            nameof(HintSettle),
            nameof(SurfaceState),
            nameof(EmphasizedSurfaceState),
            nameof(EntranceFade),
            nameof(ExitFade),
            nameof(EntranceTranslate),
            nameof(EntranceTranslateOvershoot),
            nameof(DividerResize),
            nameof(RouteTransition),
            nameof(StartupFade),
            nameof(StartupRotate),
            nameof(StartupLift),
            nameof(ModalOvershoot),
            nameof(ModalExit),
            nameof(HintNudge)
        ];

        private int _animationFpsLimit = 59;
        private double _debugAnimationSpeed = 9;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int AnimationFpsLimit => _animationFpsLimit;

        public int AnimationFps => NormalizeFpsSetting(_animationFpsLimit);

        public double DebugAnimationSpeed => _debugAnimationSpeed;

        public double SpeedFactor => NormalizeSpeedSetting(_debugAnimationSpeed);

        public TimeSpan WindowEnterDelay => ScaleMilliseconds(100);

        public TimeSpan QuickState => ScaleMilliseconds(110);

        public TimeSpan InteractiveState => ScaleMilliseconds(120);

        public TimeSpan HintSettle => ScaleMilliseconds(150);

        public TimeSpan SurfaceState => ScaleMilliseconds(200);

        public TimeSpan EmphasizedSurfaceState => ScaleMilliseconds(240);

        public TimeSpan EntranceFade => ScaleMilliseconds(170);

        public TimeSpan ExitFade => ScaleMilliseconds(130);

        public TimeSpan EntranceTranslate => ScaleMilliseconds(220);

        public TimeSpan EntranceTranslateOvershoot => ScaleMilliseconds(280);

        public TimeSpan DividerResize => ScaleMilliseconds(260);

        public TimeSpan RouteTransition => ScaleMilliseconds(300);

        public TimeSpan StartupFade => ScaleMilliseconds(250);

        public TimeSpan StartupRotate => ScaleMilliseconds(500);

        public TimeSpan StartupLift => ScaleMilliseconds(600);

        public TimeSpan ModalOvershoot => ScaleMilliseconds(92);

        public TimeSpan ModalExit => ScaleMilliseconds(96);

        public TimeSpan HintNudge => ScaleMilliseconds(70);

        public int HintVisibleBaseMilliseconds => 800;

        public int HintVisiblePerCharacterMilliseconds => 180;

        public int HintVisibleMinCharacters => 5;

        public int HintVisibleMaxCharacters => 23;

        public TimeSpan FrameInterval => TimeSpan.FromMilliseconds(1000d / AnimationFps);

        public void Apply(int animationFpsLimit, double debugAnimationSpeed)
        {
            var normalizedFpsLimit = Math.Clamp(animationFpsLimit, 0, 59);
            var normalizedDebugSpeed = Math.Clamp(debugAnimationSpeed, 0, 30);
            if (_animationFpsLimit == normalizedFpsLimit && Math.Abs(_debugAnimationSpeed - normalizedDebugSpeed) < 0.0001d)
            {
                return;
            }

            _animationFpsLimit = normalizedFpsLimit;
            _debugAnimationSpeed = normalizedDebugSpeed;

            OnPropertyChanged(nameof(AnimationFpsLimit));
            OnPropertyChanged(nameof(AnimationFps));
            OnPropertyChanged(nameof(DebugAnimationSpeed));
            OnPropertyChanged(nameof(SpeedFactor));
            OnPropertyChanged(nameof(FrameInterval));
            foreach (var propertyName in DurationPropertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }

        public TimeSpan ScaleAnimationDuration(TimeSpan baseDuration)
        {
            if (baseDuration <= TimeSpan.Zero)
            {
                return baseDuration;
            }

            var scaledMilliseconds = baseDuration.TotalMilliseconds / SpeedFactor;
            return TimeSpan.FromMilliseconds(Math.Max(1d, scaledMilliseconds));
        }

        private TimeSpan ScaleMilliseconds(double milliseconds)
        {
            return ScaleAnimationDuration(TimeSpan.FromMilliseconds(milliseconds));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

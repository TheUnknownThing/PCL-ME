using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class WelcomeOnboardingOverlay : UserControl
{
    private static readonly Easing StepOpacityEasing = new CubicEaseOut();
    private static readonly Easing StepTranslateEasing = new BackEaseOut();
    private const string TermsLinkKind = "terms";
    private const string ReadmeLinkKind = "readme";

    private readonly ComboBox _launcherLocaleComboBox;
    private readonly ComboBox _themeModeComboBox;
    private LauncherViewModel? _launcher;
    private bool _isRenderedOpen;
    private int _overlayAnimationVersion;
    private int _stepAnimationVersion;
    private int _lastWelcomeStepIndex = -1;

    public WelcomeOnboardingOverlay()
    {
        InitializeComponent();
        _launcherLocaleComboBox = this.FindControl<ComboBox>("WelcomeLauncherLocaleComboBox")
            ?? throw new InvalidOperationException("The welcome overlay did not contain the launcher locale combo box.");
        _themeModeComboBox = this.FindControl<ComboBox>("WelcomeThemeModeComboBox")
            ?? throw new InvalidOperationException("The welcome overlay did not contain the theme mode combo box.");
        OverlayRoot.IsVisible = false;
        OverlayRoot.IsHitTestVisible = false;
        PclModalMotion.ResetToClosedState(WelcomeBackdrop, WelcomeCard);
        WelcomeLicenseBodyHost.PointerPressed += OnWelcomeLicenseBodyPointerPressed;
        WelcomeLicenseBodyHost.PointerMoved += OnWelcomeLicenseBodyPointerMoved;
        WelcomeLicenseBodyHost.PointerExited += OnWelcomeLicenseBodyPointerExited;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => QueueOverlaySync();
        DetachedFromVisualTree += (_, _) => ObserveLauncher(null);
        ScheduleSelectionRestore();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveLauncher(DataContext as LauncherViewModel);
        QueueOverlaySync();
        ScheduleSelectionRestore();
    }

    private void ObserveLauncher(LauncherViewModel? launcher)
    {
        if (ReferenceEquals(_launcher, launcher))
        {
            return;
        }

        if (_launcher is not null)
        {
            _launcher.PropertyChanged -= OnLauncherPropertyChanged;
        }

        _launcher = launcher;
        if (_launcher is not null)
        {
            _launcher.PropertyChanged += OnLauncherPropertyChanged;
        }

        _lastWelcomeStepIndex = _launcher?.WelcomeCurrentStep ?? -1;
    }

    private void OnLauncherPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.IsWelcomeOverlayVisible))
        {
            QueueOverlaySync();
            return;
        }

        if (e.PropertyName == nameof(LauncherViewModel.WelcomeCurrentStep))
        {
            QueueStepTransition();
            return;
        }

        if (e.PropertyName is nameof(LauncherViewModel.LauncherLocaleOptions)
            or nameof(LauncherViewModel.SelectedLauncherLocaleIndex)
            or nameof(LauncherViewModel.DarkModeOptions)
            or nameof(LauncherViewModel.SelectedDarkModeIndex))
        {
            ScheduleSelectionRestore();
        }
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(RestoreSelections, DispatcherPriority.Background);
    }

    private void RestoreSelections()
    {
        if (_launcher is null)
        {
            return;
        }

        ApplySelectedIndex(_launcherLocaleComboBox, _launcher.SelectedLauncherLocaleIndex);
        ApplySelectedIndex(_themeModeComboBox, _launcher.SelectedDarkModeIndex);
    }

    private static void ApplySelectedIndex(ComboBox comboBox, int selectedIndex)
    {
        if (selectedIndex < 0 || comboBox.SelectedIndex == selectedIndex)
        {
            return;
        }

        comboBox.SelectedIndex = selectedIndex;
    }

    private void QueueOverlaySync()
    {
        Dispatcher.UIThread.Post(() => _ = SyncOverlayAsync(), DispatcherPriority.Render);
    }

    private void QueueStepTransition()
    {
        Dispatcher.UIThread.Post(() => _ = PlayStepTransitionAsync(), DispatcherPriority.Render);
    }

    private async Task SyncOverlayAsync()
    {
        var shouldShow = _launcher?.IsWelcomeOverlayVisible == true;
        if (shouldShow == _isRenderedOpen)
        {
            return;
        }

        var version = ++_overlayAnimationVersion;
        _isRenderedOpen = shouldShow;

        if (shouldShow)
        {
            OverlayRoot.IsVisible = true;
            OverlayRoot.IsHitTestVisible = true;
            PclModalMotion.ResetToClosedState(WelcomeBackdrop, WelcomeCard);
            await PclModalMotion.PlayOpenAsync(
                WelcomeBackdrop,
                WelcomeCard,
                () => version == _overlayAnimationVersion && _isRenderedOpen);
            _lastWelcomeStepIndex = _launcher?.WelcomeCurrentStep ?? -1;
            return;
        }

        OverlayRoot.IsHitTestVisible = false;
        await PclModalMotion.PlayCloseAsync(
            WelcomeBackdrop,
            WelcomeCard,
            () => version == _overlayAnimationVersion && !_isRenderedOpen);
        if (version != _overlayAnimationVersion || _isRenderedOpen)
        {
            return;
        }

        OverlayRoot.IsVisible = false;
        PclModalMotion.ResetToClosedState(WelcomeBackdrop, WelcomeCard);
    }

    private async Task PlayStepTransitionAsync()
    {
        if (_launcher is null || !_isRenderedOpen)
        {
            _lastWelcomeStepIndex = _launcher?.WelcomeCurrentStep ?? -1;
            return;
        }

        var currentStep = _launcher.WelcomeCurrentStep;
        if (_lastWelcomeStepIndex < 0)
        {
            _lastWelcomeStepIndex = currentStep;
            return;
        }

        if (currentStep == _lastWelcomeStepIndex)
        {
            return;
        }

        var direction = currentStep > _lastWelcomeStepIndex ? 1d : -1d;
        _lastWelcomeStepIndex = currentStep;
        var version = ++_stepAnimationVersion;

        PrepareStepElement(WelcomeStepHeader, 0d, 10d);
        PrepareStepElement(WelcomeStepContentHost, 34d * direction, 0d);
        PrepareStepElement(WelcomeActionBar, 0d, 8d);

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _stepAnimationVersion || !_isRenderedOpen)
        {
            return;
        }

        StartStepElementAnimation(WelcomeStepHeader, useBounce: false);
        StartStepElementAnimation(WelcomeStepContentHost, useBounce: true);
        StartStepElementAnimation(WelcomeActionBar, useBounce: false);
    }

    private static void PrepareStepElement(Control target, double offsetX, double offsetY)
    {
        target.Transitions = null;
        target.Opacity = 0d;
        target.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        var transform = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        transform.Transitions = null;
        transform.X = offsetX;
        transform.Y = offsetY;
        target.RenderTransform = transform;
    }

    private static void StartStepElementAnimation(Control target, bool useBounce)
    {
        var translateEasing = useBounce ? StepTranslateEasing : StepOpacityEasing;
        target.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = MotionDurations.EntranceFade,
                Easing = StepOpacityEasing
            }
        ];

        var transform = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        transform.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = MotionDurations.EntranceTranslateOvershoot,
                Easing = translateEasing
            },
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = MotionDurations.EntranceTranslateOvershoot,
                Easing = translateEasing
            }
        ];
        target.RenderTransform = transform;
        target.Opacity = 1d;
        transform.X = 0d;
        transform.Y = 0d;
    }

    private void OnWelcomeLicenseBodyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_launcher is null || !e.GetCurrentPoint(WelcomeLicenseBodyHost).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(WelcomeLicenseBodyText);
        var linkKind = HitTestWelcomeLicenseLink(point);
        if (linkKind is null)
        {
            return;
        }

        if (linkKind == TermsLinkKind)
        {
            _launcher.WelcomeOpenTermsCommand.Execute(null);
        }
        else if (linkKind == ReadmeLinkKind)
        {
            _launcher.WelcomeOpenReadmeCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnWelcomeLicenseBodyPointerMoved(object? sender, PointerEventArgs e)
    {
        WelcomeLicenseBodyHost.Cursor = HitTestWelcomeLicenseLink(e.GetPosition(WelcomeLicenseBodyText)) is null
            ? new Cursor(StandardCursorType.Arrow)
            : new Cursor(StandardCursorType.Hand);
    }

    private void OnWelcomeLicenseBodyPointerExited(object? sender, PointerEventArgs e)
    {
        WelcomeLicenseBodyHost.Cursor = new Cursor(StandardCursorType.Arrow);
    }

    private string? HitTestWelcomeLicenseLink(Point point)
    {
        if (_launcher is null || WelcomeLicenseBodyText.Bounds.Width <= 0)
        {
            return null;
        }

        var layout = WelcomeLicenseBodyText.TextLayout;
        if (layout is null)
        {
            return null;
        }

        var prefix = _launcher.WelcomeTermsBodyPrefix;
        var terms = _launcher.WelcomeTermsLinkText;
        var middle = _launcher.WelcomeTermsBodyMiddle;
        var readme = _launcher.WelcomeReadmeLinkText;

        var termsStart = prefix.Length;
        if (ContainsPoint(layout.HitTestTextRange(termsStart, terms.Length), point))
        {
            return TermsLinkKind;
        }

        var readmeStart = termsStart + terms.Length + middle.Length;
        return ContainsPoint(layout.HitTestTextRange(readmeStart, readme.Length), point)
            ? ReadmeLinkKind
            : null;
    }

    private static bool ContainsPoint(IEnumerable<Rect> rects, Point point)
    {
        foreach (var rect in rects)
        {
            if (rect.Contains(point))
            {
                return true;
            }
        }

        return false;
    }
}

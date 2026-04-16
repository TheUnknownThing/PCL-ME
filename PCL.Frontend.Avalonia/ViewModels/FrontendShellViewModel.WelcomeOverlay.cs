using Avalonia.Media;
using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string WelcomeEulaUrl = "https://shimo.im/docs/rGrd8pY8xWkt6ryW";
    private const string WelcomeFeedbackUrl = "https://github.com/TheUnknownThing/PCL-ME/issues";
    private const string WelcomeGitHubUrl = "https://github.com/TheUnknownThing/PCL-ME";
    private const int WelcomeTotalSteps = 4;

    private bool _welcomeOverlayDismissed;
    private int _welcomeCurrentStep;
    private bool _welcomeEulaAccepted;

    private ActionCommand? _welcomeNextStepCommandBacking;
    private ActionCommand? _welcomePreviousStepCommandBacking;
    private ActionCommand? _welcomeOpenEulaCommandBacking;
    private ActionCommand? _welcomeDeclineCommandBacking;
    private ActionCommand? _welcomeOpenFeedbackCommandBacking;
    private ActionCommand? _welcomeOpenGitHubCommandBacking;

    // ── Overlay visibility ───────────────────────────────────────────────────

    public bool IsWelcomeOverlayVisible =>
        _shellComposition.NeedsOnboarding && !_welcomeOverlayDismissed;

    // ── Step identity ────────────────────────────────────────────────────────

    public int WelcomeCurrentStep => _welcomeCurrentStep;

    public bool IsWelcomeLanguageStep => _welcomeCurrentStep == 0;
    public bool IsWelcomeLicenseStep => _welcomeCurrentStep == 1;
    public bool IsWelcomeThemeStep => _welcomeCurrentStep == 2;
    public bool IsWelcomeDoneStep => _welcomeCurrentStep == 3;
    public bool IsWelcomeAdvanceArrowVisible => _welcomeCurrentStep < WelcomeTotalSteps - 1;

    // ── Step content ─────────────────────────────────────────────────────────

    public string WelcomeStepTitle => _welcomeCurrentStep switch
    {
        0 => _i18n.T("welcome.step_0.title"),
        1 => _i18n.T("welcome.step_1.title"),
        2 => _i18n.T("welcome.step_2.title"),
        3 => _i18n.T("welcome.step_3.title"),
        _ => string.Empty
    };

    public string WelcomeStepDescription => _welcomeCurrentStep switch
    {
        0 => _i18n.T("welcome.step_0.description"),
        1 => _i18n.T("welcome.step_1.description"),
        2 => _i18n.T("welcome.step_2.description"),
        3 => _i18n.T("welcome.step_3.description"),
        _ => string.Empty
    };

    // Step 1 – License

    public string WelcomeEulaSummary => _i18n.T("welcome.step_1.eula_summary");
    public string WelcomeOpenEulaButtonText => _i18n.T("welcome.step_1.open_eula");
    public string WelcomeDeclineButtonText => _i18n.T("welcome.step_1.decline");
    public string WelcomeEulaAcceptLabel => _i18n.T("welcome.step_1.accept_label");

    // Step 3 – Done

    public string WelcomeFeedbackButtonText => _i18n.T("welcome.step_3.feedback");
    public string WelcomeGitHubButtonText => _i18n.T("welcome.step_3.github");
    public string WelcomeDoneButtonToolTip => _i18n.T("common.actions.confirm");

    // ── EULA checkbox ────────────────────────────────────────────────────────

    public bool WelcomeEulaAccepted
    {
        get => _welcomeEulaAccepted;
        set
        {
            if (SetProperty(ref _welcomeEulaAccepted, value))
            {
                RaisePropertyChanged(nameof(CanGoToNextWelcomeStep));
                WelcomeNextStepCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public bool CanGoToPreviousWelcomeStep => _welcomeCurrentStep > 0;

    /// <summary>
    /// Next is always enabled except on the License step when the EULA box is unchecked.
    /// </summary>
    public bool CanGoToNextWelcomeStep =>
        _welcomeCurrentStep != 1 || _welcomeEulaAccepted;

    // ── Dot indicator brushes ────────────────────────────────────────────────

    public IBrush WelcomeDot0Brush => GetWelcomeDotBrush(0);
    public IBrush WelcomeDot1Brush => GetWelcomeDotBrush(1);
    public IBrush WelcomeDot2Brush => GetWelcomeDotBrush(2);
    public IBrush WelcomeDot3Brush => GetWelcomeDotBrush(3);

    // ── Commands (lazy) ──────────────────────────────────────────────────────

    public ActionCommand WelcomeNextStepCommand =>
        _welcomeNextStepCommandBacking ??=
            new ActionCommand(GoToNextWelcomeStep, () => CanGoToNextWelcomeStep);

    public ActionCommand WelcomePreviousStepCommand =>
        _welcomePreviousStepCommandBacking ??=
            new ActionCommand(GoToPreviousWelcomeStep);

    public ActionCommand WelcomeOpenEulaCommand =>
        _welcomeOpenEulaCommandBacking ??=
            new ActionCommand(OpenWelcomeEulaUrl);

    public ActionCommand WelcomeDeclineCommand =>
        _welcomeDeclineCommandBacking ??=
            new ActionCommand(DeclineOnboarding);

    public ActionCommand WelcomeOpenFeedbackCommand =>
        _welcomeOpenFeedbackCommandBacking ??=
            new ActionCommand(OpenWelcomeFeedbackUrl);

    public ActionCommand WelcomeOpenGitHubCommand =>
        _welcomeOpenGitHubCommandBacking ??=
            new ActionCommand(OpenWelcomeGitHubUrl);

    // ── Private logic ────────────────────────────────────────────────────────

    private void GoToNextWelcomeStep()
    {
        if (_welcomeCurrentStep >= WelcomeTotalSteps - 1)
        {
            CompleteOnboarding();
            return;
        }

        _welcomeCurrentStep++;
        RaiseWelcomeStepProperties();
    }

    private void GoToPreviousWelcomeStep()
    {
        if (_welcomeCurrentStep <= 0)
        {
            return;
        }

        _welcomeCurrentStep--;
        RaiseWelcomeStepProperties();
    }

    private void CompleteOnboarding()
    {
        // Persist acceptance so the old EULA prompt never surfaces.
        _shellActionService.AcceptLauncherEula();
        _shellActionService.PersistSharedValue("SystemOnboardingCompleted", true);

        // Update the in-memory consent request so the startup prompt queue
        // reflects the accepted state for the rest of this session.
        UpdateStartupConsentRequest(request => request with { HasAcceptedEula = true });

        _welcomeOverlayDismissed = true;
        RaisePropertyChanged(nameof(IsWelcomeOverlayVisible));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }

    private void DeclineOnboarding()
    {
        _shellActionService.ExitLauncher();
    }

    private void OpenWelcomeEulaUrl()
    {
        OpenExternalTarget(WelcomeEulaUrl, "Opened EULA URL from the welcome overlay.");
    }

    private void OpenWelcomeFeedbackUrl()
    {
        OpenExternalTarget(WelcomeFeedbackUrl, "Opened feedback URL from the welcome overlay.");
    }

    private void OpenWelcomeGitHubUrl()
    {
        OpenExternalTarget(WelcomeGitHubUrl, "Opened GitHub URL from the welcome overlay.");
    }

    // Called from RaiseSectionBLocalizedProperties so titles/descriptions
    // update immediately when the user switches language on step 0.
    private void RaiseWelcomeLocaleProperties()
    {
        RaisePropertyChanged(nameof(WelcomeStepTitle));
        RaisePropertyChanged(nameof(WelcomeStepDescription));
        RaisePropertyChanged(nameof(WelcomeEulaSummary));
        RaisePropertyChanged(nameof(WelcomeOpenEulaButtonText));
        RaisePropertyChanged(nameof(WelcomeDeclineButtonText));
        RaisePropertyChanged(nameof(WelcomeEulaAcceptLabel));
        RaisePropertyChanged(nameof(WelcomeFeedbackButtonText));
        RaisePropertyChanged(nameof(WelcomeGitHubButtonText));
        RaisePropertyChanged(nameof(WelcomeDoneButtonToolTip));
    }

    private void RaiseWelcomeStepProperties()
    {
        RaisePropertyChanged(nameof(WelcomeCurrentStep));
        RaisePropertyChanged(nameof(IsWelcomeLanguageStep));
        RaisePropertyChanged(nameof(IsWelcomeLicenseStep));
        RaisePropertyChanged(nameof(IsWelcomeThemeStep));
        RaisePropertyChanged(nameof(IsWelcomeDoneStep));
        RaisePropertyChanged(nameof(IsWelcomeAdvanceArrowVisible));
        RaiseWelcomeLocaleProperties();
        RaisePropertyChanged(nameof(CanGoToPreviousWelcomeStep));
        RaisePropertyChanged(nameof(CanGoToNextWelcomeStep));
        RaisePropertyChanged(nameof(WelcomeDot0Brush));
        RaisePropertyChanged(nameof(WelcomeDot1Brush));
        RaisePropertyChanged(nameof(WelcomeDot2Brush));
        RaisePropertyChanged(nameof(WelcomeDot3Brush));
        WelcomeNextStepCommand.NotifyCanExecuteChanged();
        WelcomePreviousStepCommand.NotifyCanExecuteChanged();
    }

    private IBrush GetWelcomeDotBrush(int step)
    {
        return _welcomeCurrentStep == step
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#CCCCCC");
    }
}

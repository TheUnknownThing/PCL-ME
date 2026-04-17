using Avalonia.Media;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string WelcomeEulaUrl = "https://shimo.im/docs/rGrd8pY8xWkt6ryW";
    private const string WelcomeReadmeUrl = "https://github.com/TheUnknownThing/PCL-ME#readme";
    private const string WelcomeTutorialFallbackUrl = "https://github.com/TheUnknownThing/PCL-ME#readme";
    private const string WelcomeFeedbackUrl = "https://github.com/TheUnknownThing/PCL-ME/issues";
    private const string WelcomeGitHubUrl = "https://github.com/TheUnknownThing/PCL-ME";
    private const int WelcomeTotalSteps = 3;

    private bool _welcomeOverlayDismissed;
    private int _welcomeCurrentStep;

    private ActionCommand? _welcomeNextStepCommandBacking;
    private ActionCommand? _welcomePreviousStepCommandBacking;
    private ActionCommand? _welcomeOpenEulaCommandBacking;
    private ActionCommand? _welcomeOpenReadmeCommandBacking;
    private ActionCommand? _welcomeOpenTutorialCommandBacking;
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
    public bool IsWelcomeDoneStep => _welcomeCurrentStep == 2;

    // ── Step content ─────────────────────────────────────────────────────────

    public string WelcomeStepTitle => _welcomeCurrentStep switch
    {
        0 => _i18n.T("welcome.step_0.title"),
        1 => _i18n.T("welcome.step_1.title"),
        2 => _i18n.T("welcome.step_2.title"),
        _ => string.Empty
    };

    public string WelcomeStepDescription => _welcomeCurrentStep switch
    {
        0 => _i18n.T("welcome.step_0.description"),
        1 => _i18n.T("welcome.step_1.description"),
        2 => _i18n.T("welcome.step_2.description"),
        _ => string.Empty
    };

    public string WelcomeLanguageEntryDescription => _i18n.T("welcome.step_0.language_hint");
    public string WelcomeThemeEntryDescription => _i18n.T("welcome.step_0.theme_hint");

    // Step 1 – License

    public string WelcomeTermsBodyPrefix => _i18n.T("welcome.step_1.community_notice");
    public string WelcomeTermsLinkText => _i18n.T("welcome.step_1.open_eula");
    public string WelcomeTermsBodyMiddle => _i18n.T("welcome.step_1.eula_summary");
    public string WelcomeReadmeLinkText => _i18n.T("welcome.step_1.open_readme");
    public string WelcomeTermsBodySuffix => _i18n.T("welcome.step_1.readme_summary");
    public string WelcomeAcceptAndContinueButtonText => _i18n.T("welcome.step_1.accept_and_continue");

    // Step 2 – Done

    public string WelcomeTutorialButtonText => _i18n.T("welcome.step_2.tutorial");
    public string WelcomeFeedbackButtonText => _i18n.T("welcome.step_2.feedback");
    public string WelcomeGitHubButtonText => _i18n.T("welcome.step_2.github");
    public string WelcomeBackButtonText => _i18n.T("common.actions.back");
    public string WelcomePrimaryActionText => _welcomeCurrentStep switch
    {
        1 => WelcomeAcceptAndContinueButtonText,
        WelcomeTotalSteps - 1 => _i18n.T("common.actions.confirm"),
        _ => _i18n.T("common.actions.continue")
    };

    // ── Navigation ───────────────────────────────────────────────────────────

    public bool CanGoToPreviousWelcomeStep => _welcomeCurrentStep > 0;

    public bool CanGoToNextWelcomeStep => true;

    // ── Dot indicator brushes ────────────────────────────────────────────────

    public IBrush WelcomeDot0Brush => GetWelcomeDotBrush(0);
    public IBrush WelcomeDot1Brush => GetWelcomeDotBrush(1);
    public IBrush WelcomeDot2Brush => GetWelcomeDotBrush(2);

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

    public ActionCommand WelcomeOpenTermsCommand => WelcomeOpenEulaCommand;

    public ActionCommand WelcomeOpenReadmeCommand =>
        _welcomeOpenReadmeCommandBacking ??=
            new ActionCommand(OpenWelcomeReadmeUrl);

    public ActionCommand WelcomeOpenTutorialCommand =>
        _welcomeOpenTutorialCommandBacking ??=
            new ActionCommand(OpenWelcomeTutorial);

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

        if (_welcomeCurrentStep == 1)
        {
            _shellActionService.AcceptLauncherEula();
            UpdateStartupConsentRequest(request => request with { HasAcceptedEula = true });
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
        if (_welcomeOverlayDismissed)
        {
            return;
        }

        // Persist acceptance so the old EULA prompt never surfaces.
        _shellActionService.AcceptLauncherEula();
        _shellActionService.PersistSharedValue("SystemOnboardingCompleted", true);

        // Update the in-memory consent request so the startup prompt queue
        // reflects the accepted state for the rest of this session.
        UpdateStartupConsentRequest(request => request with { HasAcceptedEula = true });

        _welcomeOverlayDismissed = true;
        RaisePropertyChanged(nameof(IsWelcomeOverlayVisible));
        RaisePropertyChanged(nameof(IsTopLevelNavigationInteractive));
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

    private void OpenWelcomeReadmeUrl()
    {
        OpenExternalTarget(WelcomeReadmeUrl, "Opened README URL from the welcome overlay.");
    }

    private void OpenWelcomeTutorial()
    {
        CompleteOnboarding();
        if (TryResolveHelpEntry("新手教程.json", out var helpEntry))
        {
            ShowHelpDetail(helpEntry, addActivity: true);
            return;
        }

        OpenExternalTarget(WelcomeTutorialFallbackUrl, "Opened the onboarding tutorial fallback URL.");
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
        RaisePropertyChanged(nameof(WelcomeLanguageEntryDescription));
        RaisePropertyChanged(nameof(WelcomeThemeEntryDescription));
        RaisePropertyChanged(nameof(WelcomeTermsBodyPrefix));
        RaisePropertyChanged(nameof(WelcomeTermsLinkText));
        RaisePropertyChanged(nameof(WelcomeTermsBodyMiddle));
        RaisePropertyChanged(nameof(WelcomeReadmeLinkText));
        RaisePropertyChanged(nameof(WelcomeTermsBodySuffix));
        RaisePropertyChanged(nameof(WelcomeAcceptAndContinueButtonText));
        RaisePropertyChanged(nameof(WelcomeTutorialButtonText));
        RaisePropertyChanged(nameof(WelcomeFeedbackButtonText));
        RaisePropertyChanged(nameof(WelcomeGitHubButtonText));
        RaisePropertyChanged(nameof(WelcomeBackButtonText));
        RaisePropertyChanged(nameof(WelcomePrimaryActionText));
    }

    private void RaiseWelcomeStepProperties()
    {
        RaisePropertyChanged(nameof(WelcomeCurrentStep));
        RaisePropertyChanged(nameof(IsWelcomeLanguageStep));
        RaisePropertyChanged(nameof(IsWelcomeLicenseStep));
        RaisePropertyChanged(nameof(IsWelcomeDoneStep));
        RaiseWelcomeLocaleProperties();
        RaisePropertyChanged(nameof(CanGoToPreviousWelcomeStep));
        RaisePropertyChanged(nameof(CanGoToNextWelcomeStep));
        RaiseWelcomeDotBrushProperties();
        WelcomeNextStepCommand.NotifyCanExecuteChanged();
        WelcomePreviousStepCommand.NotifyCanExecuteChanged();
    }

    private void RaiseWelcomeDotBrushProperties()
    {
        RaisePropertyChanged(nameof(WelcomeDot0Brush));
        RaisePropertyChanged(nameof(WelcomeDot1Brush));
        RaisePropertyChanged(nameof(WelcomeDot2Brush));
    }

    private IBrush GetWelcomeDotBrush(int step)
    {
        return _welcomeCurrentStep == step
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB")
            : FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#CCCCCC");
    }

    private void HandleWelcomeAppearanceChanged()
    {
        Dispatcher.UIThread.Post(RaiseWelcomeDotBrushProperties, DispatcherPriority.Render);
    }
}

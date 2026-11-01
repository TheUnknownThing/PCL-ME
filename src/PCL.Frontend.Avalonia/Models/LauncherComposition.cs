using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record LauncherComposition(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest,
    LauncherStartupConsentResult StartupConsentResult,
    LauncherFrontendNavigationViewRequest NavigationRequest,
    string EnvironmentLabel,
    string InputLabel,
    bool NeedsOnboarding);

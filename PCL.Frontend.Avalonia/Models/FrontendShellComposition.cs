using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendShellComposition(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest,
    LauncherStartupConsentResult StartupConsentResult,
    LauncherFrontendNavigationViewRequest NavigationRequest,
    string EnvironmentLabel,
    string InputLabel,
    bool NeedsOnboarding);

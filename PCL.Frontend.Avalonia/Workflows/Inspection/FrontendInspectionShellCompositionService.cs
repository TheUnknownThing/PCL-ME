using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class FrontendInspectionShellCompositionService
{
    public static FrontendShellComposition? TryComposeReplay(AvaloniaCommandOptions options)
    {
        var replayInputs = AvaloniaInputStore.LoadShellInputs(options.InputRoot);
        if (replayInputs is null)
        {
            return null;
        }

        var startupConsentRequest = replayInputs.StartupInputs.StartupConsentRequest;
        return new FrontendShellComposition(
            replayInputs.StartupInputs.StartupWorkflowRequest,
            startupConsentRequest,
            LauncherStartupConsentService.Evaluate(startupConsentRequest),
            replayInputs.NavigationRequest,
            "Replay-backed shell inputs",
            $"Input root: {options.InputRoot}");
    }
}

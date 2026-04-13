using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class FrontendInspectionShellCompositionService
{
    public static FrontendShellComposition? TryComposeReplay(SpikeCommandOptions options)
    {
        var replayInputs = SpikeInputStore.LoadShellInputs(options.InputRoot);
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

using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class FrontendInspectionLaunchCompositionService
{
    public static FrontendLaunchComposition? TryComposeReplay(
        SpikeCommandOptions options,
        string? saveBatchPath = null)
    {
        var replayInputs = SpikeInputStore.LoadLaunchInputs(options.InputRoot);
        return replayInputs is null
            ? null
            : FrontendLaunchCompositionService.FromSpikePlan(
                SpikeSampleFactory.BuildLaunchPlan(replayInputs, saveBatchPath));
    }

    public static FrontendInspectionLaunchDefaults CreateRuntimeDefaults(string scenario)
    {
        return new FrontendInspectionLaunchDefaults(
            SpikeSampleFactory.CreateDefaultLaunchInputs(scenario),
            SpikeHostInputFactory.CreateLaunchInputs(scenario).JavaRuntimeInputs);
    }
}

internal sealed record FrontendInspectionLaunchDefaults(
    LaunchSpikeInputs ScenarioDefaults,
    JavaRuntimeSpikeInputs HostJavaInputs);

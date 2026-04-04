using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class FrontendInspectionCrashCompositionService
{
    public static CrashSpikePlan Compose(SpikeCommandOptions options)
    {
        return SpikeSampleFactory.BuildCrashPlan(SpikeInputResolver.ResolveCrashInputs(options));
    }
}

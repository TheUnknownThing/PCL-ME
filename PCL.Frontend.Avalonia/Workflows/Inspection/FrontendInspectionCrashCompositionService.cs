using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class FrontendInspectionCrashCompositionService
{
    public static CrashAvaloniaPlan Compose(AvaloniaCommandOptions options)
    {
        return AvaloniaSampleFactory.BuildCrashPlan(AvaloniaInputResolver.ResolveCrashInputs(options));
    }
}

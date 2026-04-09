using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class FrontendInspectionLaunchCompositionService
{
    public static FrontendLaunchComposition? TryComposeReplay(
        AvaloniaCommandOptions options,
        string? saveBatchPath = null)
    {
        var replayInputs = AvaloniaInputStore.LoadLaunchInputs(options.InputRoot);
        return replayInputs is null
            ? null
            : FrontendLaunchCompositionService.FromAvaloniaPlan(
                AvaloniaSampleFactory.BuildLaunchPlan(replayInputs, saveBatchPath));
    }

    public static FrontendInspectionLaunchDefaults CreateRuntimeDefaults(string scenario)
    {
        return new FrontendInspectionLaunchDefaults(
            AvaloniaSampleFactory.CreateDefaultLaunchInputs(scenario),
            AvaloniaHostInputFactory.CreateLaunchInputs(scenario).JavaRuntimeInputs);
    }
}

internal sealed record FrontendInspectionLaunchDefaults(
    LaunchAvaloniaInputs ScenarioDefaults,
    JavaRuntimeAvaloniaInputs HostJavaInputs);

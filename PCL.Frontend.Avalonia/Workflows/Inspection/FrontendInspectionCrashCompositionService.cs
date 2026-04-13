using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class FrontendInspectionCrashCompositionService
{
    public static CrashAvaloniaPlan Compose(AvaloniaCommandOptions options)
    {
        var replayInputs = AvaloniaInputStore.LoadCrashInputs(options.InputRoot);
        if (replayInputs is not null)
        {
            return Compose(replayInputs);
        }

        if (!options.UseHostEnvironment && options.Command != AvaloniaCommandKind.App)
        {
            return AvaloniaSampleFactory.BuildCrashPlan(AvaloniaSampleFactory.CreateDefaultCrashInputs());
        }

        return Compose(AvaloniaHostInputFactory.CreateCrashInputs());
    }

    public static CrashAvaloniaPlan Compose(CrashAvaloniaInputs inputs)
    {
        var analysisResult = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
            BuildAnalysisSourcePaths(inputs.ExportPlanRequest),
            inputs.ExportPlanRequest.CurrentLauncherLogFilePath));
        var outputPrompt = MinecraftCrashWorkflowService.BuildOutputPrompt(inputs.OutputPromptRequest with
        {
            ResultText = analysisResult.ResultText,
            HasDirectFile = analysisResult.HasDirectFile
        });

        return new CrashAvaloniaPlan(
            outputPrompt,
            MinecraftCrashExportWorkflowService.CreatePlan(inputs.ExportPlanRequest));
    }

    private static IReadOnlyList<string> BuildAnalysisSourcePaths(MinecraftCrashExportPlanRequest request)
    {
        return request.SourceFilePaths
            .Concat(request.AdditionalSourceFilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

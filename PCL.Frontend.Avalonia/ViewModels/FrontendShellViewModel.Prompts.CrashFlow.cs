using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using System.Runtime.InteropServices;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void OpenCrashLogFromPrompt()
    {
        var logPath = _shellActionService.MaterializeCrashLog(_activeCrashPlan);
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), T("shell.prompts.activities.navigate.game_log"));
        RefreshGameLogSurface();
        AddActivity(T("shell.prompts.activities.open_log.title"), T("shell.prompts.activities.open_log.body", ("path", logPath)));
    }

    private void ExportCrashReportFromPrompt()
    {
        var exportResult = _shellActionService.ExportCrashReport(_activeCrashPlan);
        OpenExternalTarget(exportResult.ArchivePath, T("shell.prompts.external_open.success.crash_report_exported"));
        AddActivity(
            T("shell.prompts.activities.crash_report_exported.title"),
            T("shell.prompts.activities.crash_report_exported.body", ("path", exportResult.ArchivePath), ("count", exportResult.ArchivedFileCount)));
    }

    private void ShowCrashPromptForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        _activeCrashPlan = BuildCrashPlanForLaunchFailure(startResult);
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity(T("shell.prompts.activities.crash_prompt_shown.title"), T("shell.prompts.activities.crash_prompt_shown.body"));
    }

    private CrashAvaloniaPlan BuildCrashPlanForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        var exportRequest = new MinecraftCrashExportPlanRequest(
            Timestamp: DateTime.Now,
            ReportDirectory: Path.Combine(
                _shellActionService.RuntimePaths.FrontendTempDirectory,
                "CrashReport",
                DateTime.Now.ToString("yyyy-MM-dd")),
            LauncherVersionName: "frontend-avalonia",
            UniqueAddress: _instanceComposition.Selection.InstanceDirectory ??
                           _launchComposition.InstancePath,
            SourceFilePaths:
            [
                startResult.LaunchScriptPath,
                startResult.RawOutputLogPath
            ],
            AdditionalSourceFilePaths:
            [
                startResult.SessionSummaryPath,
                Path.Combine(_launchComposition.InstancePath, "logs", "latest.log")
            ],
            CurrentLauncherLogFilePath: _shellActionService.RuntimePaths.ResolveCurrentLauncherLogFilePath(),
            Environment: GetHostEnvironmentSnapshot(),
            CurrentAccessToken: _launchComposition.SelectedProfile.AccessToken,
            CurrentUserUuid: _launchComposition.SelectedProfile.Uuid,
            UserProfilePath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var analysisResult = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
            BuildAnalysisSourcePaths(exportRequest),
            exportRequest.CurrentLauncherLogFilePath), _i18n.T);
        var resultText = analysisResult.HasKnownReason
            ? analysisResult.ResultText
            : $"{analysisResult.ResultText}{Environment.NewLine}{Environment.NewLine}{T("shell.prompts.crash.details_header")}{Environment.NewLine}{BuildLaunchFailureMessage(startResult)}";
        var outputPrompt = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            resultText,
            IsManualAnalysis: false,
            HasDirectFile: analysisResult.HasDirectFile,
            CanOpenModLoaderSettings: true,
            HasModLoaderVersionMismatch: analysisResult.HasModLoaderVersionMismatch));
        var exportPlan = MinecraftCrashExportWorkflowService.CreatePlan(exportRequest);

        return new CrashAvaloniaPlan(outputPrompt, exportPlan);
    }

    private static IReadOnlyList<string> BuildAnalysisSourcePaths(MinecraftCrashExportPlanRequest request)
    {
        return request.SourceFilePaths
            .Concat(request.AdditionalSourceFilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string BuildLaunchFailureMessage(FrontendLaunchStartResult startResult)
    {
        var details = new List<string>
        {
            T("shell.prompts.crash.launch_failure.exit_code", ("exit_code", startResult.Process.ExitCode))
        };

        var lastOutputLine = TryReadLastMeaningfulLine(startResult.RawOutputLogPath);
        if (!string.IsNullOrWhiteSpace(lastOutputLine))
        {
            details.Add(lastOutputLine);
        }

        details.Add(T("shell.prompts.crash.launch_failure.raw_output_log", ("path", startResult.RawOutputLogPath)));
        return string.Join(Environment.NewLine, details);
    }

    private static string? TryReadLastMeaningfulLine(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadLines(path)
                .Reverse()
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return null;
        }
    }

    private static SystemEnvironmentSnapshot GetHostEnvironmentSnapshot()
    {
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var totalPhysicalMemoryBytes = availableMemory > 0
            ? (ulong)availableMemory
            : 8UL * 1024UL * 1024UL * 1024UL;

        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            totalPhysicalMemoryBytes,
            GetHostCpuName(),
            []);
    }

    private static string GetHostCpuName()
    {
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
               Environment.GetEnvironmentVariable("HOSTTYPE") ??
               RuntimeInformation.ProcessArchitecture.ToString();
    }

    private void TriggerCrashPromptTest()
    {
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity(T("shell.prompts.activities.crash_test_triggered.title"), T("shell.prompts.activities.crash_test_triggered.body"));
    }
}

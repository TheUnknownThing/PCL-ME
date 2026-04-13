using System.Runtime.InteropServices;
using PCL.Core.Minecraft;
using PCL.Core.Utils.OS;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendCrashCompositionService
{
    public static CrashAvaloniaPlan Compose(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        return Compose(CreateRuntimeInputs(runtimePaths));
    }

    public static CrashAvaloniaPlan Compose(CrashAvaloniaInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

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

    public static CrashAvaloniaInputs CreateRuntimeInputs(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var minecraftRoot = GetMinecraftRootDirectory(homeDirectory);
        var logDirectory = Path.Combine(minecraftRoot, "logs");
        var crashReportDirectory = Path.Combine(minecraftRoot, "crash-reports");
        var launcherLogPath = runtimePaths.ResolveCurrentLauncherLogFilePath();
        var primaryFiles = new List<string>
        {
            Path.Combine(runtimePaths.LauncherAppDataDirectory, $"LatestLaunch{GetCommandScriptExtension()}"),
            Path.Combine(logDirectory, "RawOutput.log"),
            Path.Combine(logDirectory, "latest.log"),
            Path.Combine(logDirectory, "debug.log")
        };
        var additionalFiles = new List<string>();

        if (Directory.Exists(crashReportDirectory))
        {
            additionalFiles.AddRange(Directory.EnumerateFiles(crashReportDirectory));
        }

        if (Directory.Exists(minecraftRoot))
        {
            additionalFiles.AddRange(Directory.EnumerateFiles(minecraftRoot, "*.log"));
        }

        return new CrashAvaloniaInputs(
            new MinecraftCrashOutputPromptRequest(
                ResultText: "Minecraft 启动或运行过程中发生错误。",
                IsManualAnalysis: false,
                HasDirectFile: true,
                CanOpenModLoaderSettings: true),
            new MinecraftCrashExportPlanRequest(
                Timestamp: DateTime.Now,
                ReportDirectory: Path.Combine(
                    runtimePaths.FrontendTempDirectory,
                    "CrashReport",
                    DateTime.Now.ToString("yyyy-MM-dd")),
                LauncherVersionName: "frontend-avalonia",
                UniqueAddress: runtimePaths.LauncherAppDataDirectory,
                SourceFilePaths: primaryFiles,
                AdditionalSourceFilePaths: additionalFiles,
                CurrentLauncherLogFilePath: launcherLogPath,
                Environment: GetHostEnvironmentSnapshot(),
                CurrentAccessToken: null,
                CurrentUserUuid: null,
                UserProfilePath: homeDirectory));
    }

    private static IReadOnlyList<string> BuildAnalysisSourcePaths(MinecraftCrashExportPlanRequest request)
    {
        return request.SourceFilePaths
            .Concat(request.AdditionalSourceFilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetMinecraftRootDirectory(string homeDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        return Path.Combine(homeDirectory, ".minecraft");
    }

    private static string GetCommandScriptExtension()
    {
        return OperatingSystem.IsWindows()
            ? ".bat"
            : OperatingSystem.IsMacOS()
                ? ".command"
                : ".sh";
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
            GetCpuName(),
            []);
    }

    private static string GetCpuName()
    {
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
               Environment.GetEnvironmentVariable("HOSTTYPE") ??
               RuntimeInformation.ProcessArchitecture.ToString();
    }
}

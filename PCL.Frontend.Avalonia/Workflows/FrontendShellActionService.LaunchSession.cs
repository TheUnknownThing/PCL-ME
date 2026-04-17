using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendShellActionService
{
    public FrontendLaunchStartResult StartLaunchSession(
        FrontendLaunchComposition launchComposition,
        string? instanceDirectory,
        Action<string>? onStageChanged = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launchComposition);

        var speedLimiter = GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        cancellationToken.ThrowIfCancellationRequested();
        onStageChanged?.Invoke("Checking runtime dependencies");
        EnsureRequiredArtifacts(launchComposition.RequiredArtifacts, GetDownloadProvider(), speedLimiter, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        onStageChanged?.Invoke("Synchronizing local game libraries");
        var nativeSyncResult = EnsureNativeLibraries(launchComposition.NativeSyncRequest);
        EnsureNativePathAlias(launchComposition.NativePathAliasDirectory, launchComposition.NativesDirectory);

        var launcherDataDirectory = RuntimePaths.LauncherAppDataDirectory;
        var logDirectory = Path.Combine(launcherDataDirectory, "Log");
        Directory.CreateDirectory(launcherDataDirectory);
        Directory.CreateDirectory(logDirectory);

        cancellationToken.ThrowIfCancellationRequested();
        onStageChanged?.Invoke("Writing pre-launch configuration");
        ApplyPrerunPlan(launchComposition.PrerunPlan);

        var launchScriptPath = FrontendLauncherPathService.GetLatestLaunchScriptPath(RuntimePaths, PlatformAdapter);
        CleanupLegacyLaunchScripts(launcherDataDirectory, launchScriptPath);
        File.WriteAllText(
            launchScriptPath,
            launchComposition.SessionStartPlan.CustomCommandPlan.BatchScriptContent,
            launchComposition.SessionStartPlan.CustomCommandPlan.UseUtf8Encoding ? new UTF8Encoding(false) : Encoding.Default);
        EnsureFileExecutable(launchScriptPath);

        var sessionSummaryPath = GetUniqueFilePath(Path.Combine(logDirectory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log"));
        var sessionSummaryLines = BuildSessionSummaryLines(
            launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines,
            nativeSyncResult);
        File.WriteAllLines(
            sessionSummaryPath,
            sessionSummaryLines,
            new UTF8Encoding(false));

        var rawOutputLogPath = Path.Combine(logDirectory, "RawOutput.log");
        if (File.Exists(rawOutputLogPath))
        {
            File.Delete(rawOutputLogPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        onStageChanged?.Invoke("Running pre-launch commands");
        foreach (var shellPlan in launchComposition.SessionStartPlan.CustomCommandShellPlans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var customProcess = SystemProcessManager.Current.Start(
                MinecraftLaunchProcessExecutionService.BuildCustomCommandStartRequest(shellPlan))
                ?? throw new InvalidOperationException(shellPlan.FailureLogMessage);
            if (shellPlan.WaitForExit)
            {
                customProcess.WaitForExit();
                if (customProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{shellPlan.FailureLogMessage} (ExitCode: {customProcess.ExitCode})");
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        onStageChanged?.Invoke("Starting game process");
        var process = SystemProcessManager.Current.Start(
            MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(
                launchComposition.SessionStartPlan.ProcessShellPlan))
            ?? throw new InvalidOperationException("Failed to start the game process.");
        MinecraftLaunchProcessExecutionService.TryApplyPriority(
            process,
            launchComposition.SessionStartPlan.ProcessShellPlan.PriorityKind);

        ApplyPostLaunchShellPlanOnUiThread(launchComposition.PostLaunchShell);
        IncrementLaunchCounts(instanceDirectory, launchComposition.PostLaunchShell);

        return new FrontendLaunchStartResult(
            process,
            launchScriptPath,
            sessionSummaryPath,
            rawOutputLogPath);
    }

    private static IReadOnlyList<string> BuildSessionSummaryLines(
        IReadOnlyList<string> startupSummaryLines,
        MinecraftLaunchNativesSyncResult? nativeSyncResult)
    {
        if (nativeSyncResult is null || nativeSyncResult.LogMessages.Count == 0)
        {
            return startupSummaryLines;
        }

        return startupSummaryLines
            .Concat(["~ Natives Sync ~"])
            .Concat(nativeSyncResult.LogMessages)
            .ToArray();
    }

    private static MinecraftLaunchNativesSyncResult? EnsureNativeLibraries(MinecraftLaunchNativesSyncRequest? nativeSyncRequest)
    {
        if (nativeSyncRequest is null || nativeSyncRequest.NativeArchives.Count == 0)
        {
            return null;
        }

        try
        {
            return MinecraftLaunchNativesSyncService.Sync(nativeSyncRequest);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract game native libraries.", ex);
        }
    }

    private static void EnsureNativePathAlias(string? aliasDirectory, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(aliasDirectory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(aliasDirectory)!);

            if (File.Exists(aliasDirectory))
            {
                File.Delete(aliasDirectory);
            }
            else if (Directory.Exists(aliasDirectory))
            {
                Directory.Delete(aliasDirectory);
            }

            Directory.CreateSymbolicLink(aliasDirectory, targetDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to prepare an ASCII-compatible native library path.", ex);
        }
    }

    private static void EnsureRequiredArtifacts(
        IReadOnlyList<FrontendLaunchArtifactRequirement> requirements,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        foreach (var requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(requirement.TargetPath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(requirement.TargetPath)!);
            Exception? lastError = null;
            foreach (var url in downloadProvider.GetPreferredUrls(requirement.DownloadUrl))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    FrontendDownloadTransferService.DownloadToPath(
                        JavaRuntimeHttpClient,
                        url,
                        requirement.TargetPath,
                        speedLimiter: speedLimiter);
                    lastError = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
            {
                throw new InvalidOperationException(
                    $"A required launch file is missing and automatic download failed: {requirement.TargetPath}",
                    lastError);
            }
        }
    }

    public void ApplyWatcherStopShellPlan(FrontendLaunchComposition launchComposition)
    {
        ArgumentNullException.ThrowIfNull(launchComposition);

        var watcherStopPlan = MinecraftLaunchShellService.GetWatcherStopShellPlan(
            new MinecraftLaunchWatcherStopShellRequest(
                ReadSharedValue("LaunchArgumentVisible", LauncherVisibility.DoNothing),
                ReadLocalValue("UiMusicStop", false),
                ReadLocalValue("UiMusicStart", false),
                TriggerLauncherShutdown: false));
        ApplyLauncherShellAction(watcherStopPlan.LauncherAction);
    }

    public string GetCommandScriptExtension() => PlatformAdapter.GetCommandScriptExtension();

    public string GetLatestLaunchScriptPath() => FrontendLauncherPathService.GetLatestLaunchScriptPath(RuntimePaths, PlatformAdapter);

    public void EnsureFileExecutable(string path) => PlatformAdapter.EnsureFileExecutable(path);

    private void CleanupLegacyLaunchScripts(string launcherDataDirectory, string retainedPath)
    {
        foreach (var candidatePath in FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                     launcherDataDirectory,
                     PlatformAdapter))
        {
            if (PathsEqual(candidatePath, retainedPath) || !File.Exists(candidatePath))
            {
                continue;
            }

            TryDeleteFile(candidatePath);
        }
    }

    private void ApplyPrerunPlan(MinecraftLaunchPrerunWorkflowPlan prerunPlan)
    {
        if (prerunPlan.LauncherProfiles.ShouldEnsureFileExists &&
            !string.IsNullOrWhiteSpace(prerunPlan.LauncherProfiles.Path))
        {
            var launcherProfilesPath = prerunPlan.LauncherProfiles.Path!;
            Directory.CreateDirectory(Path.GetDirectoryName(launcherProfilesPath)!);

            try
            {
                File.WriteAllText(
                    launcherProfilesPath,
                    prerunPlan.LauncherProfiles.Workflow.InitialAttempt?.UpdatedProfilesJson ?? "{}",
                    new UTF8Encoding(false));
            }
            catch
            {
                File.WriteAllText(
                    launcherProfilesPath,
                    prerunPlan.LauncherProfiles.Workflow.RetryAttempt?.UpdatedProfilesJson ?? "{}",
                    new UTF8Encoding(false));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(prerunPlan.Options.TargetFilePath)!);
        ApplyOptionsWrites(prerunPlan.Options.TargetFilePath, prerunPlan.Options.SyncPlan.Writes);
    }

    private void IncrementLaunchCounts(string? instanceDirectory, MinecraftGameShellPlan shellPlan)
    {
        var currentLaunchCount = LauncherFrontendRuntimeStateService.ReadProtectedInt(
            RuntimePaths.SharedConfigDirectory,
            RuntimePaths.SharedConfigPath,
            "SystemLaunchCount");
        PersistProtectedSharedValue(
            "SystemLaunchCount",
            (currentLaunchCount + shellPlan.GlobalLaunchCountIncrement).ToString());

        if (FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory ?? string.Empty))
        {
            var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory!);
            var currentInstanceLaunchCount = provider.Exists("VersionLaunchCount")
                ? provider.Get<int>("VersionLaunchCount")
                : 0;
            provider.Set("VersionLaunchCount", currentInstanceLaunchCount + shellPlan.InstanceLaunchCountIncrement);
            provider.Sync();
        }
    }

    private static void ApplyOptionsWrites(string optionsPath, IReadOnlyList<MinecraftLaunchOptionWrite> writes)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(optionsPath))
        {
            foreach (var line in File.ReadAllLines(optionsPath))
            {
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                values[line[..separatorIndex]] = line[(separatorIndex + 1)..];
            }
        }

        foreach (var write in writes)
        {
            values[write.Key] = write.Value;
        }

        File.WriteAllLines(optionsPath, values.Select(pair => $"{pair.Key}:{pair.Value}"), new UTF8Encoding(false));
    }
}

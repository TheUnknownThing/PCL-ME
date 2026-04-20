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
        CancellationToken cancellationToken = default,
        bool ensureRequiredArtifacts = true)
    {
        ArgumentNullException.ThrowIfNull(launchComposition);

        cancellationToken.ThrowIfCancellationRequested();
        if (ensureRequiredArtifacts)
        {
            onStageChanged?.Invoke("Checking runtime dependencies");
            EnsureRequiredLaunchArtifacts(
                launchComposition.RequiredArtifacts,
                snapshot =>
                {
                    if (!string.IsNullOrWhiteSpace(snapshot.CurrentFileName))
                    {
                        onStageChanged?.Invoke($"Downloading {snapshot.CurrentFileName}");
                    }
                },
                cancellationToken);
        }

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

    public void EnsureRequiredLaunchArtifacts(
        IReadOnlyList<FrontendLaunchArtifactRequirement> requirements,
        Action<FrontendLaunchArtifactProgressSnapshot>? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var transferOptions = GetDownloadTransferOptions();
        var speedLimiter = transferOptions.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        EnsureRequiredArtifacts(
            requirements,
            GetDownloadProvider(),
            transferOptions,
            speedLimiter,
            progressReporter,
            cancellationToken);
    }

    private static void EnsureRequiredArtifacts(
        IReadOnlyList<FrontendLaunchArtifactRequirement> requirements,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadTransferOptions transferOptions,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        Action<FrontendLaunchArtifactProgressSnapshot>? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var missingRequirements = requirements
            .Where(requirement => !File.Exists(requirement.TargetPath))
            .ToArray();
        var completedCount = 0;
        progressReporter?.Invoke(new FrontendLaunchArtifactProgressSnapshot(0, missingRequirements.Length, string.Empty, 0L, 0d));

        foreach (var requirement in missingRequirements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(requirement.TargetPath)!);
            Exception? lastError = null;
            foreach (var url in downloadProvider.GetPreferredUrls(requirement.DownloadUrl))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tempPath = requirement.TargetPath + ".download";
                var lastReportedBytes = 0L;
                var lastReportedAt = Environment.TickCount64;
                try
                {
                    TryDeleteFile(tempPath);
                    FrontendDownloadTransferService.DownloadToPath(
                        JavaRuntimeHttpClient,
                        url,
                        tempPath,
                        transferredBytes =>
                        {
                            var now = Environment.TickCount64;
                            if (now - lastReportedAt < 250)
                            {
                                return;
                            }

                            var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                            var speed = (transferredBytes - lastReportedBytes) / elapsedSeconds;
                            lastReportedAt = now;
                            lastReportedBytes = transferredBytes;
                            progressReporter?.Invoke(new FrontendLaunchArtifactProgressSnapshot(
                                completedCount,
                                missingRequirements.Length,
                                Path.GetFileName(requirement.TargetPath),
                                transferredBytes,
                                speed));
                        },
                        speedLimiter: speedLimiter,
                        stalledTransferTimeout: transferOptions.StalledTransferTimeout,
                        maxAttempts: transferOptions.MaxFileDownloadAttempts,
                        cancelToken: cancellationToken);
                    if (!string.IsNullOrWhiteSpace(requirement.Sha1))
                    {
                        var actualSha1 = ComputeSha1FromFile(tempPath);
                        if (!string.Equals(actualSha1, requirement.Sha1, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"Launch file verification failed: {Path.GetFileName(requirement.TargetPath)}");
                        }
                    }

                    File.Move(tempPath, requirement.TargetPath, overwrite: true);
                    completedCount++;
                    progressReporter?.Invoke(new FrontendLaunchArtifactProgressSnapshot(
                        completedCount,
                        missingRequirements.Length,
                        Path.GetFileName(requirement.TargetPath),
                        0L,
                        0d));
                    lastError = null;
                    break;
                }
                catch (OperationCanceledException)
                {
                    TryDeleteFile(tempPath);
                    throw;
                }
                catch (Exception ex)
                {
                    TryDeleteFile(tempPath);
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
                TriggerLauncherShutdown: false));
        ApplyLauncherShellAction(watcherStopPlan.LauncherAction);
    }

    public string GetCommandScriptExtension() => PlatformAdapter.GetCommandScriptExtension();

    public string GetLatestLaunchScriptPath() => FrontendLauncherPathService.GetLatestLaunchScriptPath(RuntimePaths, PlatformAdapter);

    public void EnsureFileExecutable(string path) => PlatformAdapter.EnsureFileExecutable(path);

    public sealed record FrontendLaunchArtifactProgressSnapshot(
        int CompletedFileCount,
        int TotalFileCount,
        string CurrentFileName,
        long CurrentFileBytes,
        double SpeedBytesPerSecond);

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

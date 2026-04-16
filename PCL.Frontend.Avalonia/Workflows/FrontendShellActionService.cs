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

internal sealed class FrontendShellActionService(
    FrontendRuntimePaths runtimePaths,
    FrontendPlatformAdapter platformAdapter,
    Action exitLauncher,
    II18nService i18nService)
{
    private static readonly HttpClient JavaRuntimeHttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));

    public FrontendRuntimePaths RuntimePaths { get; } = runtimePaths;

    public FrontendPlatformAdapter PlatformAdapter { get; } = platformAdapter;

    private II18nService I18n { get; } = i18nService;

    public Func<string, string, string, bool, Task<bool>>? ConfirmPresenter { get; set; }

    public Func<string, string, string, string, string?, bool, Task<string?>>? TextInputPresenter { get; set; }

    public Func<string, string, IReadOnlyList<PclChoiceDialogOption>, string?, string, Task<string?>>? ChoicePresenter { get; set; }

    public static void ApplyStoredAnimationPreferences(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var animationFpsLimit = 59;
        var debugAnimationSpeed = 9d;
        if (File.Exists(runtimePaths.SharedConfigPath))
        {
            var provider = runtimePaths.OpenSharedConfigProvider();
            if (provider.Exists("UiAniFPS"))
            {
                animationFpsLimit = provider.Get<int>("UiAniFPS");
            }

            if (provider.Exists("SystemDebugAnim"))
            {
                debugAnimationSpeed = provider.Get<int>("SystemDebugAnim");
            }
        }

        ApplyAnimationPreferences(animationFpsLimit, debugAnimationSpeed);
    }

    public static void ApplyAnimationPreferences(int animationFpsLimit, double debugAnimationSpeed)
    {
        MotionDurations.ApplyRuntimePreferences(animationFpsLimit, debugAnimationSpeed);
    }

    public void ApplyAppearance(
        int darkModeIndex,
        int lightColorIndex,
        int darkColorIndex,
        string? lightCustomColorHex,
        string? darkCustomColorHex,
        string? globalFontConfigValue,
        string? motdFontConfigValue)
    {
        FrontendAppearanceService.ApplyAppearance(
            Application.Current,
            new FrontendAppearanceSelection(
                darkModeIndex,
                lightColorIndex,
                darkColorIndex,
                lightCustomColorHex,
                darkCustomColorHex,
                globalFontConfigValue,
                motdFontConfigValue));
    }

    public void AcceptLauncherEula()
    {
        PersistSharedValue("SystemEula", true);
    }

    public void PersistLocalValue<T>(string key, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = RuntimePaths.OpenLocalConfigProvider();
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistProtectedSharedValue(string key, string value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        provider.Set(key, ProtectSharedValue(value));
        provider.Sync();
    }

    public void RemoveLocalValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = RuntimePaths.OpenLocalConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void RemoveSharedValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = RuntimePaths.OpenSharedConfigProvider();
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void PersistInstanceValue<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        provider.Set(key, value);
        provider.Sync();
    }

    public void RemoveInstanceValues(string instanceDirectory, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void DisableNonAsciiGamePathWarning()
    {
        PersistSharedValue("HintDisableGamePathCheckTip", true);
    }

    public void ExitLauncher()
    {
        exitLauncher();
    }

    public FrontendInstanceRepairResult RepairInstance(
        FrontendInstanceRepairRequest request,
        Action<FrontendInstanceRepairProgressSnapshot>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        return FrontendInstanceRepairService.Repair(
            request,
            onProgress,
            GetDownloadProvider(),
            GetDownloadTransferOptions(),
            cancellationToken);
    }

    public FrontendDownloadTransferOptions GetDownloadTransferOptions()
    {
        return FrontendDownloadSettingsService.ResolveTransferOptions(RuntimePaths);
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        return PlatformAdapter.TryOpenExternalTarget(target, out error);
    }

    public bool TryRevealExternalTarget(string target, out string? error)
    {
        return PlatformAdapter.TryRevealExternalTarget(target, out error);
    }

    public bool TryStartDetachedScript(string scriptPath, out string? error)
    {
        return PlatformAdapter.TryStartDetachedScript(scriptPath, out error);
    }

    public async Task<string?> PickOpenFileAsync(string title, string typeName, params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support file pickers.");
        var fileTypes = patterns.Length == 0
            ? null
            : new List<FilePickerFileType>
            {
                new(typeName)
                {
                    Patterns = patterns
                }
            };

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support folder pickers.");
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(
        string title,
        string suggestedFileName,
        string typeName,
        string? suggestedStartFolder = null,
        params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "The current environment does not support file pickers.");
        var fileTypes = patterns.Length == 0
            ? null
            : new List<FilePickerFileType>
            {
                new(typeName)
                {
                    Patterns = patterns
                }
            };
        var startLocation = string.IsNullOrWhiteSpace(suggestedStartFolder)
            ? null
            : await storageProvider.TryGetFolderFromPathAsync(suggestedStartFolder);

        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(suggestedFileName),
            FileTypeChoices = fileTypes,
            SuggestedStartLocation = startLocation,
            ShowOverwritePrompt = true
        });
        return result?.TryGetLocalPath();
    }

    public async Task<string?> ReadClipboardTextAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("The current environment does not support the clipboard.");
        }

        return await desktop.MainWindow.Clipboard.TryGetTextAsync();
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("The current environment does not support the clipboard.");
        }

        await desktop.MainWindow.Clipboard.SetTextAsync(text ?? string.Empty);
    }

    public async Task<string?> PromptForTextAsync(
        string title,
        string message,
        string initialText = "",
        string confirmText = "Confirm",
        string? placeholderText = null,
        bool isPassword = false)
    {
        if (TextInputPresenter is not null)
        {
            return await TextInputPresenter(title, message, initialText, confirmText, placeholderText, isPassword);
        }

        throw new InvalidOperationException("Text input dialogs require an in-app presenter.");
    }

    public async Task<string?> PromptForChoiceAsync(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId = null,
        string confirmText = "Confirm")
    {
        if (options.Count == 0)
        {
            return null;
        }

        if (ChoicePresenter is not null)
        {
            return await ChoicePresenter(title, message, options, selectedId, confirmText);
        }

        throw new InvalidOperationException("Choice dialogs require an in-app presenter.");
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        bool isDanger = false)
    {
        if (ConfirmPresenter is not null)
        {
            return await ConfirmPresenter(title, message, confirmText, isDanger);
        }

        throw new InvalidOperationException("Confirm dialogs require an in-app presenter.");
    }

    public FrontendCrashExportResult ExportCrashReport(CrashAvaloniaPlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        Directory.CreateDirectory(RuntimePaths.FrontendArtifactDirectory);
        Directory.CreateDirectory(RuntimePaths.FrontendTempDirectory);

        var archivePath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-exports",
            crashPlan.ExportPlan.SuggestedArchiveName));
        var tempRoot = Path.Combine(RuntimePaths.FrontendTempDirectory, "crash-export", Guid.NewGuid().ToString("N"));

        try
        {
            var exportRequest = MaterializeCrashExportRequest(crashPlan.ExportPlan.ExportRequest, tempRoot);
            var archiveResult = MinecraftCrashExportArchiveService.CreateArchive(new MinecraftCrashExportArchiveRequest(
                archivePath,
                exportRequest));
            return new FrontendCrashExportResult(
                archiveResult.ArchiveFilePath,
                archiveResult.ArchivedFileNames.Count);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public string MaterializeCrashLog(CrashAvaloniaPlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        var outputPath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-logs",
            "game-output.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var exportRequest = crashPlan.ExportPlan.ExportRequest;
        var builder = new StringBuilder()
            .AppendLine(I18n.T(crashPlan.OutputPrompt.Title))
            .AppendLine()
            .AppendLine(crashPlan.OutputPrompt.Message)
            .AppendLine()
            .AppendLine(I18n.T("crash.export.heading"))
            .AppendLine(I18n.T(
                "crash.export.suggested_archive",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["archive_name"] = crashPlan.ExportPlan.SuggestedArchiveName
                }))
            .AppendLine(I18n.T(
                "crash.export.source_file_count",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["file_count"] = exportRequest.SourceFiles.Count
                }));

        foreach (var sourceFile in exportRequest.SourceFiles)
        {
            builder.AppendLine($"- {sourceFile.SourcePath}");
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
        return outputPath;
    }

    public FrontendJavaRuntimeInstallResult MaterializeJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installPlan);

        return installPlan.Kind switch
        {
            FrontendJavaRuntimeInstallPlanKind.MojangManifest => MaterializeMojangJavaRuntime(
                installPlan,
                onProgress,
                cancellationToken),
            FrontendJavaRuntimeInstallPlanKind.ArchivePackage => MaterializeArchiveJavaRuntime(
                installPlan,
                onProgress,
                cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported Java download plan: {installPlan.Kind}")
        };
    }

    private FrontendJavaRuntimeInstallResult MaterializeMojangJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress,
        CancellationToken cancellationToken)
    {
        var manifestPlan = installPlan.MojangManifestPlan
                           ?? throw new InvalidOperationException("The Mojang Java download plan is missing a manifest.");
        var transferPlan = installPlan.MojangTransferPlan
                           ?? throw new InvalidOperationException("The Mojang Java download plan is missing a transfer plan.");
        var effectiveTransferPlan = ResolveEffectiveJavaTransferPlan(manifestPlan, transferPlan);
        var runtimeDirectory = effectiveTransferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory;
        var downloadProvider = GetDownloadProvider();
        var speedLimiter = GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;

        Directory.CreateDirectory(runtimeDirectory);

        var totalFileCount = effectiveTransferPlan.FilesToDownload.Count;
        var completedFileCount = 0;
        var downloadedBytes = 0L;
        onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
            string.Empty,
            completedFileCount,
            totalFileCount,
            downloadedBytes,
            effectiveTransferPlan.DownloadBytes,
            0d));
        foreach (var file in effectiveTransferPlan.FilesToDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lastReportedBytes = 0L;
            var lastReportedAt = Environment.TickCount64;
            DownloadJavaRuntimeFile(
                file,
                downloadProvider,
                speedLimiter,
                transferredBytes =>
                {
                    var now = Environment.TickCount64;
                    if (now - lastReportedAt < 250)
                    {
                        return;
                    }

                    var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                    var speed = (transferredBytes - lastReportedBytes) / elapsedSeconds;
                    lastReportedBytes = transferredBytes;
                    lastReportedAt = now;
                    onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                        file.RelativePath,
                        completedFileCount,
                        totalFileCount,
                        downloadedBytes + transferredBytes,
                        effectiveTransferPlan.DownloadBytes,
                        speed));
                },
                cancellationToken);
            downloadedBytes += file.Size;
            completedFileCount++;
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                file.RelativePath,
                completedFileCount,
                totalFileCount,
                downloadedBytes,
                effectiveTransferPlan.DownloadBytes,
                0d));
        }

        EnsureUnixExecutableBits(runtimeDirectory, effectiveTransferPlan);
        var summaryPath = WriteJavaRuntimeSummary(
            installPlan,
            runtimeDirectory,
            effectiveTransferPlan.FilesToDownload.Count,
            effectiveTransferPlan.ReusedFiles.Count,
            effectiveTransferPlan.DownloadBytes);
        return new FrontendJavaRuntimeInstallResult(
            installPlan.VersionName,
            runtimeDirectory,
            effectiveTransferPlan.FilesToDownload.Count,
            effectiveTransferPlan.ReusedFiles.Count,
            summaryPath);
    }

    private FrontendJavaRuntimeInstallResult MaterializeArchiveJavaRuntime(
        FrontendJavaRuntimeInstallPlan installPlan,
        Action<FrontendJavaRuntimeInstallProgressSnapshot>? onProgress,
        CancellationToken cancellationToken)
    {
        var archivePlan = installPlan.ArchivePlan
                          ?? throw new InvalidOperationException("The archive Java download plan is missing archive metadata.");
        var runtimeDirectory = installPlan.RuntimeDirectory;
        var downloadProvider = GetDownloadProvider();
        var speedLimiter = GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        var stagingDirectory = runtimeDirectory + ".staging";
        var extractDirectory = Path.Combine(stagingDirectory, "extract");
        var archivePath = Path.Combine(stagingDirectory, archivePlan.PackageName);

        try
        {
            TryDeleteDirectory(stagingDirectory);
            Directory.CreateDirectory(stagingDirectory);
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                archivePlan.PackageName,
                0,
                1,
                0L,
                archivePlan.Size,
                0d));

            var lastReportedBytes = 0L;
            var lastReportedAt = Environment.TickCount64;
            DownloadJavaRuntimeArchivePackage(
                archivePlan,
                archivePath,
                downloadProvider,
                speedLimiter,
                transferredBytes =>
                {
                    var now = Environment.TickCount64;
                    if (now - lastReportedAt < 250)
                    {
                        return;
                    }

                    var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                    var speed = (transferredBytes - lastReportedBytes) / elapsedSeconds;
                    lastReportedBytes = transferredBytes;
                    lastReportedAt = now;
                    onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                        archivePlan.PackageName,
                        0,
                        1,
                        transferredBytes,
                        archivePlan.Size,
                        speed));
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                "Extracting Java runtime",
                0,
                1,
                archivePlan.Size,
                archivePlan.Size,
                0d));

            ExtractJavaRuntimeArchive(archivePath, extractDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            var extractedRoot = ResolveExtractedJavaRuntimeRoot(extractDirectory)
                                ?? throw new InvalidOperationException("No usable Java runtime directory was found in the archive.");

            TryDeleteDirectory(runtimeDirectory);
            Directory.CreateDirectory(runtimeDirectory);
            MoveDirectoryContents(extractedRoot, runtimeDirectory);
            TryDeleteDirectory(stagingDirectory);
            EnsureUnixExecutableBits(runtimeDirectory);

            onProgress?.Invoke(new FrontendJavaRuntimeInstallProgressSnapshot(
                archivePlan.PackageName,
                1,
                1,
                archivePlan.Size,
                archivePlan.Size,
                0d));

            var summaryPath = WriteJavaRuntimeSummary(
                installPlan,
                runtimeDirectory,
                downloadedFileCount: 1,
                reusedFileCount: 0,
                totalBytes: archivePlan.Size);
            return new FrontendJavaRuntimeInstallResult(
                installPlan.VersionName,
                runtimeDirectory,
                1,
                0,
                summaryPath);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

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

    public string GetJavaExecutablePath(string runtimeDirectory) => PlatformAdapter.GetJavaExecutablePath(runtimeDirectory);

    public IReadOnlyList<string> GetDefaultJavaDetectionCandidates() => PlatformAdapter.GetDefaultJavaDetectionCandidates();

    public void EnsureFileExecutable(string path) => PlatformAdapter.EnsureFileExecutable(path);

    public string CreateLauncherShortcut(string displayName)
    {
        var desktopDirectory = PlatformAdapter.TryGetDesktopDirectory()
            ?? throw new InvalidOperationException("The current system did not provide a desktop directory.");
        return CreateLauncherShortcutAt(desktopDirectory, displayName);
    }

    public string CreateLauncherShortcutAt(string targetDirectory, string displayName)
    {
        var executablePath = Environment.ProcessPath ?? Path.Combine(RuntimePaths.ExecutableDirectory, "PCL.Frontend.Avalonia");
        return PlatformAdapter.CreateLauncherShortcut(targetDirectory, executablePath, displayName).ShortcutPath;
    }

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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary frontend artifacts.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for stale launch scripts.
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(left, right, comparison);
    }

    private MinecraftJavaRuntimeDownloadTransferPlan ResolveEffectiveJavaTransferPlan(
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        var hasPlaceholderUrls = transferPlan.FilesToDownload.Any(file =>
            file.RequestUrls.AllUrls.Any(url => url.Contains("example.invalid", StringComparison.OrdinalIgnoreCase))) ||
                                 manifestPlan.RequestUrls.AllUrls.Any(url => url.Contains("example.invalid", StringComparison.OrdinalIgnoreCase));
        if (!hasPlaceholderUrls)
        {
            return transferPlan;
        }

        var downloadProvider = GetDownloadProvider();
        var indexJson = DownloadUtf8String(downloadProvider.GetPreferredUrls(
            MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().OfficialUrls,
            MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().MirrorUrls));
        var liveManifestPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            new MinecraftJavaRuntimeManifestRequestPlanRequest(
                indexJson,
                manifestPlan.Selection.PlatformKey,
                manifestPlan.Selection.RequestedComponent,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        var manifestJson = DownloadUtf8String(downloadProvider.GetPreferredUrls(
            liveManifestPlan.RequestUrls.OfficialUrls,
            liveManifestPlan.RequestUrls.MirrorUrls));
        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestJson,
                transferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory,
                MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes(),
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
        var existingRelativePaths = workflowPlan.Files
            .Where(file => File.Exists(file.TargetPath))
            .Select(file => file.RelativePath)
            .ToArray();

        return MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
            new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                workflowPlan,
                existingRelativePaths));
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

    private void ApplyPostLaunchShellPlan(MinecraftGameShellPlan shellPlan)
    {
        ApplyLauncherShellAction(shellPlan.LauncherAction);
    }

    private void ApplyPostLaunchShellPlanOnUiThread(MinecraftGameShellPlan shellPlan)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPostLaunchShellPlan(shellPlan);
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() => ApplyPostLaunchShellPlan(shellPlan)).GetAwaiter().GetResult();
    }

    private void ApplyLauncherShellAction(MinecraftLaunchShellAction action)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        switch (action.Kind)
        {
            case MinecraftLaunchShellActionKind.ExitLauncher:
                exitLauncher();
                break;
            case MinecraftLaunchShellActionKind.HideLauncher:
                desktop.MainWindow.Hide();
                break;
            case MinecraftLaunchShellActionKind.MinimizeLauncher:
                desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Minimized;
                break;
            case MinecraftLaunchShellActionKind.ShowLauncher:
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Normal;
                desktop.MainWindow.Activate();
                break;
        }
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

        if (!string.IsNullOrWhiteSpace(instanceDirectory))
        {
            var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
            var currentInstanceLaunchCount = provider.Exists("VersionLaunchCount")
                ? provider.Get<int>("VersionLaunchCount")
                : 0;
            provider.Set("VersionLaunchCount", currentInstanceLaunchCount + shellPlan.InstanceLaunchCountIncrement);
            provider.Sync();
        }
    }

    private T ReadLocalValue<T>(string key, T fallback)
    {
        var provider = RuntimePaths.OpenLocalConfigProvider();
        return provider.Exists(key)
            ? provider.Get<T>(key)
            : fallback;
    }

    private T ReadSharedValue<T>(string key, T fallback)
    {
        var provider = RuntimePaths.OpenSharedConfigProvider();
        return provider.Exists(key)
            ? provider.Get<T>(key)
            : fallback;
    }

    private static IStorageProvider? TryGetStorageProvider(out string? error)
    {
        error = null;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.StorageProvider is null)
        {
            error = "The current environment does not support file selection.";
            return null;
        }

        return desktop.MainWindow.StorageProvider;
    }

    private static global::Avalonia.Controls.Window GetDesktopMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            throw new InvalidOperationException("The current environment did not provide a main window.");
        }

        return desktop.MainWindow;
    }

    private static string GetUniqueDirectoryPath(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return directoryPath;
        }

        var extension = 1;
        while (true)
        {
            var candidate = $"{directoryPath}-{extension}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }

            extension++;
        }
    }

    private static string GetUniqueFilePath(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        var suffix = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static MinecraftCrashExportRequest MaterializeCrashExportRequest(
        MinecraftCrashExportRequest request,
        string tempRoot)
    {
        var sourceRoot = Path.Combine(tempRoot, "source");
        var reportRoot = Path.Combine(tempRoot, "report");
        Directory.CreateDirectory(sourceRoot);

        var sourceFiles = request.SourceFiles
            .Select(file => new MinecraftCrashExportFile(MaterializeCrashSourceFile(file.SourcePath, sourceRoot)))
            .ToArray();

        return request with
        {
            ReportDirectory = reportRoot,
            SourceFiles = sourceFiles
        };
    }

    private static string MaterializeCrashSourceFile(string sourcePath, string sourceRoot)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var fileName = string.IsNullOrWhiteSpace(sourcePath)
            ? "missing-log.txt"
            : Path.GetFileName(sourcePath);
        var fallbackPath = Path.Combine(sourceRoot, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
        File.WriteAllText(
            fallbackPath,
            $"Placeholder crash artifact generated for missing source file: {sourcePath}",
            new UTF8Encoding(false));
        return fallbackPath;
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

    private static string DownloadUtf8String(IReadOnlyList<string> urls)
    {
        Exception? lastError = null;
        foreach (var url in urls)
        {
            try
            {
                return JavaRuntimeHttpClient.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException("Unable to download Java runtime metadata.", lastError);
    }

    private static void DownloadJavaRuntimeFile(
        MinecraftJavaRuntimeDownloadRequestFilePlan file,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        Action<long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);

        Exception? lastError = null;
        foreach (var url in downloadProvider.GetPreferredUrls(file.RequestUrls.OfficialUrls, file.RequestUrls.MirrorUrls))
        {
            try
            {
                var tempPath = file.TargetPath + ".download";
                FrontendDownloadTransferService.DownloadToPath(
                    JavaRuntimeHttpClient,
                    url,
                    tempPath,
                    onProgress,
                    speedLimiter: speedLimiter,
                    cancelToken: cancellationToken);
                var sha1 = ComputeSha1FromFile(tempPath);
                if (!string.Equals(sha1, file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(tempPath);
                    throw new InvalidOperationException($"Java file verification failed: {file.RelativePath}");
                }

                File.Move(tempPath, file.TargetPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(file.TargetPath + ".download");
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(file.TargetPath + ".download");
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download Java file: {file.RelativePath}", lastError);
    }

    private static void DownloadJavaRuntimeArchivePackage(
        FrontendJavaRuntimeArchiveDownloadPlan archivePlan,
        string targetPath,
        FrontendDownloadProvider downloadProvider,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        Action<long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        Exception? lastError = null;
        foreach (var url in downloadProvider.GetPreferredUrls(archivePlan.RequestUrls.OfficialUrls, archivePlan.RequestUrls.MirrorUrls))
        {
            try
            {
                var tempPath = targetPath + ".download";
                FrontendDownloadTransferService.DownloadToPath(
                    JavaRuntimeHttpClient,
                    url,
                    tempPath,
                    onProgress,
                    speedLimiter: speedLimiter,
                    cancelToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(archivePlan.Sha256))
                {
                    var sha256 = ComputeSha256FromFile(tempPath);
                    if (!string.Equals(sha256, archivePlan.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteFile(tempPath);
                        throw new InvalidOperationException($"Java archive validation failed: {archivePlan.PackageName}");
                    }
                }

                File.Move(tempPath, targetPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(targetPath + ".download");
                throw;
            }
            catch (Exception ex)
            {
                TryDeleteFile(targetPath + ".download");
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download Java archive: {archivePlan.PackageName}", lastError);
    }

    private static string ComputeSha1FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeSha256FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ExtractJavaRuntimeArchive(string archivePath, string extractDirectory)
    {
        TryDeleteDirectory(extractDirectory);
        Directory.CreateDirectory(extractDirectory);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDirectory, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            using var archiveStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzipStream, extractDirectory, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException($"Unsupported Java archive format: {Path.GetFileName(archivePath)}");
    }

    private string? ResolveExtractedJavaRuntimeRoot(string extractDirectory)
    {
        if (HasJavaExecutable(extractDirectory))
        {
            return extractDirectory;
        }

        var childDirectories = Directory.Exists(extractDirectory)
            ? Directory.EnumerateDirectories(extractDirectory).ToArray()
            : [];
        if (childDirectories.Length == 1 && HasJavaExecutable(childDirectories[0]))
        {
            return childDirectories[0];
        }

        return childDirectories.FirstOrDefault(HasJavaExecutable);
    }

    private bool HasJavaExecutable(string runtimeDirectory)
    {
        return File.Exists(GetJavaExecutablePath(runtimeDirectory));
    }

    private static void MoveDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(directory));
            Directory.Move(directory, targetPath);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Move(file, targetPath, overwrite: true);
        }
    }

    private static void EnsureUnixExecutableBits(
        string runtimeDirectory,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var file in transferPlan.WorkflowPlan.Files)
        {
            var relativePath = file.RelativePath.Replace('\\', '/');
            if (!relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!File.Exists(file.TargetPath))
            {
                continue;
            }

            File.SetUnixFileMode(
                file.TargetPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }
    }

    private void EnsureUnixExecutableBits(string runtimeDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var executablePath = GetJavaExecutablePath(runtimeDirectory);
        if (!File.Exists(executablePath))
        {
            return;
        }

        File.SetUnixFileMode(
            executablePath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
    }

    private static string WriteJavaRuntimeSummary(
        FrontendJavaRuntimeInstallPlan installPlan,
        string runtimeDirectory,
        int downloadedFileCount,
        int reusedFileCount,
        long totalBytes)
    {
        var summaryPath = Path.Combine(runtimeDirectory, "download-summary.txt");
        var summary = $"""
            Java runtime: {installPlan.VersionName}
            Source: {installPlan.SourceName}
            Requested component: {installPlan.RequestedComponent}
            Platform: {installPlan.PlatformKey}
            Runtime directory: {runtimeDirectory}
            Download files: {downloadedFileCount}
            Reused files: {reusedFileCount}
            Total bytes planned: {totalBytes}
            """;
        File.WriteAllText(summaryPath, summary, new UTF8Encoding(false));
        return summaryPath;
    }

    private static void WriteJavaRuntimeFile(string runtimeDirectory, string relativePath, string content)
    {
        var segments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var filePath = Path.Combine([runtimeDirectory, ..segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }

    private static string SanitizePathSegment(string raw)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "artifact" : cleaned;
    }

    private string ProtectSharedValue(string plainText)
    {
        var encryptionKey = ResolveSharedEncryptionKey();
        return LauncherDataProtectionService.Protect(plainText, encryptionKey);
    }

    private FrontendDownloadProvider GetDownloadProvider()
    {
        return FrontendDownloadProvider.FromPreference(ReadSharedValue("ToolDownloadSource", 1));
    }

    private byte[] ResolveSharedEncryptionKey()
    {
        return LauncherSharedEncryptionKeyService.ResolveOrCreate(
            RuntimePaths.SharedConfigDirectory,
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
    }
}

internal sealed record FrontendCrashExportResult(
    string ArchivePath,
    int ArchivedFileCount);

internal sealed record FrontendJavaRuntimeInstallResult(
    string VersionName,
    string RuntimeDirectory,
    int DownloadedFileCount,
    int ReusedFileCount,
    string SummaryPath);

internal sealed record FrontendJavaRuntimeInstallProgressSnapshot(
    string CurrentFileRelativePath,
    int CompletedFileCount,
    int TotalFileCount,
    long DownloadedBytes,
    long TotalDownloadBytes,
    double SpeedBytesPerSecond);

internal sealed record FrontendLaunchStartResult(
    Process Process,
    string LaunchScriptPath,
    string SessionSummaryPath,
    string RawOutputLogPath);

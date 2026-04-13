using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.Processes;
using PCL.Frontend.Spike.Desktop.Dialogs;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal sealed class FrontendShellActionService(
    FrontendRuntimePaths runtimePaths,
    FrontendPlatformAdapter platformAdapter,
    Action exitLauncher)
{
    private static readonly HttpClient JavaRuntimeHttpClient = new();

    public FrontendRuntimePaths RuntimePaths { get; } = runtimePaths;

    public FrontendPlatformAdapter PlatformAdapter { get; } = platformAdapter;

    public void AcceptLauncherEula()
    {
        PersistSharedValue("SystemEula", true);
    }

    public void SetTelemetryEnabled(bool enabled)
    {
        PersistSharedValue("SystemTelemetry", enabled);
    }

    public void PersistLocalValue<T>(string key, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = new YamlFileProvider(RuntimePaths.LocalConfigPath);
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistSharedValue<T>(string key, T value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        provider.Set(key, value);
        provider.Sync();
    }

    public void PersistProtectedSharedValue(string key, string value)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        provider.Set(key, ProtectSharedValue(value));
        provider.Sync();
    }

    public void RemoveLocalValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimePaths.LocalConfigPath)!);
        var provider = new YamlFileProvider(RuntimePaths.LocalConfigPath);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void RemoveSharedValues(IEnumerable<string> keys)
    {
        Directory.CreateDirectory(RuntimePaths.SharedConfigDirectory);
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
        foreach (var key in keys)
        {
            provider.Remove(key);
        }

        provider.Sync();
    }

    public void PersistInstanceValue<T>(string instanceDirectory, string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = OpenInstanceConfigProvider(instanceDirectory);
        provider.Set(key, value);
        provider.Sync();
    }

    public void RemoveInstanceValues(string instanceDirectory, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        var provider = OpenInstanceConfigProvider(instanceDirectory);
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

    public FrontendInstanceRepairResult RepairInstance(FrontendInstanceRepairRequest request)
    {
        return FrontendInstanceRepairService.Repair(request);
    }

    public bool TryOpenExternalTarget(string target, out string? error)
    {
        return PlatformAdapter.TryOpenExternalTarget(target, out error);
    }

    public async Task<string?> PickOpenFileAsync(string title, string typeName, params string[] patterns)
    {
        var storageProvider = TryGetStorageProvider(out var error)
            ?? throw new InvalidOperationException(error ?? "当前环境不支持文件选择器。");
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
            ?? throw new InvalidOperationException(error ?? "当前环境不支持文件夹选择器。");
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }

    public async Task<string?> ReadClipboardTextAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("当前壳层未提供剪贴板。");
        }

        return await desktop.MainWindow.Clipboard.GetTextAsync();
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("当前壳层未提供剪贴板。");
        }

        await desktop.MainWindow.Clipboard.SetTextAsync(text ?? string.Empty);
    }

    public async Task<string?> PromptForTextAsync(
        string title,
        string message,
        string initialText = "",
        string confirmText = "确定",
        string? placeholderText = null)
    {
        var owner = GetDesktopMainWindow();
        var dialog = new PclTextInputDialog(title, message, initialText, confirmText, placeholderText);
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<string?> PromptForChoiceAsync(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId = null,
        string confirmText = "确定")
    {
        if (options.Count == 0)
        {
            return null;
        }

        var owner = GetDesktopMainWindow();
        var dialog = new PclChoiceDialog(title, message, options, selectedId, confirmText);
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "确定",
        bool isDanger = false)
    {
        var owner = GetDesktopMainWindow();
        var dialog = new PclConfirmDialog(title, message, confirmText, isDanger);
        return await dialog.ShowDialog<bool>(owner);
    }

    public FrontendCrashExportResult ExportCrashReport(CrashSpikePlan crashPlan)
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

    public string MaterializeCrashLog(CrashSpikePlan crashPlan)
    {
        ArgumentNullException.ThrowIfNull(crashPlan);

        var outputPath = GetUniqueFilePath(Path.Combine(
            RuntimePaths.FrontendArtifactDirectory,
            "crash-logs",
            "game-output.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var exportRequest = crashPlan.ExportPlan.ExportRequest;
        var builder = new StringBuilder()
            .AppendLine(crashPlan.OutputPrompt.Title)
            .AppendLine()
            .AppendLine(crashPlan.OutputPrompt.Message)
            .AppendLine()
            .AppendLine("导出计划")
            .AppendLine($"- 建议压缩包: {crashPlan.ExportPlan.SuggestedArchiveName}")
            .AppendLine($"- 源文件数量: {exportRequest.SourceFiles.Count}");

        foreach (var sourceFile in exportRequest.SourceFiles)
        {
            builder.AppendLine($"- {sourceFile.SourcePath}");
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
        return outputPath;
    }

    public FrontendJavaRuntimeInstallResult MaterializeJavaRuntime(
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        ArgumentNullException.ThrowIfNull(manifestPlan);
        ArgumentNullException.ThrowIfNull(transferPlan);

        var effectiveTransferPlan = ResolveEffectiveJavaTransferPlan(manifestPlan, transferPlan);
        var runtimeDirectory = effectiveTransferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory;
        var summaryPath = Path.Combine(runtimeDirectory, "download-summary.txt");

        Directory.CreateDirectory(runtimeDirectory);

        foreach (var file in effectiveTransferPlan.FilesToDownload)
        {
            DownloadJavaRuntimeFile(file);
        }
        EnsureUnixExecutableBits(runtimeDirectory, effectiveTransferPlan);

        var summary = $"""
            Java runtime: {manifestPlan.Selection.VersionName}
            Component: {manifestPlan.Selection.ComponentKey}
            Runtime directory: {runtimeDirectory}
            Download files: {effectiveTransferPlan.FilesToDownload.Count}
            Reused files: {effectiveTransferPlan.ReusedFiles.Count}
            Total bytes planned: {effectiveTransferPlan.DownloadBytes}
            """;
        File.WriteAllText(summaryPath, summary, new UTF8Encoding(false));

        return new FrontendJavaRuntimeInstallResult(
            manifestPlan.Selection.VersionName,
            runtimeDirectory,
            effectiveTransferPlan.FilesToDownload.Count,
            effectiveTransferPlan.ReusedFiles.Count,
            summaryPath);
    }

    public FrontendLaunchStartResult StartLaunchSession(
        FrontendLaunchComposition launchComposition,
        string? instanceDirectory)
    {
        ArgumentNullException.ThrowIfNull(launchComposition);

        var launcherDataDirectory = RuntimePaths.LauncherAppDataDirectory;
        var logDirectory = Path.Combine(launcherDataDirectory, "Log");
        Directory.CreateDirectory(launcherDataDirectory);
        Directory.CreateDirectory(logDirectory);

        ApplyPrerunPlan(launchComposition.PrerunPlan);

        var launchScriptPath = Path.Combine(launcherDataDirectory, "LatestLaunch.bat");
        File.WriteAllText(
            launchScriptPath,
            launchComposition.SessionStartPlan.CustomCommandPlan.BatchScriptContent,
            launchComposition.SessionStartPlan.CustomCommandPlan.UseUtf8Encoding ? new UTF8Encoding(false) : Encoding.Default);

        var sessionSummaryPath = GetUniqueFilePath(Path.Combine(logDirectory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log"));
        File.WriteAllLines(
            sessionSummaryPath,
            launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines,
            new UTF8Encoding(false));

        var rawOutputLogPath = Path.Combine(logDirectory, "RawOutput.log");
        if (File.Exists(rawOutputLogPath))
        {
            File.Delete(rawOutputLogPath);
        }

        foreach (var shellPlan in launchComposition.SessionStartPlan.CustomCommandShellPlans)
        {
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

        var process = SystemProcessManager.Current.Start(
            MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(
                launchComposition.SessionStartPlan.ProcessShellPlan))
            ?? throw new InvalidOperationException("游戏进程启动失败。");
        MinecraftLaunchProcessExecutionService.TryApplyPriority(
            process,
            launchComposition.SessionStartPlan.ProcessShellPlan.PriorityKind);

        ApplyPostLaunchShellPlan(launchComposition.PostLaunchShell);
        IncrementLaunchCounts(instanceDirectory, launchComposition.PostLaunchShell);

        return new FrontendLaunchStartResult(
            process,
            launchScriptPath,
            sessionSummaryPath,
            rawOutputLogPath);
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

    public string GetJavaExecutablePath(string runtimeDirectory) => PlatformAdapter.GetJavaExecutablePath(runtimeDirectory);

    public IReadOnlyList<string> GetDefaultJavaDetectionCandidates() => PlatformAdapter.GetDefaultJavaDetectionCandidates();

    public void EnsureFileExecutable(string path) => PlatformAdapter.EnsureFileExecutable(path);

    public string CreateLauncherShortcut(string displayName)
    {
        var desktopDirectory = PlatformAdapter.TryGetDesktopDirectory()
            ?? throw new InvalidOperationException("当前系统未提供桌面目录。");
        var executablePath = Environment.ProcessPath ?? Path.Combine(RuntimePaths.ExecutableDirectory, "PCL.Frontend.Spike");
        return PlatformAdapter.CreateLauncherShortcut(desktopDirectory, executablePath, displayName).ShortcutPath;
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

    private static MinecraftJavaRuntimeDownloadTransferPlan ResolveEffectiveJavaTransferPlan(
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

        var indexJson = DownloadUtf8String(MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().AllUrls);
        var liveManifestPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            new MinecraftJavaRuntimeManifestRequestPlanRequest(
                indexJson,
                manifestPlan.Selection.PlatformKey,
                manifestPlan.Selection.RequestedComponent,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        var manifestJson = DownloadUtf8String(liveManifestPlan.RequestUrls.AllUrls);
        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestJson,
                transferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory,
                null,
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
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                break;
            case MinecraftLaunchShellActionKind.ShowLauncher:
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
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
            var provider = OpenInstanceConfigProvider(instanceDirectory);
            var currentInstanceLaunchCount = provider.Exists("VersionLaunchCount")
                ? provider.Get<int>("VersionLaunchCount")
                : 0;
            provider.Set("VersionLaunchCount", currentInstanceLaunchCount + shellPlan.InstanceLaunchCountIncrement);
            provider.Sync();
        }
    }

    private T ReadLocalValue<T>(string key, T fallback)
    {
        var provider = new YamlFileProvider(RuntimePaths.LocalConfigPath);
        return provider.Exists(key)
            ? provider.Get<T>(key)
            : fallback;
    }

    private T ReadSharedValue<T>(string key, T fallback)
    {
        var provider = new JsonFileProvider(RuntimePaths.SharedConfigPath);
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
            error = "当前壳层未提供文件选择器。";
            return null;
        }

        return desktop.MainWindow.StorageProvider;
    }

    private static Avalonia.Controls.Window GetDesktopMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            throw new InvalidOperationException("当前壳层未提供桌面主窗口。");
        }

        return desktop.MainWindow;
    }

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        var configPath = Path.Combine(pclDirectory, "config.v1.yml");
        if (!File.Exists(configPath))
        {
            var legacyPath = Path.Combine(pclDirectory, "Setup.ini");
            if (File.Exists(legacyPath))
            {
                Directory.CreateDirectory(pclDirectory);
                var provider = new YamlFileProvider(configPath);
                foreach (var line in File.ReadLines(legacyPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var splitIndex = line.IndexOf(':');
                    if (splitIndex <= 0)
                    {
                        continue;
                    }

                    provider.Set(line[..splitIndex], line[(splitIndex + 1)..]);
                }

                provider.Sync();
            }
        }

        return new YamlFileProvider(configPath);
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

        throw new InvalidOperationException("无法下载 Java 运行时元数据。", lastError);
    }

    private static void DownloadJavaRuntimeFile(MinecraftJavaRuntimeDownloadRequestFilePlan file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);

        Exception? lastError = null;
        foreach (var url in file.RequestUrls.AllUrls)
        {
            try
            {
                var bytes = JavaRuntimeHttpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                var sha1 = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(sha1, file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Java 文件校验失败：{file.RelativePath}");
                }

                File.WriteAllBytes(file.TargetPath, bytes);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"无法下载 Java 文件：{file.RelativePath}", lastError);
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

    private byte[] ResolveSharedEncryptionKey()
    {
        var explicitKey = LauncherSecretKeyResolutionService.ParseExplicitKeyOverride(
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
        if (explicitKey is not null)
        {
            return explicitKey;
        }

        var persistedKeyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(RuntimePaths.SharedConfigDirectory);
        var persistedEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(persistedKeyPath)
            ?? throw new InvalidOperationException("Launcher encryption key is unavailable.");
        var storedKey = LauncherVersionedDataService.Parse(persistedEnvelope);
        return LauncherStoredKeyEnvelopeService.ReadKey(storedKey);
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

internal sealed record FrontendLaunchStartResult(
    Process Process,
    string LaunchScriptPath,
    string SessionSummaryPath,
    string RawOutputLogPath);

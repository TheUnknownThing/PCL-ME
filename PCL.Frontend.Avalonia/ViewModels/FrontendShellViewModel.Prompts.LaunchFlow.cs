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
    private async Task HandleLaunchRequestedAsync()
    {
        if (_isLaunchInProgress)
        {
            AddActivity(T("shell.prompts.activities.launch_in_progress.title"), T("shell.prompts.activities.launch_in_progress.body"));
            return;
        }

        await AwaitLatestSelectedInstanceRefreshAsync();
        RefreshLaunchState();

        if (_isLaunchBlockedByPrompt)
        {
            AddActivity(T("shell.prompts.activities.launch_blocked.title"), T("shell.prompts.activities.launch_blocked.body"));
            return;
        }

        if (!_launchComposition.PrecheckResult.IsSuccess)
        {
            AddFailureActivity(T("shell.prompts.activities.precheck_failed.title"), GetLaunchPrecheckFailureMessage());
            return;
        }

        _dismissedLaunchPromptIds.Clear();
        EnsureLaunchPromptLane();
        if (_promptCatalog[AvaloniaPromptLaneKind.Launch].Count > 0)
        {
            _pendingLaunchAfterPrompt = true;
            RebuildPromptLanes();
            SetPromptOverlayOpen(true);
            SelectPromptLane(AvaloniaPromptLaneKind.Launch, updateActivity: false);
            AddActivity(
                T("shell.prompts.activities.pending_launch_prompts.title"),
                T("shell.prompts.activities.pending_launch_prompts.body", ("instance", LaunchVersionSubtitle), ("count", _promptCatalog[AvaloniaPromptLaneKind.Launch].Count)));
            return;
        }

        await StartLaunchAsync(resumeAfterPrompt: false);
    }

    private async Task DownloadJavaRuntimeFromPromptAsync()
    {
        if (_launchComposition.JavaRuntimeInstallPlan is null)
        {
            AddFailureActivity(T("shell.prompts.activities.java_runtime_prepare_failed.title"), T("shell.prompts.activities.java_runtime_prepare_failed.body_missing_plan"));
            return;
        }

        var installPlan = _launchComposition.JavaRuntimeInstallPlan;
        var trackedRuntimeDirectory = installPlan.RuntimeDirectory;
        var shouldResumePendingLaunch = _pendingLaunchAfterPrompt;
        var downloadState = MinecraftJavaRuntimeDownloadSessionState.Loading;

        _isLaunchBlockedByPrompt = true;
        AddActivity(T("shell.prompts.activities.java_runtime_preparing.title"), T("shell.prompts.activities.java_runtime_preparing.body"));

        try
        {
            var installResult = await ExecuteManagedJavaRuntimeDownloadAsync(
                $"Auto download {installPlan.DisplayName} ({installPlan.SourceName})",
                installPlan);
            downloadState = MinecraftJavaRuntimeDownloadSessionState.Finished;

            try
            {
                await FrontendJavaInventoryService.RefreshPortableJavaScanCacheAsync();
            }
            catch
            {
                // Persisted Java storage keeps the downloaded runtime selectable even if a background scan fails.
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var executablePath = _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory);
                var installedRuntime = FrontendJavaInventoryService.TryResolveRuntime(
                                         executablePath,
                                         isEnabled: true,
                                         fallbackDisplayName: installPlan.DisplayName)
                                     ?? FrontendJavaInventoryService.CreateStoredRuntime(
                                         executablePath,
                                         installPlan.DisplayName,
                                         installPlan.VersionName,
                                         isEnabled: true,
                                         is64Bit: Is64BitMachineType(installPlan.RuntimeArchitecture),
                                         isJre: installPlan.IsJre,
                                         brand: installPlan.Brand,
                                         architecture: installPlan.RuntimeArchitecture);

                RegisterMaterializedJavaRuntime(installedRuntime);
                _launchComposition = _launchComposition with
                {
                    SelectedJavaRuntime = new FrontendJavaRuntimeSummary(
                        installedRuntime.ExecutablePath,
                        installedRuntime.DisplayName,
                        installedRuntime.MajorVersion ?? _launchComposition.JavaWorkflow.RecommendedMajorVersion,
                        IsEnabled: installedRuntime.IsEnabled,
                        Is64Bit: installedRuntime.Is64Bit ?? Environment.Is64BitOperatingSystem,
                        Architecture: installedRuntime.Architecture),
                    JavaWarningMessage = null
                };
                RaiseLaunchCompositionProperties();
                _isLaunchBlockedByPrompt = false;
                AddActivity(
                    T("shell.prompts.activities.java_runtime_ready.title"),
                    T("shell.prompts.activities.java_runtime_ready.body", ("path", installResult.RuntimeDirectory), ("downloaded_count", installResult.DownloadedFileCount), ("reused_count", installResult.ReusedFileCount)));

                if (shouldResumePendingLaunch)
                {
                    _pendingLaunchAfterPrompt = false;
                    AddActivity(T("shell.prompts.activities.java_runtime_ready.title"), T("shell.prompts.activities.java_runtime_resume.body"));
                    _ = StartLaunchAsync();
                    return;
                }

                _pendingLaunchAfterPrompt = false;
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava), T("shell.prompts.activities.navigate.java_settings"));
            });
        }
        catch (OperationCanceledException)
        {
            downloadState = MinecraftJavaRuntimeDownloadSessionState.Aborted;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isLaunchBlockedByPrompt = false;
                _pendingLaunchAfterPrompt = false;
                AddActivity("Java download canceled", "The automatic download was canceled and no new Java runtime was registered.");
            });
        }
        catch (Exception ex)
        {
            downloadState = MinecraftJavaRuntimeDownloadSessionState.Failed;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isLaunchBlockedByPrompt = false;
                _pendingLaunchAfterPrompt = false;
                AddFailureActivity(T("shell.prompts.activities.java_runtime_prepare_failed.title"), ex.Message);
            });
        }
        finally
        {
            var transitionPlan = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(
                downloadState,
                trackedRuntimeDirectory);

            if (!string.IsNullOrWhiteSpace(transitionPlan.CleanupLogMessage))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddActivity("Java download cleanup", transitionPlan.CleanupLogMessage);
                });
            }

            if (!string.IsNullOrWhiteSpace(transitionPlan.CleanupDirectoryPath) &&
                Directory.Exists(transitionPlan.CleanupDirectoryPath))
            {
                try
                {
                    Directory.Delete(transitionPlan.CleanupDirectoryPath, recursive: true);
                }
                catch
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AddActivity("Java download cleanup", $"Could not clean up directory automatically: {transitionPlan.CleanupDirectoryPath}");
                    });
                }
            }
        }
    }

    private void RegisterMaterializedJavaRuntime(FrontendStoredJavaRuntime installedRuntime)
    {
        var items = LoadStoredJavaItems();
        var existingIndex = items.FindIndex(item =>
            string.Equals(item.Path, installedRuntime.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        var updatedItem = new JavaStorageItem
        {
            Path = installedRuntime.ExecutablePath,
            IsEnable = true,
            Source = JavaSource.AutoInstalled,
            Installation = new JavaStorageInstallationInfo
            {
                JavaExePath = installedRuntime.ExecutablePath,
                DisplayName = installedRuntime.DisplayName,
                Version = installedRuntime.ParsedVersion?.ToString() ?? installedRuntime.DisplayName,
                MajorVersion = installedRuntime.MajorVersion,
                Is64Bit = installedRuntime.Is64Bit,
                IsJre = installedRuntime.IsJre,
                Brand = installedRuntime.Brand,
                Architecture = installedRuntime.Architecture
            }
        };

        if (existingIndex >= 0)
        {
            items[existingIndex] = updatedItem;
        }
        else
        {
            items.Add(updatedItem);
        }

        SaveStoredJavaItems(items);
        SelectJavaRuntime(installedRuntime.ExecutablePath);
        ReloadSetupComposition(initializeAllSurfaces: false);
    }

    private static bool? Is64BitMachineType(MachineType? machineType)
    {
        return machineType switch
        {
            MachineType.AMD64 or MachineType.ARM64 or MachineType.IA64 => true,
            MachineType.I386 or MachineType.ARM or MachineType.ARMNT => false,
            _ => null
        };
    }

    private async Task StartLaunchAsync(bool resumeAfterPrompt = false)
    {
        try
        {
            var launchCancellation = new CancellationTokenSource();
            _launchSessionCancellation = launchCancellation;
            ShowLaunchDialog();
            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                T("launch.dialog.state.running.initializing"),
                0d,
                showDownload: false,
                isError: false);

            _isLaunchInProgress = true;
            _showLaunchLog = true;
            ClearLaunchLogBuffer();
            RaiseLaunchSessionProperties();
            RefreshGameLogSurface();

            SetLaunchDialogRunningState(
                "Launching game",
                "Synchronizing instance state",
                0.01d,
                showDownload: false,
                isError: false);

            await AwaitLatestSelectedInstanceRefreshAsync();
            launchCancellation.Token.ThrowIfCancellationRequested();

            await EnsureSelectedLaunchProfileReadyForLaunchAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            await RefreshLaunchCompositionAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();
            AppendLaunchDebugCompositionSnapshot();

            if (!_launchComposition.PrecheckResult.IsSuccess)
            {
                var failureMessage = GetLaunchPrecheckFailureMessage();
                AddFailureActivity(T("shell.prompts.activities.precheck_failed.title"), failureMessage);
                SetLaunchDialogStoppedState(T("shell.prompts.activities.precheck_failed.title"), failureMessage, isError: true);
                return;
            }

            EnsureLaunchPromptLane();
            if (_promptCatalog[AvaloniaPromptLaneKind.Launch].Count > 0)
            {
                _pendingLaunchAfterPrompt = true;
                RebuildPromptLanes();
                HideLaunchDialog();
                SetPromptOverlayOpen(true);
                SelectPromptLane(AvaloniaPromptLaneKind.Launch, updateActivity: false);
                AddActivity("Launch prompts pending", $"{LaunchVersionSubtitle} still has {_promptCatalog[AvaloniaPromptLaneKind.Launch].Count} prompts that need confirmation.");
                _isLaunchInProgress = false;
                RaiseLaunchSessionProperties();
                return;
            }

            if (_launchComposition.SelectedJavaRuntime is null)
            {
                throw new InvalidOperationException(T("launch.status.errors.java_missing"));
            }

            if (!string.IsNullOrWhiteSpace(_launchComposition.JavaWarningMessage))
            {
                AppendLaunchLogLine(_launchComposition.JavaWarningMessage);
                AddActivity(T("launch.status.activities.java_check_ignored"), _launchComposition.JavaWarningMessage);
            }

            foreach (var line in _launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines)
            {
                AppendLaunchLogLine(line);
            }

            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection
                    ? T("launch.status.steps.prepare_arguments")
                    : T("launch.dialog.stages.verify_instance"),
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection ? 0.18d : 0.06d,
                showDownload: false,
                isError: false);

            await EnsureLaunchFilesAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                T("launch.status.steps.write_launch_script"),
                0.88d,
                showDownload: false,
                isError: false);

            var startResult = await Task.Run(() => _shellActionService.StartLaunchSession(
                _launchComposition,
                _instanceComposition.Selection.InstanceDirectory,
                stage => Dispatcher.UIThread.Post(() => SetLaunchDialogRunningState(
                    "Launching game",
                    stage,
                    ResolveLaunchDialogStartupProgress(stage),
                    showDownload: false,
                    isError: false)),
                launchCancellation.Token));
            _activeLaunchProcess = startResult.Process;
            _latestLaunchScriptPath = startResult.LaunchScriptPath;
            _latestLaunchSessionSummaryPath = startResult.SessionSummaryPath;
            _latestLaunchRawOutputLogPath = startResult.RawOutputLogPath;
            RefreshDebugModeSurface();
            AppendLaunchLogLine(_launchComposition.SessionStartPlan.ProcessShellPlan.StartedLogMessage);
            AppendLaunchDebugLine("Launch script", startResult.LaunchScriptPath);
            AppendLaunchDebugLine("Session summary", startResult.SessionSummaryPath);
            AppendLaunchDebugLine("Raw output", startResult.RawOutputLogPath);
            AddActivity(T("launch.status.activities.game_process_started"), T("launch.status.messages.game_process_started", ("instance_name", LaunchVersionSubtitle), ("pid", startResult.Process.Id)));
            if (_currentRoute.Page != LauncherFrontendPageKey.Launch)
            {
                NavigateTo(
                    new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                    T("launch.status.messages.returned_to_launch_page"),
                    RouteNavigationBehavior.Reset);
            }

            AvaloniaHintBus.Show(T("launch.status.hints.launch_succeeded"), AvaloniaHintTheme.Success);
            HideLaunchDialog();
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();

            _ = MonitorLaunchSessionAsync(startResult);
        }
        catch (OperationCanceledException)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine(T("launch.status.logs.canceled"));
            AddActivity(T("launch.status.activities.canceled"), T("launch.status.messages.canceled"));
            SetLaunchDialogStoppedState(T("launch.status.stopped.canceled_title"), T("launch.status.messages.canceled"), isError: false);
        }
        catch (Exception ex)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine(T("launch.status.logs.failed", ("message", ex.Message)));
            AppendLaunchDebugException("Launch failure details", ex);
            AddFailureActivity(T("launch.status.activities.failed"), ex.Message);
            SetLaunchDialogStoppedState(T("launch.status.stopped.failed_title"), ex.Message, isError: true);
        }
        finally
        {
            _launchSessionCancellation?.Dispose();
            _launchSessionCancellation = null;
            RaiseLaunchSessionProperties();
        }
    }

    private static double ResolveLaunchDialogStartupProgress(string stage)
    {
        return stage switch
        {
            "Checking runtime dependencies" => 0.9d,
            "Synchronizing local game libraries" => 0.93d,
            "Writing pre-launch configuration" => 0.95d,
            "Running pre-launch commands" => 0.97d,
            "Starting game process" => 0.99d,
            _ => 0.9d
        };
    }

    private async Task RefreshLaunchCompositionAsync(CancellationToken cancellationToken)
    {
        SetLaunchDialogRunningState(
            T("launch.dialog.state.running.title"),
            T("launch.status.steps.read_configuration"),
            0.02d,
            showDownload: false,
            isError: false);

        var ignoreJavaCompatibilityWarningOnce = _ignoreJavaCompatibilityWarningOnce;
        _ignoreJavaCompatibilityWarningOnce = false;
        var launchComposition = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FrontendLaunchCompositionService.Compose(
                _options,
                _shellActionService.RuntimePaths,
                ignoreJavaCompatibilityWarningOnce,
                _i18n);
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
    }

    private async Task EnsureSelectedLaunchProfileReadyForLaunchAsync(CancellationToken cancellationToken)
    {
        var result = await RefreshSelectedLaunchProfileCoreAsync(
            cancellationToken,
            forceRefresh: false,
            onStatusChanged: stage => SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                stage,
                0.03d,
                showDownload: false,
                isError: false));
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.WasChecked)
        {
            return;
        }

        if (result.ShouldInvalidateAvatarCache)
        {
            InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
        }

        AppendLaunchLogLine(result.Message);
        AddActivity(T("launch.status.activities.prelaunch_account_check"), result.Message);
    }

    private async Task EnsureLaunchFilesAsync(CancellationToken cancellationToken)
    {
        if (!_instanceComposition.Selection.HasSelection || DisableInstanceFileValidation)
        {
            return;
        }

        AppendLaunchLogLine(T("launch.status.logs.verifying_instance_files"));
        AppendLaunchLogLine(T("launch.status.logs.task_manager_progress"));

        try
        {
            var repairResult = await ExecuteManagedInstanceRepairAsync(
                T("launch.status.tasks.verify_instance_files", ("instance_name", _instanceComposition.Selection.InstanceName)),
                new FrontendInstanceRepairRequest(
                    _instanceComposition.Selection.LauncherDirectory,
                    _instanceComposition.Selection.InstanceDirectory,
                    _instanceComposition.Selection.InstanceName,
                    ForceCoreRefresh: false),
                ApplyLaunchRepairProgress,
                cancellationToken);
            var completionMessage = T("launch.status.messages.instance_verification_completed", ("downloaded_count", repairResult.DownloadedFiles.Count), ("reused_count", repairResult.ReusedFiles.Count));
            AppendLaunchLogLine(completionMessage);
            AppendRepairDebugSummary(repairResult);

            if (repairResult.DownloadedFiles.Count > 0)
            {
                AddActivity(T("launch.status.activities.instance_verification"), completionMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(T("launch.status.errors.instance_verification_failed", ("message", ex.Message)), ex);
        }
    }

    private async Task MonitorLaunchSessionAsync(FrontendLaunchStartResult startResult)
    {
        try
        {
            startResult.Process.EnableRaisingEvents = true;
            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.BeginOutputReadLine();
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.BeginErrorReadLine();
            }

            await startResult.Process.WaitForExitAsync();
            _shellActionService.ApplyWatcherStopShellPlan(_launchComposition);
            AppendLaunchLogLine(T("shell.prompts.launch_logs.process_exited", ("exit_code", startResult.Process.ExitCode)));
            AddActivity(T("shell.prompts.activities.game_process_ended.title"), T("shell.prompts.activities.game_process_ended.body", ("instance", LaunchVersionSubtitle), ("exit_code", startResult.Process.ExitCode)));
            if (startResult.Process.ExitCode != 0 && !_launchProcessTerminationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ShowCrashPromptForLaunchFailure(startResult));
            }
        }
        catch (Exception ex)
        {
            AppendLaunchLogLine(T("shell.prompts.launch_logs.monitor_exception", ("message", ex.Message)));
            AppendLaunchDebugException("Session monitor exception details", ex);
            AddActivity(T("shell.prompts.activities.monitor_exception.title"), ex.Message);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _activeLaunchProcess = null;
                _launchProcessTerminationRequested = false;
                _isLaunchInProgress = false;
                RaiseLaunchSessionProperties();
                RefreshLaunchState();
                RefreshGameLogSurface();
                if (IsLaunchDialogVisible)
                {
                    HideLaunchDialog();
                }
            });
        }
    }

    private void RefreshLaunchState()
    {
        ReloadSetupComposition();
        ReloadInstanceComposition();
        ApplyLaunchComposition(
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths, i18n: _i18n),
            normalizeLaunchProfileSurface: true);
    }

    private async Task RefreshLaunchProfileCompositionAsync()
    {
        var refreshVersion = Interlocked.Increment(ref _launchProfileCompositionRefreshVersion);
        var launchComposition = await Task.Run(() =>
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths, i18n: _i18n));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _launchProfileCompositionRefreshVersion)
            {
                return;
            }

            ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
        });
    }

    private void ApplyLaunchComposition(FrontendLaunchComposition composition, bool normalizeLaunchProfileSurface)
    {
        _launchComposition = composition;
        ClearOptimisticLaunchInstanceName(raiseProperties: false);
        if (normalizeLaunchProfileSurface)
        {
            NormalizeLaunchProfileSurface();
        }

        var launchPromptContextKey = BuildLaunchPromptContextKey(_launchComposition, _instanceComposition.Selection.InstanceDirectory);
        if (!string.Equals(_launchPromptContextKey, launchPromptContextKey, StringComparison.Ordinal))
        {
            _dismissedLaunchPromptIds.Clear();
            _launchPromptContextKey = launchPromptContextKey;
        }

        RaiseLaunchCompositionProperties();
        RefreshDebugModeSurface();
        ScheduleLaunchAvatarRefresh();
    }

    private void AppendLaunchLogLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AppendLaunchLogEntry(line);
            RaiseGameLogSurfaceProperties();
            if (!_showLaunchLog)
            {
                _showLaunchLog = true;
                RaisePropertyChanged(nameof(ShowLaunchLog));
            }
        });
    }

    private void ShowLaunchCompletionNotification()
    {
        var message = _i18n.T(_launchComposition.CompletionNotification.Message);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        switch (_launchComposition.CompletionNotification.Kind)
        {
            case MinecraftLaunchNotificationKind.Info:
                AvaloniaHintBus.Show(message, AvaloniaHintTheme.Info);
                break;
            case MinecraftLaunchNotificationKind.Finish:
                AvaloniaHintBus.Show(message, AvaloniaHintTheme.Success);
                break;
        }
    }

    private void RaiseLaunchSessionProperties()
    {
        RaisePropertyChanged(nameof(LaunchButtonTitle));
        RaisePropertyChanged(nameof(ShowLaunchLog));
        RaisePropertyChanged(nameof(LaunchLogText));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _launchCommand.NotifyCanExecuteChanged();
        _cancelLaunchCommand.NotifyCanExecuteChanged();
        RaiseLaunchDialogProperties();
    }

    private string GetLaunchPrecheckFailureMessage()
    {
        return _launchComposition.PrecheckResult.Failure is { } failure
            ? _i18n.T(failure.ToLocalizedText())
            : _i18n.T("launch.precheck.failures.unknown");
    }
}

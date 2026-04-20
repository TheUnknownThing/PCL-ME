using System.Text;
using Avalonia;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private async Task CheckForLauncherUpdatesAsync(bool forceRefresh, bool isUserInitiated = false)
    {
        var updateMode = ResolveSelectedUpdateMode();
        var signature = $"{SelectedUpdateChannelIndex}:{(int)updateMode}";
        if (_isCheckingUpdate)
        {
            return;
        }

        if (!forceRefresh
            && string.Equals(_lastUpdateCheckSignature, signature, StringComparison.Ordinal)
            && _updateStatus.SurfaceState is not UpdateSurfaceState.Checking)
        {
            return;
        }

        if (updateMode == LauncherUpdateMode.Disabled && !isUserInitiated)
        {
            _lastUpdateCheckSignature = signature;
            _updateStatus = CreateDisabledUpdateStatus();
            RaiseUpdateSurfaceProperties();
            return;
        }

        _isCheckingUpdate = true;
        _updateStatus = FrontendSetupUpdateStatusService.CreateChecking(_i18n);
        RaiseUpdateSurfaceProperties();

        try
        {
            _updateStatus = await FrontendSetupUpdateStatusService.QueryAsync(SelectedUpdateChannelIndex, _i18n);
            _lastUpdateCheckSignature = signature;
            RaiseUpdateSurfaceProperties();

            AddActivity(
                LT("setup.update.activities.refresh"),
                _updateStatus.SurfaceState switch
                {
                    UpdateSurfaceState.Available => LT("setup.update.activities.available", ("version", _updateStatus.AvailableUpdateName)),
                    UpdateSurfaceState.Latest => LT("setup.update.activities.latest", ("version", _updateStatus.CurrentVersionName)),
                    UpdateSurfaceState.Error => _updateStatus.CurrentVersionDescription,
                    _ => LT("setup.update.activities.checking")
                });

            if (_updateStatus.SurfaceState == UpdateSurfaceState.Available)
            {
                await HandleUpdateModeAfterCheckAsync(updateMode);
            }
        }
        finally
        {
            _isCheckingUpdate = false;
        }
    }

    private void DownloadAvailableUpdate()
    {
        _ = DownloadAvailableUpdateAsync();
    }

    private async Task DownloadAvailableUpdateAsync()
    {
        if (_isDownloadingLauncherUpdate)
        {
            return;
        }

        if (_updateStatus.SurfaceState != UpdateSurfaceState.Available)
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.no_pending_update"));
            return;
        }

        var target = _updateStatus.AvailableUpdateDownloadUrl ?? _updateStatus.AvailableUpdateReleaseUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.no_download_url"));
            return;
        }

        try
        {
            _isDownloadingLauncherUpdate = true;
            var preparedInstall = await PrepareAvailableUpdateAsync(target);
            await ApplyPreparedUpdateAsync(preparedInstall);
        }
        catch (OperationCanceledException)
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("download.install.workflow.tasks.canceled"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.update.activities.download_install_failed"), ex.Message);
        }
        finally
        {
            _isDownloadingLauncherUpdate = false;
        }
    }

    private void ShowAvailableUpdateDetail() => _ = ShowAvailableUpdateDetailAsync();

    private async Task ShowAvailableUpdateDetailAsync()
    {
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateChangelog))
        {
            var result = await ShowToolboxConfirmationAsync(
                LT("setup.update.activities.view_detail_title", ("version", _updateStatus.AvailableUpdateName)),
                _updateStatus.AvailableUpdateChangelog);
            if (result is null)
            {
                return;
            }

            AddActivity(LT("setup.update.activities.view_detail"), _updateStatus.AvailableUpdateName);

            return;
        }

        string? openError = null;
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateReleaseUrl)
            && _shellActionService.TryOpenExternalTarget(_updateStatus.AvailableUpdateReleaseUrl, out openError))
        {
            AddActivity(LT("setup.update.activities.view_detail"), _updateStatus.AvailableUpdateReleaseUrl);
            return;
        }

        AddFailureActivity(
            LT("setup.update.activities.view_detail_failed"),
            openError ?? LT("setup.update.activities.detail_unavailable"));
    }

    private void ResetLaunchSettingsSurface()
    {
        _shellActionService.RemoveLocalValues(LaunchLocalResetKeys);
        _shellActionService.RemoveSharedValues(LaunchSharedResetKeys);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.launch.activities.reset"),
            LT("setup.launch.activities.reset_completed"));
    }

    private LauncherUpdateMode ResolveSelectedUpdateMode()
    {
        return SelectedUpdateModeIndex switch
        {
            (int)LauncherUpdateMode.AutoDownloadAndInstall => LauncherUpdateMode.AutoDownloadAndInstall,
            (int)LauncherUpdateMode.AutoDownloadAndPrompt => LauncherUpdateMode.AutoDownloadAndPrompt,
            (int)LauncherUpdateMode.PromptOnly => LauncherUpdateMode.PromptOnly,
            _ => LauncherUpdateMode.Disabled
        };
    }

    private FrontendSetupUpdateStatus CreateDisabledUpdateStatus()
    {
        var disabledDescription = UpdateModeOptions.Count > SelectedUpdateModeIndex
            ? UpdateModeOptions[SelectedUpdateModeIndex]
            : string.Empty;
        var status = FrontendSetupUpdateStatusService.CreateDefault(_i18n);
        return status with
        {
            SurfaceState = UpdateSurfaceState.Latest,
            CurrentVersionDescription = disabledDescription
        };
    }

    private async Task HandleUpdateModeAfterCheckAsync(LauncherUpdateMode updateMode)
    {
        switch (updateMode)
        {
            case LauncherUpdateMode.AutoDownloadAndInstall:
                await DownloadAvailableUpdateAsync();
                break;
            case LauncherUpdateMode.AutoDownloadAndPrompt:
                await PrepareAndPromptAvailableUpdateAsync();
                break;
        }
    }

    private async Task PrepareAndPromptAvailableUpdateAsync()
    {
        if (_isDownloadingLauncherUpdate)
        {
            return;
        }

        var target = _updateStatus.AvailableUpdateDownloadUrl ?? _updateStatus.AvailableUpdateReleaseUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.no_download_url"));
            return;
        }

        try
        {
            _isDownloadingLauncherUpdate = true;
            var preparedInstall = await PrepareAvailableUpdateAsync(target);
            var confirmed = await ShowToolboxConfirmationAsync(
                LT("setup.update.activities.view_detail_title", ("version", _updateStatus.AvailableUpdateName)),
                BuildPreparedUpdateApplyPromptMessage());
            if (confirmed is not true)
            {
                return;
            }

            await ApplyPreparedUpdateAsync(preparedInstall);
        }
        catch (OperationCanceledException)
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("download.install.workflow.tasks.canceled"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.update.activities.download_install_failed"), ex.Message);
        }
        finally
        {
            _isDownloadingLauncherUpdate = false;
        }
    }

    private string BuildPreparedUpdateApplyPromptMessage()
    {
        var builder = new StringBuilder();
        builder.AppendLine(LT("setup.update.activities.package_ready", ("version", _updateStatus.AvailableUpdateName)));

        var detail = string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateChangelog)
            ? _updateStatus.AvailableUpdateSummary
            : _updateStatus.AvailableUpdateChangelog;
        if (!string.IsNullOrWhiteSpace(detail))
        {
            builder.AppendLine();
            builder.AppendLine(detail.Trim());
        }

        return builder.ToString().Trim();
    }

    private async Task<FrontendPreparedUpdateInstall> PrepareAvailableUpdateAsync(string target)
    {
        var request = new FrontendUpdateInstallRequest(
            DownloadUrl: target,
            ReleaseFileStem: SanitizeFileSegment(_updateStatus.AvailableUpdateName),
            ExpectedSha256: string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateSha256)
                ? null
                : _updateStatus.AvailableUpdateSha256,
            ArtifactDirectory: _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            TempDirectory: _shellActionService.RuntimePaths.FrontendTempDirectory,
            ExecutableDirectory: _shellActionService.RuntimePaths.ExecutableDirectory,
            ProcessPath: Environment.ProcessPath,
            ProcessId: Environment.ProcessId,
            PlatformAdapter: _shellActionService.PlatformAdapter);
        var updateTask = new FrontendManagedUpdateInstallTask(
            _i18n,
            $"{LT("setup.update.activities.download_install")}: {_updateStatus.AvailableUpdateName}",
            async (progressReporter, cancellationToken) =>
            {
                var preparedInstall = await FrontendUpdateInstallWorkflowService.PrepareAsync(
                    request,
                    progressReporter,
                    cancellationToken);
                WritePreparedUpdateRecord(target, preparedInstall);
                return preparedInstall;
            });

        TaskCenter.Register(updateTask, start: false);
        AddActivity(LT("setup.update.activities.download_install"), updateTask.Title);
        await updateTask.ExecuteAsync();
        return updateTask.Result ?? throw new InvalidOperationException(LT("setup.update.activities.download_install_failed"));
    }

    private void WritePreparedUpdateRecord(string target, FrontendPreparedUpdateInstall preparedInstall)
    {
        var outputPath = Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "update-downloads",
            $"{SanitizeFileSegment(_updateStatus.AvailableUpdateName)}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, $"""
            Update: {_updateStatus.AvailableUpdateName}
            Source: {_updateStatus.AvailableUpdateSource}
            SHA256: {_updateStatus.AvailableUpdateSha256}
            Download: {target}
            Release: {_updateStatus.AvailableUpdateReleaseUrl}
            Archive: {preparedInstall.ArchivePath}
            Extracted: {preparedInstall.ExtractedPackagePath}
            Script: {preparedInstall.InstallerScriptPath}
            """, new UTF8Encoding(false));
    }

    private async Task ApplyPreparedUpdateAsync(FrontendPreparedUpdateInstall preparedInstall)
    {
        if (!_shellActionService.TryStartDetachedScript(preparedInstall.InstallerScriptPath, out var error))
        {
            AddFailureActivity(
                LT("setup.update.activities.download_install_failed"),
                error ?? preparedInstall.InstallerScriptPath);
            return;
        }

        AddActivity(
            LT("setup.update.activities.download_install"),
            LT("setup.update.activities.package_ready", ("version", _updateStatus.AvailableUpdateName)));
        AvaloniaHintBus.Show(LT("setup.update.activities.package_ready_hint"), AvaloniaHintTheme.Success);
        await Task.Delay(400);
        _shellActionService.ExitLauncher();
    }
}

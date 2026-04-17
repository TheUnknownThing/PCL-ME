using System.Text;
using Avalonia;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private async Task CheckForLauncherUpdatesAsync(bool forceRefresh)
    {
        var signature = $"{SelectedUpdateChannelIndex}";
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
            var preparedInstall = await FrontendUpdateInstallWorkflowService.PrepareAsync(
                new FrontendUpdateInstallRequest(
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
                    PlatformAdapter: _shellActionService.PlatformAdapter));
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
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.update.activities.download_install_failed"), ex.Message);
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
}

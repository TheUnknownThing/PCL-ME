using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Diagnostics;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private CancellationTokenSource? _launchSessionCancellation;
    private Process? _activeLaunchProcess;
    private bool _launchProcessTerminationRequested;
    private bool _launchDialogHasSuccessfulSession;
    private bool _isLaunchDialogVisible;
    private bool _isLaunchDialogBusy;
    private bool _isLaunchDialogError;
    private bool _showLaunchDialogProgress = true;
    private bool _showLaunchDialogDownload;
    private bool _showLaunchDialogHint;
    private double _launchDialogProgress;
    private string _launchDialogTitle = string.Empty;
    private string _launchDialogStage = string.Empty;
    private string _launchDialogMethod = string.Empty;
    private string _launchDialogInstanceName = string.Empty;
    private string _launchDialogProgressText = "0.00 %";
    private string _launchDialogDownloadText = string.Empty;
    private string _launchDialogHint = string.Empty;
    private string _launchDialogActionText = string.Empty;

    public bool IsLaunchDialogVisible => _isLaunchDialogVisible;

    public bool IsLaunchDialogBusy => _isLaunchDialogBusy;

    public bool IsLaunchDialogError => _isLaunchDialogError;

    public bool ShowLaunchDialogProgress => _showLaunchDialogProgress;

    public bool ShowLaunchDialogDownload => _showLaunchDialogDownload;

    public bool ShowLaunchDialogHint => _showLaunchDialogHint;

    public string LaunchDialogTitle => _launchDialogTitle;

    public string LaunchDialogStage => _launchDialogStage;

    public string LaunchDialogMethod => _launchDialogMethod;

    public string LaunchDialogInstanceName => _launchDialogInstanceName;

    public string LaunchDialogProgressText => _launchDialogProgressText;

    public string LaunchDialogDownloadText => _launchDialogDownloadText;

    public string LaunchDialogHint => _launchDialogHint;

    public string LaunchDialogActionText => _launchDialogActionText;

    public GridLength LaunchDialogProgressFinishedWidth => new(Math.Clamp(_launchDialogProgress, 0d, 1d), GridUnitType.Star);

    public GridLength LaunchDialogProgressRemainingWidth => new(Math.Max(1d - Math.Clamp(_launchDialogProgress, 0d, 1d), 0d), GridUnitType.Star);

    private void ShowLaunchDialog()
    {
        _launchDialogMethod = LaunchProfileDescription;
        _launchDialogInstanceName = LaunchVersionSubtitle;
        _launchDialogHint = _showLaunchingHint
            ? FrontendLaunchHintService.GetRandomHint(_launcherActionService.RuntimePaths, _i18n)
            : string.Empty;
        _launchDialogHasSuccessfulSession = false;
        _launchProcessTerminationRequested = false;
        _launchDialogActionText = T("common.actions.cancel");
        _isLaunchDialogBusy = true;
        _isLaunchDialogError = false;
        _showLaunchDialogHint = _showLaunchingHint && !string.IsNullOrWhiteSpace(_launchDialogHint);
        _showLaunchDialogProgress = true;
        _showLaunchDialogDownload = false;
        _launchDialogDownloadText = T("launch.dialog.download_speed.zero");
        _launchDialogProgress = 0d;
        _launchDialogProgressText = "0.00 %";
        _launchDialogTitle = T("launch.dialog.state.running.title");
        _launchDialogStage = T("launch.dialog.state.running.initializing");
        if (!_isLaunchDialogVisible)
        {
            _isLaunchDialogVisible = true;
            RaisePropertyChanged(nameof(IsLaunchDialogVisible));
            NotifyTopLevelNavigationInteractionChanged();
        }

        RaiseLaunchDialogProperties();
    }

    private void HideLaunchDialog()
    {
        if (!_isLaunchDialogVisible)
        {
            return;
        }

        _isLaunchDialogVisible = false;
        RaisePropertyChanged(nameof(IsLaunchDialogVisible));
        NotifyTopLevelNavigationInteractionChanged();
        RaiseLaunchDialogProperties();
    }

    private void SetLaunchDialogRunningState(
        string title,
        string stage,
        double progress,
        bool showDownload,
        bool isError)
    {
        _launchDialogTitle = title;
        _launchDialogStage = stage;
        _launchDialogProgress = Math.Clamp(progress, 0d, 1d);
        _launchDialogProgressText = $"{_launchDialogProgress * 100:0.00} %";
        _showLaunchDialogProgress = true;
        _showLaunchDialogHint = _showLaunchingHint && !string.IsNullOrWhiteSpace(_launchDialogHint);
        _showLaunchDialogDownload = showDownload;
        _isLaunchDialogBusy = !isError;
        _isLaunchDialogError = isError;
        _launchDialogActionText = T("common.actions.cancel");
        RaiseLaunchDialogProperties();
    }

    private void SetLaunchDialogStoppedState(string title, string stage, bool isError)
    {
        if (!_isLaunchDialogVisible)
        {
            ShowLaunchDialog();
        }

        _launchDialogTitle = title;
        _launchDialogStage = stage;
        _showLaunchDialogHint = false;
        _showLaunchDialogDownload = false;
        _launchDialogDownloadText = T("launch.dialog.download_speed.zero");
        _launchDialogHasSuccessfulSession = false;
        _isLaunchDialogBusy = false;
        _isLaunchDialogError = isError;
        _launchDialogActionText = T("common.actions.close");
        RaiseLaunchDialogProperties();
    }

    private void ApplyLaunchRepairProgress(FrontendInstanceRepairProgressSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var stage = FrontendLaunchRepairStageService.ResolveStage(snapshot);
            _launchDialogStage = stage;
            _launchDialogProgress = Math.Clamp(0.08d + snapshot.Progress * 0.72d, 0d, 0.82d);
            _launchDialogProgressText = $"{_launchDialogProgress * 100:0.00} %";
            _launchDialogDownloadText = snapshot.SpeedBytesPerSecond > 0d
                ? $"{FormatLaunchDialogBytes(snapshot.SpeedBytesPerSecond)}/s"
                : T("launch.dialog.download_speed.zero");
            _showLaunchDialogDownload = snapshot.TotalFileCount > 0;
            _showLaunchDialogProgress = true;
            _showLaunchDialogHint = _showLaunchingHint && !string.IsNullOrWhiteSpace(_launchDialogHint);
            RaiseLaunchDialogProperties();
        });
    }

    private string ResolveLaunchRepairStage(FrontendInstanceRepairProgressSnapshot snapshot)
    {
        var assets = snapshot.Groups.TryGetValue(FrontendInstanceRepairFileGroup.Assets, out var assetGroup)
            ? assetGroup
            : null;
        if (assets is not null && assets.TotalFiles > 0 && assets.Progress < 0.999d)
        {
            return string.IsNullOrWhiteSpace(assets.CurrentFileName)
                ? T("launch.dialog.stages.assets")
                : T("launch.dialog.stages.assets_file", ("file_name", assets.CurrentFileName));
        }

        var supportGroups = new[]
        {
            FrontendInstanceRepairFileGroup.Client,
            FrontendInstanceRepairFileGroup.Libraries,
            FrontendInstanceRepairFileGroup.AssetIndex
        };
        foreach (var group in supportGroups)
        {
            if (!snapshot.Groups.TryGetValue(group, out var snapshotGroup) || snapshotGroup.TotalFiles == 0 || snapshotGroup.Progress >= 0.999d)
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(snapshotGroup.CurrentFileName)
                ? T("launch.dialog.stages.support")
                : T("launch.dialog.stages.support_file", ("file_name", snapshotGroup.CurrentFileName));
        }

        return T("launch.dialog.stages.verify_instance");
    }

    private void HandleCancelLaunchRequested()
    {
        if (_launchSessionCancellation is { IsCancellationRequested: false })
        {
            _launchSessionCancellation.Cancel();
            SetLaunchDialogStoppedState(
                T("launch.dialog.state.canceling.title"),
                T("launch.dialog.state.canceling.prelaunch_tasks"),
                isError: false);
            return;
        }

        if (_launchDialogHasSuccessfulSession)
        {
            HideLaunchDialog();
            return;
        }

        if (_activeLaunchProcess is not null)
        {
            try
            {
                if (!_activeLaunchProcess.HasExited)
                {
                    _launchProcessTerminationRequested = true;
                    _activeLaunchProcess.Kill(entireProcessTree: true);
                    SetLaunchDialogStoppedState(
                        T("launch.dialog.state.canceling.title"),
                        T("launch.dialog.state.canceling.game_process"),
                        isError: false);
                    return;
                }
            }
            catch
            {
                // Fall through and close the surface if the process has already exited or cannot be killed.
            }
        }

        HideLaunchDialog();
    }

    private void RaiseLaunchDialogProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchDialogBusy));
        RaisePropertyChanged(nameof(IsLaunchDialogError));
        RaisePropertyChanged(nameof(ShowLaunchDialogProgress));
        RaisePropertyChanged(nameof(ShowLaunchDialogDownload));
        RaisePropertyChanged(nameof(ShowLaunchDialogHint));
        RaisePropertyChanged(nameof(LaunchDialogTitle));
        RaisePropertyChanged(nameof(LaunchDialogStage));
        RaisePropertyChanged(nameof(LaunchDialogMethod));
        RaisePropertyChanged(nameof(LaunchDialogInstanceName));
        RaisePropertyChanged(nameof(LaunchDialogProgressText));
        RaisePropertyChanged(nameof(LaunchDialogDownloadText));
        RaisePropertyChanged(nameof(LaunchDialogHint));
        RaisePropertyChanged(nameof(LaunchDialogActionText));
        RaisePropertyChanged(nameof(LaunchDialogProgressFinishedWidth));
        RaisePropertyChanged(nameof(LaunchDialogProgressRemainingWidth));
        _cancelLaunchCommand.NotifyCanExecuteChanged();
    }

    private static string FormatLaunchDialogBytes(double value)
    {
        if (value <= 0d)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = value;
        var unitIndex = 0;
        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

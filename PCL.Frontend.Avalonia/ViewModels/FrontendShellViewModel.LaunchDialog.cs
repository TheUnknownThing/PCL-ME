using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Diagnostics;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
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
    private string _launchDialogTitle = "正在启动游戏";
    private string _launchDialogStage = "初始化";
    private string _launchDialogMethod = string.Empty;
    private string _launchDialogInstanceName = string.Empty;
    private string _launchDialogProgressText = "0.00 %";
    private string _launchDialogDownloadText = "0 B/s";
    private string _launchDialogHint = string.Empty;
    private string _launchDialogActionText = "取消";

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
            ? FrontendLaunchHintService.GetRandomHint(_shellActionService.RuntimePaths)
            : string.Empty;
        _launchDialogHasSuccessfulSession = false;
        _launchProcessTerminationRequested = false;
        _launchDialogActionText = "取消";
        _isLaunchDialogBusy = true;
        _isLaunchDialogError = false;
        _showLaunchDialogHint = _showLaunchingHint && !string.IsNullOrWhiteSpace(_launchDialogHint);
        _showLaunchDialogProgress = true;
        _showLaunchDialogDownload = false;
        _launchDialogDownloadText = "0 B/s";
        _launchDialogProgress = 0d;
        _launchDialogProgressText = "0.00 %";
        _launchDialogTitle = "正在启动游戏";
        _launchDialogStage = "初始化";
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
        _launchDialogActionText = "取消";
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
        _launchDialogDownloadText = "0 B/s";
        _launchDialogHasSuccessfulSession = false;
        _isLaunchDialogBusy = false;
        _isLaunchDialogError = isError;
        _launchDialogActionText = "关闭";
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
                : "0 B/s";
            _showLaunchDialogDownload = snapshot.TotalFileCount > 0;
            _showLaunchDialogProgress = true;
            _showLaunchDialogHint = _showLaunchingHint && !string.IsNullOrWhiteSpace(_launchDialogHint);
            RaiseLaunchDialogProperties();
        });
    }

    private void HandleCancelLaunchRequested()
    {
        if (_launchSessionCancellation is { IsCancellationRequested: false })
        {
            _launchSessionCancellation.Cancel();
            SetLaunchDialogStoppedState("正在取消启动", "正在取消启动前任务…", isError: false);
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
                    SetLaunchDialogStoppedState("正在取消启动", "正在结束游戏进程…", isError: false);
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

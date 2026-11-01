using PCL.Core.App.I18n;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed class FrontendManagedUpdateInstallTask(
    II18nService i18n,
    string title,
    Func<Action<FrontendUpdateInstallProgressSnapshot>, CancellationToken, Task<FrontendPreparedUpdateInstall>> prepareAsync)
    : ITask, ITaskProgressive, ITaskGroup, ITaskProgressStatus, ITaskCancelable
{
    private readonly FrontendInstallStageTask _prepareStage = new(i18n.T("download.install.workflow.stages.prepare"));
    private readonly FrontendInstallStageTask _downloadStage = new(i18n.T("download.install.workflow.stages.support_files"));
    private readonly FrontendInstallStageTask _finalizeStage = new(i18n.T("download.install.workflow.stages.finalize"));
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", i18n.T("launch.dialog.download_speed.zero"), 1, null);
    private bool _stagesAdded;
    private double _progress;

    public string Title { get; } = title;

    public FrontendPreparedUpdateInstall? Result { get; private set; }

    public TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskGroupEvent AddTask = delegate { };

    public event TaskGroupEvent RemoveTask = delegate { };

    public event TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.canceling"));
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;

        EnsureStagesAdded();
        StateChanged(TaskState.Waiting, i18n.T("download.install.workflow.tasks.queued"));
        _prepareStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_execution"), 0d);
        _downloadStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_support_files"), 0d);
        _finalizeStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);

        try
        {
            _prepareStage.Report(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"), 0.1d);
            StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"));
            Result = await prepareAsync(ReportProgress, executionToken);
            _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
            _downloadStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.support_files_completed"), 1d);
            _finalizeStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.completed"), 1d);
            PublishProgressStatus(1d, 0d, 0);
            StateChanged(TaskState.Success, i18n.T("download.install.workflow.tasks.completed"));
        }
        catch (OperationCanceledException)
        {
            MarkActiveStage(TaskState.Canceled, i18n.T("download.install.workflow.tasks.canceled"));
            PublishProgressStatus(_progress, 0d, _progressStatus.RemainingFileCount);
            StateChanged(TaskState.Canceled, i18n.T("download.install.workflow.tasks.canceled"));
            throw;
        }
        catch (Exception ex)
        {
            MarkActiveStage(TaskState.Failed, ex.Message);
            StateChanged(TaskState.Failed, ex.Message);
            throw;
        }
    }

    private void EnsureStagesAdded()
    {
        if (_stagesAdded)
        {
            return;
        }

        AddTask(_prepareStage);
        AddTask(_downloadStage);
        AddTask(_finalizeStage);
        _stagesAdded = true;
    }

    private void ReportProgress(FrontendUpdateInstallProgressSnapshot snapshot)
    {
        switch (snapshot.Phase)
        {
            case FrontendUpdateInstallProgressPhase.Prepare:
                _prepareStage.Report(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"), 0.5d);
                _downloadStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_support_files"), 0d);
                _finalizeStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);
                StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"));
                break;
            case FrontendUpdateInstallProgressPhase.Download:
                _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Running, BuildDownloadMessage(snapshot), ResolveDownloadProgress(snapshot));
                _finalizeStage.Report(TaskState.Waiting, i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);
                StateChanged(TaskState.Running, BuildDownloadMessage(snapshot));
                break;
            case FrontendUpdateInstallProgressPhase.Verify:
                _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Success, BuildFileReadyMessage(snapshot), 1d);
                _finalizeStage.Report(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"), 0.25d);
                StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.prepare_environment"));
                break;
            case FrontendUpdateInstallProgressPhase.Extract:
                _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Success, BuildFileReadyMessage(snapshot), 1d);
                _finalizeStage.Report(TaskState.Running, i18n.T("download.install.workflow.tasks.finalizing_installation"), 0.55d);
                StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.finalizing_installation"));
                break;
            case FrontendUpdateInstallProgressPhase.Finalize:
                _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Success, BuildFileReadyMessage(snapshot), 1d);
                _finalizeStage.Report(TaskState.Running, i18n.T("download.install.workflow.tasks.finalizing_installation"), 0.85d);
                StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.finalizing_installation"));
                break;
            case FrontendUpdateInstallProgressPhase.Completed:
                _prepareStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Success, BuildFileReadyMessage(snapshot), 1d);
                _finalizeStage.Report(TaskState.Success, i18n.T("download.install.workflow.tasks.completed"), 1d);
                StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.completed"));
                break;
        }

        RecalculateProgress();
        PublishProgressStatus(
            _progress,
            snapshot.SpeedBytesPerSecond,
            snapshot.Phase is FrontendUpdateInstallProgressPhase.Completed ? 0 : 1);
    }

    private string BuildDownloadMessage(FrontendUpdateInstallProgressSnapshot snapshot)
    {
        var fileName = string.IsNullOrWhiteSpace(snapshot.ArchivePath)
            ? Title
            : Path.GetFileName(snapshot.ArchivePath);
        return i18n.T(
            "download.install.workflow.tasks.progress_with_file",
            new Dictionary<string, object?>
            {
                ["prefix"] = i18n.T("setup.update.activities.download_install"),
                ["completed_count"] = FormatBytes(snapshot.DownloadedBytes),
                ["total_count"] = snapshot.TotalBytes > 0 ? FormatBytes(snapshot.TotalBytes) : "?",
                ["file_name"] = fileName
            });
    }

    private string BuildFileReadyMessage(FrontendUpdateInstallProgressSnapshot snapshot)
    {
        var fileName = string.IsNullOrWhiteSpace(snapshot.ArchivePath)
            ? Title
            : Path.GetFileName(snapshot.ArchivePath);
        return i18n.T(
            "download.install.workflow.tasks.processing_file",
            new Dictionary<string, object?> { ["file_name"] = fileName });
    }

    private static double ResolveDownloadProgress(FrontendUpdateInstallProgressSnapshot snapshot)
    {
        if (snapshot.TotalBytes <= 0)
        {
            return snapshot.DownloadedBytes > 0 ? 0.05d : 0d;
        }

        return Math.Clamp(snapshot.DownloadedBytes / (double)snapshot.TotalBytes, 0d, 1d);
    }

    private void MarkActiveStage(TaskState state, string message)
    {
        if (_finalizeStage.State == TaskState.Running)
        {
            _finalizeStage.Report(state, message, _finalizeStage.Progress);
        }
        else if (_downloadStage.State == TaskState.Running)
        {
            _downloadStage.Report(state, message, _downloadStage.Progress);
        }
        else
        {
            _prepareStage.Report(state, message, _prepareStage.Progress);
        }

        RecalculateProgress();
    }

    private void RecalculateProgress()
    {
        _progress = (_prepareStage.Progress + _downloadStage.Progress * 8d + _finalizeStage.Progress * 2d) / 11d;
        ProgressChanged(_progress);
        _progressStatus = _progressStatus with
        {
            ProgressText = $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%"
        };
        ProgressStatusChanged(_progressStatus);
    }

    private void PublishProgressStatus(double progress, double speedBytesPerSecond, int? remainingFileCount)
    {
        _progressStatus = new TaskProgressStatusSnapshot(
            $"{Math.Round(progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            speedBytesPerSecond > 0d
                ? $"{FormatBytes(speedBytesPerSecond)}/s"
                : i18n.T("launch.dialog.download_speed.zero"),
            remainingFileCount,
            null);
        ProgressStatusChanged(_progressStatus);
    }

    private static string FormatBytes(double value)
    {
        var absolute = Math.Max(value, 0d);
        return absolute switch
        {
            >= 1024d * 1024d * 1024d => $"{absolute / (1024d * 1024d * 1024d):0.##} GB",
            >= 1024d * 1024d => $"{absolute / (1024d * 1024d):0.##} MB",
            >= 1024d => $"{absolute / 1024d:0.##} KB",
            _ => $"{absolute:0} B"
        };
    }
}

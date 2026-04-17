using PCL.Core.App.Tasks;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private async Task<FrontendJavaRuntimeInstallResult> ExecuteManagedJavaRuntimeDownloadAsync(
        string taskTitle,
        FrontendJavaRuntimeInstallPlan installPlan,
        CancellationToken cancellationToken = default)
    {
        var downloadTask = new FrontendManagedJavaRuntimeDownloadTask(
            _i18n,
            taskTitle,
            (progress, taskCancellationToken) => _shellActionService.MaterializeJavaRuntime(
                installPlan,
                progress,
                taskCancellationToken));

        TaskCenter.Register(downloadTask, start: false);
        await downloadTask.ExecuteAsync(cancellationToken);
        return downloadTask.Result ?? throw new InvalidOperationException("The Java download task did not return a result.");
    }
}

internal sealed class FrontendManagedJavaRuntimeDownloadTask(
    II18nService i18n,
    string title,
    Func<Action<FrontendJavaRuntimeInstallProgressSnapshot>, CancellationToken, FrontendJavaRuntimeInstallResult> executeDownload)
    : ITask, ITaskProgressive, ITaskGroup, ITaskProgressStatus, ITaskCancelable
{
    private readonly II18nService _i18n = i18n;
    private readonly FrontendInstallStageTask _prepareStage = new(i18n.T("download.install.workflow.stages.prepare"));
    private readonly FrontendInstallStageTask _downloadStage = new(i18n.T("download.install.workflow.stages.support_files"));
    private readonly FrontendInstallStageTask _finalizeStage = new(i18n.T("download.install.workflow.stages.finalize"));
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", i18n.T("launch.dialog.download_speed.zero"), null, null);
    private double _progress;
    private bool _stagesAdded;

    public string Title { get; } = title;

    public FrontendJavaRuntimeInstallResult? Result { get; private set; }

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
        StateChanged(TaskState.Running, _i18n.T("download.install.workflow.tasks.canceling"));
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;
        EnsureStagesAdded();
        StateChanged(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.queued"));
        _prepareStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_execution"), 0d);
        _downloadStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_support_files"), 0d);
        _finalizeStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);

        try
        {
            _prepareStage.Report(TaskState.Running, _i18n.T("download.install.workflow.tasks.prepare_environment"), 0.1d);
            StateChanged(TaskState.Running, _i18n.T("download.install.workflow.tasks.prepare_environment"));
            Result = await Task.Run(() => executeDownload(ReportProgress, executionToken), executionToken);
            _prepareStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
            _downloadStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.support_files_completed"), 1d);
            _finalizeStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.completed"), 1d);
            PublishProgressStatus(1d, 0d, 0);
            StateChanged(TaskState.Success, $"Java saved to {Result.RuntimeDirectory}");
        }
        catch (OperationCanceledException)
        {
            MarkActiveStage(TaskState.Canceled, _i18n.T("download.install.workflow.tasks.canceled"));
            PublishProgressStatus(_progress, 0d, _progressStatus.RemainingFileCount);
            StateChanged(TaskState.Canceled, "Java download canceled");
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

    private void ReportProgress(FrontendJavaRuntimeInstallProgressSnapshot snapshot)
    {
        ApplyStageSnapshot(snapshot);
        _progress = snapshot.TotalDownloadBytes <= 0
            ? snapshot.TotalFileCount == 0
                ? 1d
                : Math.Clamp(snapshot.CompletedFileCount / (double)snapshot.TotalFileCount, 0d, 1d)
            : Math.Clamp(snapshot.DownloadedBytes / (double)snapshot.TotalDownloadBytes, 0d, 1d);

        ProgressChanged(_progress);
        PublishProgressStatus(
            _progress,
            snapshot.SpeedBytesPerSecond,
            Math.Max(snapshot.TotalFileCount - snapshot.CompletedFileCount, 0));

        if (!string.IsNullOrWhiteSpace(snapshot.CurrentFileRelativePath))
        {
            StateChanged(
                TaskState.Running,
                _i18n.T("download.install.workflow.tasks.processing_file", new Dictionary<string, object?> { ["file_name"] = snapshot.CurrentFileRelativePath }));
        }
    }

    private void ApplyStageSnapshot(FrontendJavaRuntimeInstallProgressSnapshot snapshot)
    {
        switch (snapshot.Phase)
        {
            case FrontendJavaRuntimeInstallPhase.Prepare:
                _prepareStage.Report(TaskState.Running, _i18n.T("download.install.workflow.tasks.prepare_environment"), 0.6d);
                _downloadStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_support_files"), 0d);
                _finalizeStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);
                break;
            case FrontendJavaRuntimeInstallPhase.Download:
                _prepareStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(
                    snapshot.CompletedFileCount >= snapshot.TotalFileCount && snapshot.TotalFileCount > 0 ? TaskState.Success : TaskState.Running,
                    BuildDownloadStageMessage(snapshot),
                    ResolveSnapshotProgress(snapshot));
                _finalizeStage.Report(TaskState.Waiting, _i18n.T("download.install.workflow.tasks.waiting_finalize"), 0d);
                break;
            case FrontendJavaRuntimeInstallPhase.Finalize:
                _prepareStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.prepare_environment"), 1d);
                _downloadStage.Report(TaskState.Success, _i18n.T("download.install.workflow.tasks.support_files_completed"), 1d);
                _finalizeStage.Report(TaskState.Running, _i18n.T("download.install.workflow.tasks.finalizing_installation"), 0.7d);
                break;
        }

        RecalculateProgress();
    }

    private string BuildDownloadStageMessage(FrontendJavaRuntimeInstallProgressSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.CurrentFileRelativePath))
        {
            return _i18n.T(
                "download.install.workflow.tasks.progress_with_file",
                new Dictionary<string, object?>
                {
                    ["prefix"] = _i18n.T("download.install.workflow.tasks.downloading_dependencies"),
                    ["completed_count"] = snapshot.CompletedFileCount,
                    ["total_count"] = Math.Max(snapshot.TotalFileCount, 1),
                    ["file_name"] = snapshot.CurrentFileRelativePath
                });
        }

        return _i18n.T(
            "download.install.workflow.tasks.progress_without_file",
            new Dictionary<string, object?>
            {
                ["prefix"] = _i18n.T("download.install.workflow.tasks.downloading_dependencies"),
                ["completed_count"] = snapshot.CompletedFileCount,
                ["total_count"] = Math.Max(snapshot.TotalFileCount, 1)
            });
    }

    private static double ResolveSnapshotProgress(FrontendJavaRuntimeInstallProgressSnapshot snapshot)
    {
        if (snapshot.TotalDownloadBytes > 0)
        {
            return Math.Clamp(snapshot.DownloadedBytes / (double)snapshot.TotalDownloadBytes, 0d, 1d);
        }

        if (snapshot.TotalFileCount > 0)
        {
            return Math.Clamp(snapshot.CompletedFileCount / (double)snapshot.TotalFileCount, 0d, 1d);
        }

        return 0d;
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
        _progress =
            (_prepareStage.Progress * 1d +
             _downloadStage.Progress * 6d +
             _finalizeStage.Progress * 2d) / 9d;
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
                : _i18n.T("launch.dialog.download_speed.zero"),
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

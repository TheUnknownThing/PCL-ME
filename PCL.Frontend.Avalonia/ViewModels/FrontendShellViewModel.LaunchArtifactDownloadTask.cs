using PCL.Core.App.I18n;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed class FrontendManagedLaunchArtifactDownloadTask(
    II18nService i18n,
    string title,
    Func<Action<FrontendShellActionService.FrontendLaunchArtifactProgressSnapshot>, CancellationToken, Task> executeAsync)
    : ITask, ITaskProgressive, ITaskProgressStatus, ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", i18n.T("launch.dialog.download_speed.zero"), null, null);
    private double _progress;

    public string Title { get; } = title;

    public TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

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

        StateChanged(TaskState.Waiting, i18n.T("download.install.workflow.tasks.queued"));

        try
        {
            StateChanged(TaskState.Running, i18n.T("download.install.workflow.tasks.downloading_dependencies"));
            await executeAsync(ReportProgress, executionToken);
            PublishProgress(1d, 0d, 0);
            StateChanged(TaskState.Success, i18n.T("download.install.workflow.tasks.completed"));
        }
        catch (OperationCanceledException)
        {
            PublishProgress(_progress, 0d, _progressStatus.RemainingFileCount);
            StateChanged(TaskState.Canceled, i18n.T("download.install.workflow.tasks.canceled"));
            throw;
        }
        catch (Exception ex)
        {
            StateChanged(TaskState.Failed, ex.Message);
            throw;
        }
    }

    private void ReportProgress(FrontendShellActionService.FrontendLaunchArtifactProgressSnapshot snapshot)
    {
        var progress = snapshot.TotalFileCount <= 0
            ? 1d
            : Math.Clamp(snapshot.CompletedFileCount / (double)snapshot.TotalFileCount, 0d, 1d);
        var remainingFiles = snapshot.TotalFileCount <= 0
            ? 0
            : Math.Max(snapshot.TotalFileCount - snapshot.CompletedFileCount, 0);
        PublishProgress(progress, snapshot.SpeedBytesPerSecond, remainingFiles);

        var message = string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
            ? i18n.T("download.install.workflow.tasks.downloading_dependencies")
            : i18n.T(
                "download.install.workflow.tasks.progress_with_file",
                new Dictionary<string, object?>
                {
                    ["prefix"] = i18n.T("download.install.workflow.tasks.downloading_dependencies"),
                    ["completed_count"] = snapshot.CompletedFileCount,
                    ["total_count"] = Math.Max(snapshot.TotalFileCount, 1),
                    ["file_name"] = snapshot.CurrentFileName
                });
        StateChanged(TaskState.Running, message);
    }

    private void PublishProgress(double progress, double speedBytesPerSecond, int? remainingFileCount)
    {
        _progress = Math.Clamp(progress, 0d, 1d);
        ProgressChanged(_progress);
        _progressStatus = new TaskProgressStatusSnapshot(
            $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
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

using PCL.Core.App.Tasks;
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
            taskTitle,
            (progress, taskCancellationToken) => _shellActionService.MaterializeJavaRuntime(
                installPlan,
                progress,
                taskCancellationToken));

        TaskCenter.Register(downloadTask, start: false);
        await downloadTask.ExecuteAsync(cancellationToken);
        return downloadTask.Result ?? throw new InvalidOperationException("Java 下载任务未返回结果。");
    }
}

internal sealed class FrontendManagedJavaRuntimeDownloadTask(
    string title,
    Func<Action<FrontendJavaRuntimeInstallProgressSnapshot>, CancellationToken, FrontendJavaRuntimeInstallResult> executeDownload)
    : ITask, ITaskProgressive, ITaskProgressStatus, ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", 0, null);
    private double _progress;

    public string Title { get; } = title;

    public FrontendJavaRuntimeInstallResult? Result { get; private set; }

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
        StateChanged(TaskState.Running, "正在取消 Java 下载…");
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;
        StateChanged(TaskState.Waiting, "已加入任务中心");

        try
        {
            StateChanged(TaskState.Running, "正在下载 Java 运行时…");
            Result = await Task.Run(() => executeDownload(ReportProgress, executionToken), executionToken);
            PublishProgressStatus(1d, 0d, 0);
            StateChanged(TaskState.Success, $"Java 已保存到 {Result.RuntimeDirectory}");
        }
        catch (OperationCanceledException)
        {
            PublishProgressStatus(_progress, 0d, _progressStatus.RemainingFileCount);
            StateChanged(TaskState.Canceled, "Java 下载已取消");
            throw;
        }
        catch (Exception ex)
        {
            StateChanged(TaskState.Failed, ex.Message);
            throw;
        }
    }

    private void ReportProgress(FrontendJavaRuntimeInstallProgressSnapshot snapshot)
    {
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
            StateChanged(TaskState.Running, $"正在下载 {snapshot.CurrentFileRelativePath}…");
        }
    }

    private void PublishProgressStatus(double progress, double speedBytesPerSecond, int? remainingFileCount)
    {
        _progressStatus = new TaskProgressStatusSnapshot(
            $"{Math.Round(progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            $"{FormatBytes(speedBytesPerSecond)}/s",
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

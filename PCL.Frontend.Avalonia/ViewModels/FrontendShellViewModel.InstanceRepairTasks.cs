using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private async Task<FrontendInstanceRepairResult> ExecuteManagedInstanceRepairAsync(
        string taskTitle,
        FrontendInstanceRepairRequest request,
        Action<FrontendInstanceRepairTelemetrySnapshot>? onTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        var repairTask = new FrontendManagedInstanceRepairTask(
            taskTitle,
            (taskTelemetry, taskCancellationToken) =>
            {
                void PublishTelemetry(FrontendInstanceRepairTelemetrySnapshot snapshot)
                {
                    taskTelemetry(snapshot);
                    onTelemetry?.Invoke(snapshot);
                }

                return _shellActionService.RepairInstance(request, PublishTelemetry, taskCancellationToken);
            });

        TaskCenter.Register(repairTask, start: false);
        await repairTask.ExecuteAsync(cancellationToken);
        return repairTask.Result ?? throw new InvalidOperationException("实例修复任务未返回结果。");
    }
}

internal sealed class FrontendManagedInstanceRepairTask(
    string title,
    Func<Action<FrontendInstanceRepairTelemetrySnapshot>, CancellationToken, FrontendInstanceRepairResult> executeRepair)
    : ITask, ITaskProgressive, ITaskGroup, ITaskTelemetry, ITaskCancelable
{
    private readonly FrontendInstallStageTask _supportStage = new("补全游戏主文件与支持库");
    private readonly FrontendInstallStageTask _assetStage = new("补全游戏资源文件");
    private readonly CancellationTokenSource _cancellation = new();
    private bool _stagesAdded;
    private double _progress;
    private TaskTelemetrySnapshot _telemetry = new("0%", "0 B/s", null, null);
    private FrontendInstanceRepairTelemetrySnapshot? _lastSnapshot;

    public string Title { get; } = title;

    public FrontendInstanceRepairResult? Result { get; private set; }

    public TaskTelemetrySnapshot Telemetry => _telemetry;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskGroupEvent AddTask = delegate { };

    public event TaskGroupEvent RemoveTask = delegate { };

    public event TaskTelemetryEvent TelemetryChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        StateChanged(TaskState.Running, "正在取消实例文件校验…");
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;

        EnsureStagesAdded();
        StateChanged(TaskState.Waiting, "已加入任务中心");
        _supportStage.Report(TaskState.Waiting, "等待校验支持文件…", 0d);
        _assetStage.Report(TaskState.Waiting, "等待校验资源文件…", 0d);

        try
        {
            StateChanged(TaskState.Running, "正在校验并补全实例文件…");
            Result = await Task.Run(() => executeRepair(ApplyTelemetrySnapshot, executionToken), executionToken);
            CompleteSuccessfully(Result);
        }
        catch (OperationCanceledException)
        {
            PublishTelemetry(new TaskTelemetrySnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                "0 B/s",
                _telemetry.RemainingFileCount,
                null));
            MarkCancellation();
            StateChanged(TaskState.Canceled, "实例文件校验已取消");
            throw;
        }
        catch (Exception ex)
        {
            MarkFailure(ex.Message);
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

        AddTask(_supportStage);
        AddTask(_assetStage);
        _stagesAdded = true;
    }

    private void ApplyTelemetrySnapshot(FrontendInstanceRepairTelemetrySnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        var supportSnapshot = BuildMergedGroupSnapshot(
            snapshot,
            FrontendInstanceRepairFileGroup.Client,
            FrontendInstanceRepairFileGroup.Libraries,
            FrontendInstanceRepairFileGroup.AssetIndex);
        var assetSnapshot = GetGroupSnapshot(snapshot, FrontendInstanceRepairFileGroup.Assets);

        UpdateStageFromGroup(
            _supportStage,
            supportSnapshot,
            "正在补全游戏主文件与支持库…",
            "无需下载游戏支持文件");
        UpdateStageFromGroup(
            _assetStage,
            assetSnapshot,
            "正在下载游戏资源文件…",
            "无需下载游戏资源文件");

        _progress = snapshot.Progress;
        ProgressChanged(_progress);
        PublishTelemetry(new TaskTelemetrySnapshot(
            $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            snapshot.SpeedBytesPerSecond > 0d ? $"{FormatBytes(snapshot.SpeedBytesPerSecond)}/s" : "0 B/s",
            snapshot.RemainingFileCount,
            null));

        StateChanged(
            TaskState.Running,
            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? "正在校验实例文件…"
                : $"正在处理 {snapshot.CurrentFileName}");
    }

    private void CompleteSuccessfully(FrontendInstanceRepairResult result)
    {
        var supportSnapshot = _lastSnapshot is null
            ? new FrontendInstanceRepairGroupSnapshot(FrontendInstanceRepairFileGroup.Client, 0, 0, 0, 0, string.Empty)
            : BuildMergedGroupSnapshot(
                _lastSnapshot,
                FrontendInstanceRepairFileGroup.Client,
                FrontendInstanceRepairFileGroup.Libraries,
                FrontendInstanceRepairFileGroup.AssetIndex);
        var assetSnapshot = _lastSnapshot is null
            ? new FrontendInstanceRepairGroupSnapshot(FrontendInstanceRepairFileGroup.Assets, 0, 0, 0, 0, string.Empty)
            : GetGroupSnapshot(_lastSnapshot, FrontendInstanceRepairFileGroup.Assets);

        _supportStage.Report(
            TaskState.Success,
            supportSnapshot.TotalFiles == 0
                ? "无需下载游戏支持文件"
                : $"游戏支持文件已就绪 ({supportSnapshot.CompletedFiles}/{supportSnapshot.TotalFiles})",
            1d);
        _assetStage.Report(
            TaskState.Success,
            assetSnapshot.TotalFiles == 0
                ? "无需下载游戏资源文件"
                : $"游戏资源文件已就绪 ({assetSnapshot.CompletedFiles}/{assetSnapshot.TotalFiles})",
            1d);

        _progress = 1d;
        ProgressChanged(_progress);
        PublishTelemetry(new TaskTelemetrySnapshot("100%", "0 B/s", 0, null));
        StateChanged(
            TaskState.Success,
            $"已下载 {result.DownloadedFiles.Count} 个文件，复用 {result.ReusedFiles.Count} 个文件。");
    }

    private void MarkCancellation()
    {
        var runningStage = GetRunningStage();
        runningStage?.Report(TaskState.Canceled, "任务已取消", runningStage.Progress);

        if (_supportStage.State == TaskState.Waiting)
        {
            _supportStage.Report(TaskState.Canceled, "任务已取消", _supportStage.Progress);
        }

        if (_assetStage.State == TaskState.Waiting)
        {
            _assetStage.Report(TaskState.Canceled, "任务已取消", _assetStage.Progress);
        }
    }

    private void MarkFailure(string message)
    {
        var failingStage = GetRunningStage() ?? _assetStage;
        failingStage.Report(TaskState.Failed, message, failingStage.Progress);
    }

    private FrontendInstallStageTask? GetRunningStage()
    {
        if (_assetStage.State == TaskState.Running)
        {
            return _assetStage;
        }

        if (_supportStage.State == TaskState.Running)
        {
            return _supportStage;
        }

        return null;
    }

    private static void UpdateStageFromGroup(
        FrontendInstallStageTask stage,
        FrontendInstanceRepairGroupSnapshot snapshot,
        string activePrefix,
        string emptyMessage)
    {
        if (snapshot.TotalFiles == 0)
        {
            stage.Report(TaskState.Success, emptyMessage, 1d);
            return;
        }

        var message = snapshot.Progress >= 0.999
            ? $"{snapshot.CompletedFiles}/{snapshot.TotalFiles} 个文件已就绪"
            : string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? $"{activePrefix} {snapshot.CompletedFiles}/{snapshot.TotalFiles}"
                : $"{activePrefix} {snapshot.CompletedFiles}/{snapshot.TotalFiles} • {snapshot.CurrentFileName}";
        stage.Report(snapshot.Progress >= 0.999 ? TaskState.Success : TaskState.Running, message, snapshot.Progress);
    }

    private void PublishTelemetry(TaskTelemetrySnapshot snapshot)
    {
        _telemetry = snapshot;
        TelemetryChanged(snapshot);
    }

    private static FrontendInstanceRepairGroupSnapshot BuildMergedGroupSnapshot(
        FrontendInstanceRepairTelemetrySnapshot snapshot,
        params FrontendInstanceRepairFileGroup[] groups)
    {
        var available = groups
            .Select(group => GetGroupSnapshot(snapshot, group))
            .Where(group => group.TotalFiles > 0)
            .ToArray();
        if (available.Length == 0)
        {
            return new FrontendInstanceRepairGroupSnapshot(
                FrontendInstanceRepairFileGroup.Client,
                0,
                0,
                0,
                0,
                string.Empty);
        }

        var active = available.FirstOrDefault(group => group.Progress < 0.999) ?? available.Last();
        return new FrontendInstanceRepairGroupSnapshot(
            FrontendInstanceRepairFileGroup.Client,
            available.Sum(group => group.CompletedFiles),
            available.Sum(group => group.TotalFiles),
            available.Sum(group => group.CompletedBytes),
            available.Sum(group => group.TotalBytes),
            active.CurrentFileName);
    }

    private static FrontendInstanceRepairGroupSnapshot GetGroupSnapshot(
        FrontendInstanceRepairTelemetrySnapshot snapshot,
        FrontendInstanceRepairFileGroup group)
    {
        return snapshot.Groups.TryGetValue(group, out var value)
            ? value
            : new FrontendInstanceRepairGroupSnapshot(group, 0, 0, 0, 0, string.Empty);
    }

    private static string FormatBytes(double value)
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

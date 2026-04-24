using System.Collections.Generic;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private async Task<FrontendInstanceRepairResult> ExecuteManagedInstanceRepairAsync(
        string taskTitle,
        FrontendInstanceRepairRequest request,
        Action<FrontendInstanceRepairProgressSnapshot>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var repairTask = new FrontendManagedInstanceRepairTask(
            _i18n,
            taskTitle,
            (taskProgress, taskCancellationToken) =>
            {
                void PublishProgress(FrontendInstanceRepairProgressSnapshot snapshot)
                {
                    taskProgress(snapshot);
                    onProgress?.Invoke(snapshot);
                }

                return _launcherActionService.RepairInstance(request, PublishProgress, taskCancellationToken);
            });

        TaskCenter.Register(repairTask, start: false);
        await repairTask.ExecuteAsync(cancellationToken);
        return repairTask.Result ?? throw new InvalidOperationException("The instance repair task did not return a result.");
    }
}

internal sealed class FrontendManagedInstanceRepairTask(
    II18nService i18n,
    string title,
    Func<Action<FrontendInstanceRepairProgressSnapshot>, CancellationToken, FrontendInstanceRepairResult> executeRepair)
    : ITask, ITaskProgressive, ITaskGroup, ITaskProgressStatus, ITaskCancelable
{
    private readonly II18nService _i18n = i18n;
    private readonly FrontendInstallStageTask _supportStage = new(i18n.T("download.install.workflow.tasks.repairing_game_files"));
    private readonly FrontendInstallStageTask _assetStage = new(i18n.T("download.install.workflow.tasks.repairing_support_files"));
    private readonly CancellationTokenSource _cancellation = new();
    private bool _stagesAdded;
    private double _progress;
    private FrontendInstanceRepairProgressSnapshot? _lastSnapshot;
    private TaskProgressStatusSnapshot _progressStatus = new("0%", i18n.T("launch.dialog.download_speed.zero"), null, null);

    public string Title { get; } = title;

    public FrontendInstanceRepairResult? Result { get; private set; }

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
        StateChanged(TaskState.Running, T("download.install.workflow.tasks.canceling"));
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var executionToken = linkedCancellation.Token;

        EnsureStagesAdded();
        StateChanged(TaskState.Waiting, T("download.install.workflow.tasks.queued"));
        _supportStage.Report(TaskState.Waiting, T("download.install.workflow.tasks.waiting_support_files"), 0d);
        _assetStage.Report(TaskState.Waiting, T("download.install.workflow.tasks.waiting_asset_files"), 0d);

        try
        {
            StateChanged(TaskState.Running, T("download.install.workflow.tasks.repairing_game_files"));
            Result = await Task.Run(() => executeRepair(ApplyProgressSnapshot, executionToken), executionToken);
            CompleteSuccessfully(Result);
        }
        catch (OperationCanceledException)
        {
            PublishProgressStatus(
                new TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    T("launch.dialog.download_speed.zero"),
                    _progressStatus.RemainingFileCount,
                    null));
            MarkCancellation();
            StateChanged(TaskState.Canceled, T("download.install.workflow.tasks.canceled"));
            throw;
        }
        catch (Exception ex)
        {
            PublishProgressStatus(
                new TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    T("launch.dialog.download_speed.zero"),
                    _progressStatus.RemainingFileCount,
                    null));
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

    private void ApplyProgressSnapshot(FrontendInstanceRepairProgressSnapshot snapshot)
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
            T("download.install.workflow.tasks.repairing_game_files"),
            T("download.install.workflow.tasks.no_support_files_to_repair"));
        UpdateStageFromGroup(
            _assetStage,
            assetSnapshot,
            T("download.install.workflow.tasks.downloading_asset_files"),
            T("download.install.workflow.tasks.no_asset_files_to_download"));

        _progress = snapshot.Progress;
        ProgressChanged(_progress);
        PublishProgressStatus(
            new TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                snapshot.SpeedBytesPerSecond > 0d ? $"{FormatBytes(snapshot.SpeedBytesPerSecond)}/s" : T("launch.dialog.download_speed.zero"),
                snapshot.RemainingFileCount,
                null));

        StateChanged(
            TaskState.Running,
            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? T("download.install.workflow.tasks.verifying_instance_files")
                : T("download.install.workflow.tasks.processing_file", ("file_name", snapshot.CurrentFileName)));
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
                ? T("download.install.workflow.tasks.no_support_files_to_repair")
                : T("download.install.workflow.tasks.support_files_completed"),
            1d);
        _assetStage.Report(
            TaskState.Success,
            assetSnapshot.TotalFiles == 0
                ? T("download.install.workflow.tasks.no_asset_files_to_download")
                : T("download.install.workflow.tasks.asset_files_completed"),
            1d);

        _progress = 1d;
        ProgressChanged(_progress);
        PublishProgressStatus(new TaskProgressStatusSnapshot("100%", T("launch.dialog.download_speed.zero"), 0, null));
        StateChanged(TaskState.Success, T("download.install.workflow.tasks.completed"));
    }

    private void MarkCancellation()
    {
        var runningStage = GetRunningStage();
        runningStage?.Report(TaskState.Canceled, T("download.install.workflow.tasks.canceled"), runningStage.Progress);

        if (_supportStage.State == TaskState.Waiting)
        {
            _supportStage.Report(TaskState.Canceled, T("download.install.workflow.tasks.canceled"), _supportStage.Progress);
        }

        if (_assetStage.State == TaskState.Waiting)
        {
            _assetStage.Report(TaskState.Canceled, T("download.install.workflow.tasks.canceled"), _assetStage.Progress);
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

    private void UpdateStageFromGroup(
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
            ? T("download.install.workflow.tasks.files_ready", ("completed_count", snapshot.CompletedFiles), ("total_count", snapshot.TotalFiles))
            : string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                ? T("download.install.workflow.tasks.progress_without_file", ("prefix", activePrefix), ("completed_count", snapshot.CompletedFiles), ("total_count", snapshot.TotalFiles))
                : T("download.install.workflow.tasks.progress_with_file", ("prefix", activePrefix), ("completed_count", snapshot.CompletedFiles), ("total_count", snapshot.TotalFiles), ("file_name", snapshot.CurrentFileName));
        stage.Report(snapshot.Progress >= 0.999 ? TaskState.Success : TaskState.Running, message, snapshot.Progress);
    }

    private void PublishProgressStatus(TaskProgressStatusSnapshot snapshot)
    {
        _progressStatus = snapshot;
        ProgressStatusChanged(snapshot);
    }

    private static FrontendInstanceRepairGroupSnapshot BuildMergedGroupSnapshot(
        FrontendInstanceRepairProgressSnapshot snapshot,
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
        FrontendInstanceRepairProgressSnapshot snapshot,
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

    private string T(string key) => _i18n.T(key);

    private string T(string key, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0)
        {
            return _i18n.T(key);
        }

        var values = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            values[name] = value;
        }

        return _i18n.T(key, values);
    }

}

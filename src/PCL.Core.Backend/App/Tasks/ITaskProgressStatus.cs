namespace PCL.Core.App.Tasks;

public sealed record TaskProgressStatusSnapshot(
    string ProgressText,
    string SpeedText,
    int? RemainingFileCount,
    int? RemainingThreadCount);

public delegate void TaskProgressStatusEvent(TaskProgressStatusSnapshot snapshot);

public interface ITaskProgressStatus
{
    public TaskProgressStatusSnapshot ProgressStatus { get; }

    public event TaskProgressStatusEvent ProgressStatusChanged;
}

namespace PCL.Core.App.Tasks;

public sealed record TaskTelemetrySnapshot(
    string ProgressText,
    string SpeedText,
    int? RemainingFileCount,
    int? RemainingThreadCount);

public delegate void TaskTelemetryEvent(TaskTelemetrySnapshot snapshot);

public interface ITaskTelemetry
{
    public TaskTelemetrySnapshot Telemetry { get; }

    public event TaskTelemetryEvent TelemetryChanged;
}

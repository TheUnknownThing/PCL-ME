namespace PCL.Core.Utils.Processes;

public sealed class ProcessStartRequest(string fileName)
{
    public string FileName { get; init; } = fileName;
    public string? Arguments { get; init; }
    public bool UseShellExecute { get; init; }
    public bool CreateNoWindow { get; init; }
}

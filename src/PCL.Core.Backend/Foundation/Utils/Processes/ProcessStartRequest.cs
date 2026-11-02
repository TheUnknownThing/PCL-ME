using System.Collections.Generic;

namespace PCL.Core.Utils.Processes;

public sealed class ProcessStartRequest(string fileName)
{
    public string FileName { get; init; } = fileName;
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public bool UseShellExecute { get; init; }
    public bool CreateNoWindow { get; init; }
    public bool RedirectStandardOutput { get; init; }
    public bool RedirectStandardError { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}

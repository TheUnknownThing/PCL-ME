using System.Diagnostics;

namespace PCL.Core.Utils.Processes;

public interface IProcessManager
{
    Process? Start(ProcessStartRequest request);
    int Kill(Process process, int timeout = 3000, bool force = false);
    string? GetExecutablePath(Process process);
}

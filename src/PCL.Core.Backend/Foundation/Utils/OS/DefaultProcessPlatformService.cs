using System;
using System.Diagnostics;
using PCL.Core.Utils.Processes;

namespace PCL.Core.Utils.OS;

internal sealed class DefaultProcessPlatformService : IProcessPlatformService
{
    public bool IsAdmin() => false;

    public string? GetCommandLine(int processId) => null;

    public Process? StartAsAdmin(string path, string? arguments)
    {
        return SystemProcessManager.Current.Start(new ProcessStartRequest(path)
        {
            Arguments = arguments
        });
    }

    public void SetGpuPreference(string executable, bool wantHighPerformance)
    {
        throw new PlatformNotSupportedException("GPU preference changes require Windows.");
    }
}

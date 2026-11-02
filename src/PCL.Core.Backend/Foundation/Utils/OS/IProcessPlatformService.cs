using System.Diagnostics;

namespace PCL.Core.Utils.OS;

internal interface IProcessPlatformService
{
    bool IsAdmin();
    string? GetCommandLine(int processId);
    Process? StartAsAdmin(string path, string? arguments);
    void SetGpuPreference(string executable, bool wantHighPerformance);
}

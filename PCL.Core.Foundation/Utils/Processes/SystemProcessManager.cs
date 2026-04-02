using System.Diagnostics;
using System.IO;

namespace PCL.Core.Utils.Processes;

public sealed class SystemProcessManager : IProcessManager
{
    public static SystemProcessManager Current { get; } = new();

    public Process? Start(ProcessStartRequest request)
    {
        var psi = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = request.UseShellExecute,
            CreateNoWindow = request.CreateNoWindow
        };
        if (request.Arguments != null) psi.Arguments = request.Arguments;
        return Process.Start(psi);
    }

    public int Kill(Process process, int timeout = 3000, bool force = false)
    {
        if (force) process.Kill(true);
        else process.Kill();

        if (timeout == -1) process.WaitForExit();
        else if (timeout != 0) process.WaitForExit(timeout);

        return process.HasExited ? process.ExitCode : int.MinValue;
    }

    public string? GetExecutablePath(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return path == null ? null : Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }
}

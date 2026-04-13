using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory)) psi.WorkingDirectory = request.WorkingDirectory;
        if (!request.UseShellExecute)
        {
            psi.RedirectStandardOutput = request.RedirectStandardOutput;
            psi.RedirectStandardError = request.RedirectStandardError;
            if (request.EnvironmentVariables is not null)
            {
                foreach (var environmentVariable in request.EnvironmentVariables)
                {
                    psi.EnvironmentVariables[environmentVariable.Key] = environmentVariable.Value;
                }
            }
        }
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
            if (!string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(path);
            }
        }
        catch
        {
        }

        if (_HasExited(process))
        {
            return null;
        }

        return _ResolveFromStartInfo(process.StartInfo.FileName);
    }

    private static bool _HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static string? _ResolveFromStartInfo(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? Path.GetFullPath(fileName) : null;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar))
        {
            return null;
        }

        string[] candidates = OperatingSystem.IsWindows() && Path.GetExtension(fileName).Length == 0
            ? [fileName, fileName + ".exe"]
            : [fileName];

        foreach (var directory in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var resolved = candidates
                .Select(candidate => Path.Combine(directory, candidate))
                .FirstOrDefault(File.Exists);
            if (resolved != null)
            {
                return Path.GetFullPath(resolved);
            }
        }

        return null;
    }
}

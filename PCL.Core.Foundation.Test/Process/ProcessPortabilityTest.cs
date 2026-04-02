using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Processes;

namespace PCL.Core.Test;

[TestClass]
public class ProcessPortabilityTest
{
    private readonly IProcessManager _manager = SystemProcessManager.Current;

    [TestMethod]
    public void SystemProcessManager_StartsChildProcessWithArguments()
    {
        using var process = _manager.Start(new ProcessStartRequest(GetDotnetCommand())
        {
            Arguments = "--version",
            CreateNoWindow = true
        });

        Assert.IsNotNull(process);
        Assert.IsTrue(process.WaitForExit(10000));
        Assert.AreEqual(0, process.ExitCode);
    }

    [TestMethod]
    public void SystemProcessManager_KillStopsProcessWithoutForce()
    {
        using var process = StartLongRunningProcess();

        var exitCode = _manager.Kill(process, timeout: -1, force: false);

        Assert.AreNotEqual(int.MinValue, exitCode);
        Assert.IsTrue(process.HasExited);
    }

    [TestMethod]
    public void SystemProcessManager_KillStopsProcessWithForce()
    {
        using var process = StartLongRunningProcess();

        var exitCode = _manager.Kill(process, timeout: -1, force: true);

        Assert.AreNotEqual(int.MinValue, exitCode);
        Assert.IsTrue(process.HasExited);
    }

    [TestMethod]
    public void SystemProcessManager_KillTimeoutOnlyReturnsMinValueWhileProcessIsAlive()
    {
        using var process = StartLongRunningProcess();

        var exitCode = _manager.Kill(process, timeout: 0, force: false);

        Assert.AreEqual(process.HasExited, exitCode != int.MinValue);
        if (!process.HasExited)
        {
            process.Kill(true);
            process.WaitForExit(10000);
        }
    }

    [TestMethod]
    public void SystemProcessManager_GetExecutablePathReturnsAbsolutePathForLiveProcess()
    {
        using var process = StartLongRunningProcess();

        var path = _manager.GetExecutablePath(process);

        Assert.IsNotNull(path);
        Assert.IsTrue(Path.IsPathRooted(path));

        process.Kill(true);
        process.WaitForExit(10000);
    }

    [TestMethod]
    public void SystemProcessManager_GetExecutablePathReturnsNullWhenProcessCannotBeRead()
    {
        var process = StartLongRunningProcess();
        process.Kill(true);
        process.WaitForExit(10000);
        process.Dispose();

        var path = _manager.GetExecutablePath(process);

        Assert.IsNull(path);
    }

    private static Process StartLongRunningProcess()
    {
        var request = OperatingSystem.IsWindows()
            ? new ProcessStartRequest("cmd.exe")
            {
                Arguments = "/c ping 127.0.0.1 -n 30 > nul",
                CreateNoWindow = true
            }
            : new ProcessStartRequest("/bin/sh")
            {
                Arguments = "-c \"sleep 30\"",
                CreateNoWindow = true
            };

        return SystemProcessManager.Current.Start(request)
               ?? throw new InvalidOperationException("Failed to start test process");
    }

    private static string GetDotnetCommand()
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath))
        {
            return hostPath;
        }

        using var current = Process.GetCurrentProcess();
        var currentPath = current.MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(currentPath);
            if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return currentPath;
            }
        }

        return "dotnet";
    }
}

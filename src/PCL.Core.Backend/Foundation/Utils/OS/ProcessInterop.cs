using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using PCL.Core.Logging;
using PCL.Core.Utils.Processes;

namespace PCL.Core.Utils.OS;

public class ProcessInterop
{
    private static readonly IProcessManager ProcessManager = SystemProcessManager.Current;
    private static readonly IProcessPlatformService PlatformService = ProcessPlatformServiceProvider.Current;

    public static bool IsAdmin() => PlatformService.IsAdmin();

    public static string? GetCommandLine(int processId) => PlatformService.GetCommandLine(processId);

    public static Process? Start(string path, string? arguments = null, bool runAsAdmin = false)
    {
        if (!runAsAdmin)
        {
            return ProcessManager.Start(new ProcessStartRequest(path)
            {
                Arguments = arguments
            });
        }

        return PlatformService.StartAsAdmin(path, arguments);
    }

    public static string? GetExecutablePath(Process process) => ProcessManager.GetExecutablePath(process);

    public static Process? StartAsAdmin(string path, string? arguments = null) => PlatformService.StartAsAdmin(path, arguments);

    public static int Kill(Process process, int timeout = 3000, bool force = false) =>
        ProcessManager.Kill(process, timeout, force);

    public static void SetGpuPreference(string executable, bool wantHighPerformance = true)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("The executable path cannot be empty or whitespace.", nameof(executable));
        }

        try
        {
            var fullPath = Path.GetFullPath(executable);
            if (!File.Exists(fullPath))
            {
                LogWrapper.Warn("System", $"The specified executable does not exist: {executable}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"The executable path is invalid: {executable}", nameof(executable), ex);
        }

        try
        {
            PlatformService.SetGpuPreference(executable, wantHighPerformance);
        }
        catch (UnauthorizedAccessException ex)
        {
            const string errorMessage = "Insufficient permission to access the registry. Run the program as administrator or check user permissions.";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new UnauthorizedAccessException(errorMessage, ex);
        }
        catch (SecurityException ex)
        {
            const string errorMessage = "Security policy does not allow registry access. Contact the system administrator to review security settings.";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new SecurityException(errorMessage, ex);
        }
        catch (Exception ex)
        {
            var errorMessage = $"An unexpected error occurred while setting GPU preference: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }
}

public enum ProcessExitCode
{
    TaskDone = 0,
    Failed = 1,
    Canceled = 2,
    AccessDenied = 5
}

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
            throw new ArgumentException("可执行文件路径不能为空或仅包含空白字符", nameof(executable));
        }

        try
        {
            var fullPath = Path.GetFullPath(executable);
            if (!File.Exists(fullPath))
            {
                LogWrapper.Warn("System", $"指定的可执行文件不存在: {executable}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"无效的可执行文件路径: {executable}", nameof(executable), ex);
        }

        try
        {
            PlatformService.SetGpuPreference(executable, wantHighPerformance);
        }
        catch (UnauthorizedAccessException ex)
        {
            const string errorMessage = "没有足够的权限访问注册表。请以管理员身份运行程序或检查用户权限设置。";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new UnauthorizedAccessException(errorMessage, ex);
        }
        catch (SecurityException ex)
        {
            const string errorMessage = "安全策略不允许访问注册表。请联系系统管理员检查安全设置。";
            LogWrapper.Error(ex, "System", errorMessage);
            throw new SecurityException(errorMessage, ex);
        }
        catch (Exception ex)
        {
            var errorMessage = $"设置 GPU 偏好时发生未预期的错误: {ex.Message}";
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

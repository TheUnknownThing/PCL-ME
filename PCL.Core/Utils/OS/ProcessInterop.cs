using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using PCL.Core.Logging;
using PCL.Core.Utils.Processes;

namespace PCL.Core.Utils.OS;

public class ProcessInterop {
    private static readonly IProcessManager _processManager = SystemProcessManager.Current;
    private static readonly IProcessPlatformService _platformService = ProcessPlatformServiceProvider.Current;

    /// <summary>
    /// 检查当前程序是否以管理员权限运行。
    /// </summary>
    /// <returns>如果当前用户具有管理员权限，则返回 true；否则返回 false。</returns>
    public static bool IsAdmin() => _platformService.IsAdmin();

    /// <summary>
    /// 获取指定进程 ID 的命令行参数。
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>命令行参数文本</returns>
    public static string? GetCommandLine(int processId) => _platformService.GetCommandLine(processId);

    /// <summary>
    /// 从本地可执行文件启动新的进程。
    /// </summary>
    /// <param name="path">可执行文件路径</param>
    /// <param name="arguments">程序参数</param>
    /// <param name="runAsAdmin">指定是否以管理员身份启动该进程</param>
    /// <returns>新的进程实例</returns>
    public static Process? Start(string path, string? arguments = null, bool runAsAdmin = false) {
        if (!runAsAdmin) {
            return _processManager.Start(new ProcessStartRequest(path) {
                Arguments = arguments
            });
        }

        return _platformService.StartAsAdmin(path, arguments);
    }

    /// <summary>
    /// 获取指定进程的可执行文件路径
    /// </summary>
    /// <param name="process">进程实例</param>
    /// <returns>可执行文件路径，若无法获取则为 <c>null</c></returns>
    public static string? GetExecutablePath(Process process) => _processManager.GetExecutablePath(process);

    /// <summary>
    /// 从本地可执行文件以管理员身份启动新的进程。<see cref="Start"/> 的套壳。
    /// </summary>
    /// <param name="path">可执行文件路径</param>
    /// <param name="arguments">程序参数</param>
    /// <returns>新的进程实例</returns>
    public static Process? StartAsAdmin(string path, string? arguments = null) => _platformService.StartAsAdmin(path, arguments);

    /// <summary>
    /// 结束指定进程。
    /// </summary>
    /// <param name="process">要结束的进程实例</param>
    /// <param name="timeout">等待进程退出超时，以毫秒为单位，-1 表示无限制</param>
    /// <param name="force">指定是否强制结束，若为 <c>true</c> 将尝试结束整个进程树</param>
    /// <returns>进程返回值，若等待超时将返回 <see cref="int.MinValue"/></returns>
    public static int Kill(Process process, int timeout = 3000, bool force = false) =>
        _processManager.Kill(process, timeout, force);

    /// <summary>
    /// 将特定程序设置为使用高性能显卡启动。
    /// </summary>
    /// <param name="executable">可执行文件路径。</param>
    /// <param name="wantHighPerformance">是否使用高性能显卡，默认为 true。</param>
    /// <exception cref="ArgumentException">当可执行文件路径无效时抛出</exception>
    /// <exception cref="UnauthorizedAccessException">当没有足够权限访问注册表时抛出</exception>
    /// <exception cref="SecurityException">当安全策略不允许访问注册表时抛出</exception>
    /// <exception cref="InvalidOperationException">当注册表操作失败时抛出</exception>
    public static void SetGpuPreference(string executable, bool wantHighPerformance = true) {
        if (string.IsNullOrWhiteSpace(executable)) {
            throw new ArgumentException("可执行文件路径不能为空或仅包含空白字符", nameof(executable));
        }

        try {
            var fullPath = Path.GetFullPath(executable);
            if (!File.Exists(fullPath)) {
                LogWrapper.Warn("System", $"指定的可执行文件不存在: {executable}");
            }
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ArgumentException($"无效的可执行文件路径: {executable}", nameof(executable), ex);
        }

        try {
            _platformService.SetGpuPreference(executable, wantHighPerformance);
        } catch (UnauthorizedAccessException ex) {
            var errorMsg = "没有足够的权限访问注册表。请以管理员身份运行程序或检查用户权限设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new UnauthorizedAccessException(errorMsg, ex);
        } catch (SecurityException ex) {
            var errorMsg = "安全策略不允许访问注册表。请联系系统管理员检查安全设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new SecurityException(errorMsg, ex);
        } catch (Exception ex) {
            var errorMsg = $"设置 GPU 偏好时发生未预期的错误: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new InvalidOperationException(errorMsg, ex);
        }
    }
}

public enum ProcessExitCode {
    /// <summary>
    /// Indicates that the process completed successfully.
    /// </summary>
    TaskDone = 0,

    /// <summary>
    /// Indicates a general failure of the process.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Indicates the process was canceled.
    /// </summary>
    Canceled = 2,

    /// <summary>
    /// Indicates the process failed due to insufficient permissions.
    /// </summary>
    AccessDenied = 5
}

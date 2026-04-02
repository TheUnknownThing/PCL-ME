using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

internal sealed class WindowsProcessPlatformService : IProcessPlatformService
{
    public bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public string? GetCommandLine(int processId)
    {
        var query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}";
        using var searcher = new ManagementObjectSearcher(query);
        using var results = searcher.Get();
        foreach (var result in results)
        {
            return result["CommandLine"]?.ToString();
        }

        return null;
    }

    public Process? StartAsAdmin(string path, string? arguments)
    {
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        if (arguments != null) psi.Arguments = arguments;
        return Process.Start(psi);
    }

    public void SetGpuPreference(string executable, bool wantHighPerformance)
    {
        const string gpuPreferenceRegKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string gpuPreferenceRegValueHigh = "GpuPreference=2;";
        const string gpuPreferenceRegValueDefault = "GpuPreference=0;";

        try
        {
            var isCurrentHighPerformance = GetCurrentGpuPreference(executable, gpuPreferenceRegKey, gpuPreferenceRegValueHigh);

            LogWrapper.Info("System", $"当前程序 ({executable}) 的显卡设置为高性能: {isCurrentHighPerformance}");

            if (isCurrentHighPerformance == wantHighPerformance)
            {
                LogWrapper.Info("System", $"程序 ({executable}) 的显卡设置已经是期望的设置，无需修改");
                return;
            }

            SetGpuPreferenceValue(executable, wantHighPerformance, gpuPreferenceRegKey, gpuPreferenceRegValueHigh, gpuPreferenceRegValueDefault);
        }
        catch (UnauthorizedAccessException ex)
        {
            var errorMsg = "没有足够的权限访问注册表。请以管理员身份运行程序或检查用户权限设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new UnauthorizedAccessException(errorMsg, ex);
        }
        catch (SecurityException ex)
        {
            var errorMsg = "安全策略不允许访问注册表。请联系系统管理员检查安全设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new SecurityException(errorMsg, ex);
        }
        catch (Exception ex)
        {
            var errorMsg = $"设置 GPU 偏好时发生未预期的错误: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new InvalidOperationException(errorMsg, ex);
        }
    }

    private static bool GetCurrentGpuPreference(string executable, string regKey, string highPerfValue)
    {
        try
        {
            using var readOnlyKey = Registry.CurrentUser.OpenSubKey(regKey, false);
            if (readOnlyKey == null)
            {
                LogWrapper.Info("System", "GPU 偏好注册表键不存在，将在需要时创建");
                return false;
            }

            var currentValue = readOnlyKey.GetValue(executable)?.ToString();
            return string.Equals(currentValue, highPerfValue, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "System", $"读取当前 GPU 偏好设置时出现错误: {ex.Message}");
            return false;
        }
    }

    private static void SetGpuPreferenceValue(string executable, bool wantHighPerformance, string regKey, string highPerfValue, string defaultValue)
    {
        RegistryKey? writeKey = null;
        try
        {
            writeKey = Registry.CurrentUser.OpenSubKey(regKey, true);
            if (writeKey == null)
            {
                LogWrapper.Info("System", "创建 GPU 偏好注册表键");
                writeKey = Registry.CurrentUser.CreateSubKey(regKey);

                if (writeKey == null)
                {
                    throw new InvalidOperationException($"无法创建注册表键: {regKey}");
                }
            }

            var valueToSet = wantHighPerformance ? highPerfValue : defaultValue;
            writeKey.SetValue(executable, valueToSet, RegistryValueKind.String);
            LogWrapper.Info("System", $"成功设置程序 ({executable}) 的GPU偏好: {(wantHighPerformance ? "高性能" : "默认")}");
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorMsg = $"写入注册表时发生错误: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new InvalidOperationException(errorMsg, ex);
        }
        finally
        {
            writeKey?.Dispose();
        }
    }
}

using System;
using System.Management;
using System.Runtime.Versioning;
using PCL.Core.Logging;

namespace PCL.Core.Utils.Secret;

[SupportedOSPlatform("windows")]
internal static class WindowsLegacyCpuIdProvider
{
    private const string LogModule = "Identify";

    public static string? GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var collection = searcher.Get();

            foreach (var item in collection)
            {
                try
                {
                    return item["ProcessorId"]?.ToString();
                }
                catch (ManagementException ex)
                {
                    LogWrapper.Warn(LogModule, $"WMI属性读取失败: {ex.Message}");
                }
                finally
                {
                    item.Dispose();
                }
            }

            LogWrapper.Warn(LogModule, "未找到有效的CPU ID");
            return null;
        }
        catch (ManagementException ex)
        {
            LogWrapper.Error(ex, LogModule, "WMI查询失败");
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogWrapper.Error(ex, LogModule, "COM异常，请确保WMI服务正在运行");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWrapper.Error(ex, LogModule, "访问被拒绝，请以管理员权限运行");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, LogModule, "意外的系统异常");
        }

        return null;
    }
}

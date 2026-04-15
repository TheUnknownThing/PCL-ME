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
                    LogWrapper.Warn(LogModule, $"Failed to read WMI property: {ex.Message}");
                }
                finally
                {
                    item.Dispose();
                }
            }

            LogWrapper.Warn(LogModule, "No valid CPU ID was found.");
            return null;
        }
        catch (ManagementException ex)
        {
            LogWrapper.Error(ex, LogModule, "WMI query failed.");
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogWrapper.Error(ex, LogModule, "A COM exception occurred. Make sure the WMI service is running.");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWrapper.Error(ex, LogModule, "Access denied. Try running with administrator privileges.");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, LogModule, "Unexpected system exception.");
        }

        return null;
    }
}

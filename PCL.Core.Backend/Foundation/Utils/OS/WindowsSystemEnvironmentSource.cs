using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

[SupportedOSPlatform("windows")]
internal sealed class WindowsSystemEnvironmentSource(ISystemRuntimeInfoSource runtimeInfoSource) : ISystemEnvironmentSource
{
    public SystemEnvironmentSnapshot GetSnapshot()
    {
        var runtime = runtimeInfoSource.GetSnapshot();
        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            runtime.OsVersion,
            runtime.OsArchitecture,
            runtime.Is64BitOperatingSystem,
            runtime.TotalPhysicalMemoryBytes,
            GetCpuName(),
            GetGpuInfos());
    }

    private static string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name FROM Win32_Processor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                var cpuName = queryObj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(cpuName))
                {
                    return cpuName;
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "System", "获取 CPU 信息时出错");
        }

        return string.Empty;
    }

    private static IReadOnlyList<SystemGpuInfo> GetGpuInfos()
    {
        var gpus = new List<SystemGpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                var name = queryObj["Name"]?.ToString() ?? string.Empty;
                var driverVersion = queryObj["DriverVersion"]?.ToString() ?? string.Empty;
                var memoryMegabytes = 0L;

                if (queryObj["AdapterRAM"] is not null)
                {
                    try
                    {
                        memoryMegabytes = Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "System", "解析 GPU 显存时出错");
                    }
                }

                gpus.Add(new SystemGpuInfo(name, memoryMegabytes, driverVersion));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "System", "获取 GPU 信息时出错");
        }

        return gpus;
    }
}

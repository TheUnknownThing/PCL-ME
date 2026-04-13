using System;
using System.Management;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.Secret;

internal static class WindowsDeviceIdentityProvider
{
    private const string LogModule = "Identify";

    public static byte[] GetRawId()
    {
        var code = new StringBuilder();
        try
        {
            code.Append("UUID:").Append(GetWmiProperty("Win32_ComputerSystemProduct", "UUID"))
                .Append("|MB_Prod:").Append(GetWmiProperty("Win32_BaseBoard", "Product"))
                .Append("|MB_SN:").Append(GetWmiProperty("Win32_BaseBoard", "SerialNumber"))
                .Append("|CPU:").Append(GetWmiProperty("Win32_Processor", "ProcessorId"));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, LogModule, "获取设备基础信息失败");
        }

        return Encoding.UTF8.GetBytes(Convert.ToHexString(SHA512Provider.Instance.ComputeHash(code.ToString())).ToLowerInvariant());
    }

    public static string GetLauncherId()
    {
        try
        {
            var prefix = "PCL-CE|"u8.ToArray();
            var ctx = GetRawId();
            var suffix = "|LauncherId"u8.ToArray();

            var buffer = new byte[prefix.Length + ctx.Length + suffix.Length];
            var bufferSpan = buffer.AsSpan();
            prefix.CopyTo(bufferSpan[..prefix.Length]);
            ctx.CopyTo(bufferSpan.Slice(prefix.Length, ctx.Length));
            suffix.CopyTo(bufferSpan.Slice(prefix.Length + ctx.Length, suffix.Length));

            Array.Clear(ctx);
            var sample = Convert.ToHexString(SHA512Provider.Instance.ComputeHash(bufferSpan)).ToLowerInvariant();
            bufferSpan.Clear();

            return sample.Substring(64, 16)
                .ToUpperInvariant()
                .Insert(4, "-")
                .Insert(9, "-")
                .Insert(14, "-");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, LogModule, "无法获取识别码");
            return "PCL2-CECE-GOOD-2025";
        }
    }

    private static string GetWmiProperty(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            using var results = searcher.Get();
            foreach (var obj in results)
            {
                if (obj[propertyName] is not null)
                {
                    return (obj[propertyName].ToString() ?? string.Empty).Trim();
                }
            }
        }
        catch
        {
            // Ignore and fall back to an empty property contribution.
        }

        return string.Empty;
    }
}

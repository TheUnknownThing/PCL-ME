using System;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.App.Essentials.Telemetry;

internal sealed class WindowsRegistryOfficialLauncherUsageProbe : IOfficialLauncherUsageProbe
{
    private const string LauncherRegPath = @"HKEY_CURRENT_USER\Software\PCL";
    private const string LauncherRegValueName = "SystemEula";

    public bool HasUsedOfficialLauncher()
    {
        try
        {
            return bool.TryParse(Registry.GetValue(LauncherRegPath, LauncherRegValueName, "false") as string, out var value) && value;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Telemetry", "读取官方 PCL 使用状态时出现异常");
            return false;
        }
    }
}

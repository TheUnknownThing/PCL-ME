using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupEnvironmentWarningService
{
    public static IReadOnlyList<string> GetWarnings(LauncherStartupEnvironmentWarningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var executableDirectory = request.ExecutableDirectory ?? string.Empty;
        var isLikelyMacAppTranslocation = IsLikelyMacAppTranslocationPath(executableDirectory);

        if (request.DetectedWindowsVersion.Build < 17763)
        {
            warnings.Add("- Windows does not meet the recommended version. Windows 10 1809 or later is recommended; consider upgrading Windows.");
        }

        if (!request.Is64BitOperatingSystem)
        {
            warnings.Add("- The current system is 32-bit and is not supported by PCL or newer Minecraft versions. Reinstalling a 64-bit system is strongly recommended before playing.");
        }

        if (isLikelyMacAppTranslocation)
        {
            warnings.Add("- PCL is currently running from a macOS translocation path. Move it to Applications or another normal directory before opening it to avoid path detection issues.");
        }
        else if (executableDirectory.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) ||
                 executableDirectory.Contains(@"AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("- PCL is running from a temporary directory. Extract it from the archive before using it, or game saves and settings may be lost.");
        }

        if (executableDirectory.Contains("wechat_files", StringComparison.OrdinalIgnoreCase) ||
            executableDirectory.Contains("WeChat Files", StringComparison.OrdinalIgnoreCase) ||
            executableDirectory.Contains("Tencent Files", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("- PCL is running from a download directory used by QQ, WeChat, TIM, or similar apps. Consider moving it elsewhere, or game saves and settings may be lost.");
        }

        return warnings;
    }

    private static bool IsLikelyMacAppTranslocationPath(string executableDirectory)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return false;
        }

        return executableDirectory.Contains("/AppTranslocation/", StringComparison.OrdinalIgnoreCase)
               && executableDirectory.Contains(".app/Contents/MacOS", StringComparison.OrdinalIgnoreCase);
    }
}

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

        if (request.DetectedWindowsVersion.Build < 17763)
        {
            warnings.Add("- Windows 版本不满足推荐要求，推荐至少 Windows 10 1809，建议考虑升级 Windows 系统");
        }

        if (!request.Is64BitOperatingSystem)
        {
            warnings.Add("- 当前系统为 32 位，不受 PCL 和新版 Minecraft 支持，非常建议重装为 64 位系统后再进行游戏");
        }

        if (executableDirectory.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) ||
            executableDirectory.Contains(@"AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("- PCL 正在临时目录运行，请将 PCL 从压缩包中解压之后再使用，否则可能导致游戏存档或设置丢失");
        }

        if (executableDirectory.Contains("wechat_files", StringComparison.OrdinalIgnoreCase) ||
            executableDirectory.Contains("WeChat Files", StringComparison.OrdinalIgnoreCase) ||
            executableDirectory.Contains("Tencent Files", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("- PCL 正在 QQ、微信、TIM 等社交软件的下载目录运行，请考虑移动到其他位置，否则可能导致游戏存档或设置丢失");
        }

        return warnings;
    }
}

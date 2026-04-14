using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupEnvironmentWarningServiceTest
{
    [TestMethod]
    public void GetWarningsIncludesAllConfiguredChecks()
    {
        var request = new LauncherStartupEnvironmentWarningRequest(
            @"C:\Users\Alice\WeChat Files\AppData\Local\Temp\PCL\",
            new Version(10, 0, 17762, 0),
            false);

        var warnings = LauncherStartupEnvironmentWarningService.GetWarnings(request);

        CollectionAssert.AreEqual(
            new[]
            {
                "- Windows 版本不满足推荐要求，推荐至少 Windows 10 1809，建议考虑升级 Windows 系统",
                "- 当前系统为 32 位，不受 PCL 和新版 Minecraft 支持，非常建议重装为 64 位系统后再进行游戏",
                "- PCL 正在临时目录运行，请将 PCL 从压缩包中解压之后再使用，否则可能导致游戏存档或设置丢失",
                "- PCL 正在 QQ、微信、TIM 等社交软件的下载目录运行，请考虑移动到其他位置，否则可能导致游戏存档或设置丢失",
            },
            warnings.ToArray());
    }

    [TestMethod]
    public void GetWarningsReturnsEmptyForHealthyEnvironment()
    {
        var request = new LauncherStartupEnvironmentWarningRequest(
            @"D:\Games\PCL\",
            new Version(10, 0, 19045, 0),
            true);

        var warnings = LauncherStartupEnvironmentWarningService.GetWarnings(request);

        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void GetWarningsUsesDedicatedMessageForMacAppTranslocation()
    {
        var request = new LauncherStartupEnvironmentWarningRequest(
            "/private/var/folders/xx/yy/AppTranslocation/12345678-90AB-CDEF-1234-567890ABCDEF/d/PCL-ME.app/Contents/MacOS/",
            new Version(10, 0, 19045, 0),
            true);

        var warnings = LauncherStartupEnvironmentWarningService.GetWarnings(request);

        CollectionAssert.AreEqual(
            new[]
            {
                "- PCL 当前被 macOS 放在临时隔离路径中运行，请将 PCL 移到应用程序或其他常规目录后再打开，以避免路径识别异常"
            },
            warnings.ToArray());
    }
}

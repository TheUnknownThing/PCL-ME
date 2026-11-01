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
                "- Windows does not meet the recommended version. Windows 10 1809 or later is recommended; consider upgrading Windows.",
                "- The current system is 32-bit and is not supported by PCL or newer Minecraft versions. Reinstalling a 64-bit system is strongly recommended before playing.",
                "- PCL is running from a temporary directory. Extract it from the archive before using it, or game saves and settings may be lost.",
                "- PCL is running from a download directory used by QQ, WeChat, TIM, or similar apps. Consider moving it elsewhere, or game saves and settings may be lost.",
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
                "- PCL is currently running from a macOS translocation path. Move it to Applications or another normal directory before opening it to avoid path detection issues."
            },
            warnings.ToArray());
    }
}

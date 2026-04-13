using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupPreparationServiceTest
{
    [TestMethod]
    public void PrepareCombinesWarningsAndBootstrapOutputs()
    {
        var result = LauncherStartupPreparationService.Prepare(new LauncherStartupPreparationRequest(
            @"C:\Users\Alice\AppData\Local\Temp\PCL",
            @"C:\Users\Alice\AppData\Local\Temp\PCL\Temp",
            @"C:\Users\Alice\AppData\Roaming\PCL\",
            IsBetaVersion: true,
            new Version(10, 0, 17762, 0),
            Is64BitOperatingSystem: false));

        Assert.AreEqual(UpdateChannel.Beta, result.DefaultUpdateChannel);
        Assert.AreEqual(@"C:\Users\Alice\AppData\Roaming\PCL\", result.DirectoriesToCreate.Last());
        StringAssert.Contains(result.EnvironmentWarningMessage, "Windows 版本不满足推荐要求");
        StringAssert.Contains(result.EnvironmentWarningMessage, "当前系统为 32 位");
        StringAssert.Contains(result.EnvironmentWarningMessage, "临时目录运行");
    }
}

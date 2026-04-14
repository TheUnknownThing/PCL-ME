using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupBootstrapServiceTest
{
    [TestMethod]
    public void BuildReturnsDirectoriesConfigKeysLogsAndBetaChannel()
    {
        var result = LauncherStartupBootstrapService.Build(new LauncherStartupBootstrapRequest(
            @"C:\PCL\",
            @"C:\PCL\Temp\",
            @"C:\Users\Alice\AppData\Roaming\PCL\",
            IsBetaVersion: true,
            EnvironmentWarnings: []));

        CollectionAssert.AreEqual(
            new[]
            {
                @"C:\PCL\/PCL/Pictures".Replace('/', '\\'),
                @"C:\PCL\/PCL/Musics".Replace('/', '\\'),
                @"C:\PCL\Temp\/Cache".Replace('/', '\\'),
                @"C:\PCL\Temp\/Download".Replace('/', '\\'),
                @"C:\Users\Alice\AppData\Roaming\PCL\"
            },
            result.DirectoriesToCreate.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "SystemDebugMode",
                "SystemDebugAnim",
                "SystemHttpProxy",
                "SystemHttpProxyCustomUsername",
                "SystemHttpProxyCustomPassword",
                "SystemHttpProxyType",
                "ToolDownloadThread",
                "ToolDownloadSpeed",
                "ToolDownloadTimeout",
                "UiFont"
            },
            result.ConfigKeysToLoad.ToArray());
        Assert.AreEqual(UpdateChannel.Beta, result.DefaultUpdateChannel);
        Assert.AreEqual(@"C:\PCL\/PCL/Log-CE1.log".Replace('/', '\\'), result.LegacyLogFilesToDelete.First());
        Assert.IsNull(result.EnvironmentWarningMessage);
    }

    [TestMethod]
    public void BuildReturnsEnvironmentWarningMessage()
    {
        var result = LauncherStartupBootstrapService.Build(new LauncherStartupBootstrapRequest(
            "/Applications/PCL",
            "/tmp/pcl",
            "/Users/test/.pcl",
            IsBetaVersion: false,
            EnvironmentWarnings:
            [
                "- warning one",
                "- warning two"
            ]));

        Assert.AreEqual(UpdateChannel.Release, result.DefaultUpdateChannel);
        StringAssert.Contains(result.EnvironmentWarningMessage, "PCL-ME 在启动时检测到环境问题：");
        StringAssert.Contains(result.EnvironmentWarningMessage, "- warning one");
        StringAssert.Contains(result.EnvironmentWarningMessage, "- warning two");
    }
}

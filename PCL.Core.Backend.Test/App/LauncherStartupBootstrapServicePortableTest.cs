using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.App.Essentials;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class LauncherStartupBootstrapServicePortableTest
{
    [TestMethod]
    public void BuildPreservesWindowsStyleBasePathsWhenInputUsesBackslashes()
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
                @"C:\PCL\PCL\Pictures",
                @"C:\PCL\PCL\Musics",
                @"C:\PCL\Temp\Cache",
                @"C:\PCL\Temp\Download",
                @"C:\Users\Alice\AppData\Roaming\PCL\"
            },
            result.DirectoriesToCreate.ToArray());
        Assert.AreEqual(@"C:\PCL\PCL\Log-CE1.log", result.LegacyLogFilesToDelete.First());
        Assert.AreEqual(UpdateChannel.Beta, result.DefaultUpdateChannel);
    }
}

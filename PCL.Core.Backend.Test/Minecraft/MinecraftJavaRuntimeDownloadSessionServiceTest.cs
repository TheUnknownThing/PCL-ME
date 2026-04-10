using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftJavaRuntimeDownloadSessionServiceTest
{
    [TestMethod]
    public void GetDefaultIgnoredSha1HashesReturnsKnownCompatibilitySkips()
    {
        var result = MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes();

        CollectionAssert.AreEqual(
            new[]
            {
                "12976a6c2b227cbac58969c1455444596c894656",
                "c80e4bab46e34d02826eab226a4441d0970f2aba",
                "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
            },
            result.ToArray());
    }

    [TestMethod]
    public void GetRuntimeBaseDirectoryPreservesWindowsStyleRoots()
    {
        var result = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
            @"C:\Minecraft\.minecraft",
            "jre-21");

        Assert.AreEqual(@"C:\Minecraft\.minecraft\runtime\jre-21", result);
    }

    [TestMethod]
    public void ResolveStateTransitionReturnsCleanupPlanForAbortedDownload()
    {
        var result = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(
            MinecraftJavaRuntimeDownloadSessionState.Aborted,
            @"C:\Minecraft\.minecraft\runtime\jre-21");

        Assert.AreEqual(@"C:\Minecraft\.minecraft\runtime\jre-21", result.CleanupDirectoryPath);
        Assert.IsNotNull(result.CleanupLogMessage);
        Assert.IsFalse(result.ShouldRefreshJavaInventory);
        Assert.IsTrue(result.ShouldClearTrackedRuntimeDirectory);
    }

    [TestMethod]
    public void ResolveStateTransitionReturnsRefreshPlanForFinishedDownload()
    {
        var result = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(
            MinecraftJavaRuntimeDownloadSessionState.Finished,
            @"/tmp/pcl-java-runtime");

        Assert.IsNull(result.CleanupDirectoryPath);
        Assert.IsTrue(result.ShouldRefreshJavaInventory);
        Assert.IsTrue(result.ShouldClearTrackedRuntimeDirectory);
    }
}

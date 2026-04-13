using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupMilestoneServiceTest
{
    [TestMethod]
    public void AdvanceStartupCountIncrementsWithoutMilestoneBeforeThreshold()
    {
        var result = LauncherStartupMilestoneService.AdvanceStartupCount(42);

        Assert.AreEqual(43, result.UpdatedCount);
        Assert.IsFalse(result.ShouldAttemptUnlockHiddenTheme);
        Assert.IsNull(result.HiddenThemeNotice);
    }

    [TestMethod]
    public void AdvanceStartupCountReturnsHiddenThemeNoticeAtThreshold()
    {
        var result = LauncherStartupMilestoneService.AdvanceStartupCount(98);

        Assert.AreEqual(99, result.UpdatedCount);
        Assert.IsTrue(result.ShouldAttemptUnlockHiddenTheme);
        Assert.IsNotNull(result.HiddenThemeNotice);
        Assert.AreEqual("提示", result.HiddenThemeNotice.Title);
        StringAssert.Contains(result.HiddenThemeNotice.Message, "99 次 PCL 跨平台版");
    }
}

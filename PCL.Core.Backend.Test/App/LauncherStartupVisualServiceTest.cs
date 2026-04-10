using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupVisualServiceTest
{
    [TestMethod]
    public void GetVisualPlanReturnsSplashScreenWhenLogoIsEnabled()
    {
        var result = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: true);

        Assert.IsTrue(result.ShouldShowSplashScreen);
        Assert.IsNotNull(result.SplashScreen);
        Assert.AreEqual(@"Images\icon.ico", result.SplashScreen.IconPath);
    }

    [TestMethod]
    public void GetVisualPlanOmitsSplashScreenWhenLogoIsDisabled()
    {
        var result = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: false);

        Assert.IsFalse(result.ShouldShowSplashScreen);
        Assert.IsNull(result.SplashScreen);
    }

    [TestMethod]
    public void GetVisualPlanReturnsLegacyTooltipDefaults()
    {
        var result = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: true);

        Assert.AreEqual(300, result.TooltipDefaults.InitialShowDelayMilliseconds);
        Assert.AreEqual(400, result.TooltipDefaults.BetweenShowDelayMilliseconds);
        Assert.AreEqual(9_999_999, result.TooltipDefaults.ShowDurationMilliseconds);
        Assert.AreEqual(LauncherTooltipPlacement.Bottom, result.TooltipDefaults.Placement);
        Assert.AreEqual(8.0, result.TooltipDefaults.HorizontalOffset);
        Assert.AreEqual(4.0, result.TooltipDefaults.VerticalOffset);
    }
}

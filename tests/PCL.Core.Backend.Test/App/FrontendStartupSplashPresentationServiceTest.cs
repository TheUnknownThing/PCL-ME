using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendStartupSplashPresentationServiceTest
{
    [TestMethod]
    public void CreateRequest_ReturnsAssetsWhenSplashScreenIsEnabled()
    {
        var visualPlan = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: true);

        var result = FrontendStartupSplashPresentationService.CreateRequest(visualPlan);

        Assert.IsNotNull(result);
        Assert.AreEqual("avares", result.SplashImageAssetUri.Scheme);
        Assert.AreEqual("pcl.frontend.avalonia", result.SplashImageAssetUri.Host);
        Assert.AreEqual("/Assets/icon.png", result.SplashImageAssetUri.AbsolutePath);
        Assert.AreEqual(TimeSpan.FromMilliseconds(240), result.MinimumVisibleDuration);
    }

    [TestMethod]
    public void CreateRequest_ReturnsNullWhenSplashScreenIsDisabled()
    {
        var visualPlan = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: false);

        var result = FrontendStartupSplashPresentationService.CreateRequest(visualPlan);

        Assert.IsNull(result);
    }
}

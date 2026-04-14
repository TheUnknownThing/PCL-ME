using Avalonia;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class MainWindowBackgroundLayoutTest
{
    [TestMethod]
    public void ResolveBackgroundAssetPixelSize_PrefersOriginalSourceDimensions()
    {
        var result = MainWindow.ResolveBackgroundAssetPixelSize(
            new PixelSize(320, 180),
            sourcePixelWidth: 1920,
            sourcePixelHeight: 1080);

        Assert.AreEqual(new PixelSize(1920, 1080), result);
    }

    [TestMethod]
    public void ResolveBackgroundAssetPixelSize_FallsBackToRenderedBitmapSizeWhenSourceIsUnavailable()
    {
        var result = MainWindow.ResolveBackgroundAssetPixelSize(
            new PixelSize(320, 180),
            sourcePixelWidth: 0,
            sourcePixelHeight: 0);

        Assert.AreEqual(new PixelSize(320, 180), result);
    }

    [TestMethod]
    public void ResolveAutomaticBackgroundSuitMode_UsesOriginalAssetSizeForSmartMode()
    {
        var result = MainWindow.ResolveAutomaticBackgroundSuitMode(
            new PixelSize(1920, 1080),
            availableWidth: 1600,
            availableHeight: 900);

        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void ResolveAutomaticBackgroundSuitMode_TilesOnlyWhenAssetIsActuallySmall()
    {
        var result = MainWindow.ResolveAutomaticBackgroundSuitMode(
            new PixelSize(320, 180),
            availableWidth: 1600,
            availableHeight: 900);

        Assert.AreEqual(4, result);
    }

    [TestMethod]
    public void CreateDynamicBackgroundEffect_ReturnsNullWhenBlurIsDisabled()
    {
        var result = MainWindow.CreateDynamicBackgroundEffect(0.5);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void CreateDynamicBackgroundEffect_ReturnsBlurEffectWhenBlurIsEnabled()
    {
        var result = MainWindow.CreateDynamicBackgroundEffect(12);

        Assert.IsInstanceOfType<BlurEffect>(result);
        Assert.AreEqual(12d, ((BlurEffect)result).Radius);
    }

    [TestMethod]
    public void IsWindowResizeAllowed_ReturnsFalseWhenWindowSizeIsLocked()
    {
        Assert.IsFalse(MainWindow.IsWindowResizeAllowed(lockWindowSize: true));
    }

    [TestMethod]
    public void ShouldShowResizeChrome_HidesResizeChromeWhenWindowSizeIsLocked()
    {
        Assert.IsFalse(MainWindow.ShouldShowResizeChrome(isMaximized: false, lockWindowSize: true));
    }

    [TestMethod]
    public void ShouldShowResizeChrome_HidesResizeChromeWhenWindowIsMaximized()
    {
        Assert.IsFalse(MainWindow.ShouldShowResizeChrome(isMaximized: true, lockWindowSize: false));
    }

    [TestMethod]
    public void ShouldShowResizeChrome_ShowsResizeChromeWhenWindowCanResize()
    {
        Assert.IsTrue(MainWindow.ShouldShowResizeChrome(isMaximized: false, lockWindowSize: false));
    }
}

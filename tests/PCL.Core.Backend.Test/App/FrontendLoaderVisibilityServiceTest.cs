using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendLoaderVisibilityServiceTest
{
    [TestMethod]
    public void FilterVisibleLoaders_RemovesQuiltWhenConfigured()
    {
        var result = FrontendLoaderVisibilityService.FilterVisibleLoaders(
            ["Forge", "Quilt", "Fabric"],
            hideQuiltLoader: true);

        CollectionAssert.AreEqual(new[] { "Forge", "Fabric" }, result.ToArray());
    }

    [TestMethod]
    public void FilterVisibleLoaders_KeepsQuiltWhenSettingIsDisabled()
    {
        var result = FrontendLoaderVisibilityService.FilterVisibleLoaders(
            ["Forge", "Quilt", "Fabric"],
            hideQuiltLoader: false);

        CollectionAssert.AreEqual(new[] { "Forge", "Quilt", "Fabric" }, result.ToArray());
    }

    [TestMethod]
    public void IsDownloadSubpageVisible_HidesDownloadQuiltWhenConfigured()
    {
        Assert.IsFalse(FrontendLoaderVisibilityService.IsDownloadSubpageVisible(
            LauncherFrontendSubpageKey.DownloadQuilt,
            hideQuiltLoader: true));
    }
}

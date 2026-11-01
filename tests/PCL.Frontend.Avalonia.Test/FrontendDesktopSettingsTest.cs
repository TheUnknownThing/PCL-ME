using PCL.Extensions.DesktopPet;
using Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
public sealed class FrontendDesktopSettingsTest
{
    [TestMethod]
    public void DesktopSettings_ReadOnlyLauncherResidencyPreferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-desktop-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new YamlFileProvider(Path.Combine(root, "settings.yaml"));
            Assert.AreEqual(new FrontendDesktopSettings(), FrontendDesktopSettingsService.Read(provider));
            provider.Set(FrontendDesktopSettingsService.KeepRunningInTrayKey, true);
            provider.Set(FrontendDesktopSettingsService.WindowCloseActionKey, "Exit");
            provider.Sync();
            Assert.AreEqual(new FrontendDesktopSettings(true, WindowCloseAction.Exit), FrontendDesktopSettingsService.Read(provider));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void PetPositionRestore_PreservesNegativeMonitorsAndClampsEdges()
    {
        var areas = new[] { new PixelRect(-1920, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1080) };
        Assert.AreEqual(new PixelPoint(-1800, 140), PetSettingsService.RestorePosition(new(-1800, 140), new(100, 100), areas, areas[1]));
        Assert.AreEqual(new PixelPoint(1820, 980), PetSettingsService.RestorePosition(new(1900, 1000), new(100, 100), areas, areas[1]));
        Assert.AreEqual(1, PetSettingsService.NormalizeScale(double.NaN));
        Assert.AreEqual(.5, PetSettingsService.NormalizeScale(-100));
        Assert.AreEqual(2, PetSettingsService.NormalizeScale(100));
    }
}

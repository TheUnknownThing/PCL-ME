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
    public void DesktopSettings_AreOptInAndReadLocalPreferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-desktop-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "settings.yaml");
            var provider = new YamlFileProvider(path);
            Assert.AreEqual(new FrontendDesktopSettings(PetEnabled: true, CloseAction: WindowCloseAction.MinimizeToTray),
                FrontendDesktopSettingsService.Read(provider));
            Assert.IsNull(FrontendDesktopSettingsService.ReadPosition(provider));
            provider.Set(FrontendDesktopSettingsService.KeepRunningInTrayKey, true);
            provider.Set(FrontendDesktopSettingsService.PetEnabledKey, true);
            provider.Set(FrontendDesktopSettingsService.PetTopmostKey, false);
            provider.Set(FrontendDesktopSettingsService.PetScaleKey, 1.5);
            provider.Set(FrontendDesktopSettingsService.PetUseCustomKey, true);
            provider.Set(FrontendDesktopSettingsService.PetPresetKey, "campfire_gif");
            provider.Set(FrontendDesktopSettingsService.PetImagePathKey, Path.Combine(root, "custom.png"));
            provider.Set(FrontendDesktopSettingsService.PetClickActionKey, "ShowLauncher");
            provider.Set(FrontendDesktopSettingsService.PetDoubleClickActionKey, "HidePet");
            provider.Set(FrontendDesktopSettingsService.WindowCloseActionKey, "MinimizeToTray");
            provider.Set(FrontendDesktopSettingsService.PetXKey, -1800);
            provider.Set(FrontendDesktopSettingsService.PetYKey, 140);
            provider.Sync();
            var reopened = new YamlFileProvider(path);
            Assert.AreEqual(new FrontendDesktopSettings(true, true, false, 1.5, true, "campfire_gif",
                Path.Combine(root, "custom.png"), PetAction.ShowLauncher, PetAction.HidePet,
                WindowCloseAction.MinimizeToTray),
                FrontendDesktopSettingsService.Read(reopened));
            Assert.AreEqual(new PixelPoint(-1800, 140), FrontendDesktopSettingsService.ReadPosition(reopened));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PositionRestore_PreservesNegativeMonitorCoordinatesAndClampsEdges()
    {
        var primary = new PixelRect(0, 24, 1920, 1016);
        var left = new PixelRect(-1920, 0, 1920, 1080);
        var size = new PixelSize(120, 156);
        Assert.AreEqual(new PixelPoint(-1800, 140), FrontendDesktopSettingsService.RestorePosition(
            new(-1800, 140), size, [primary, left], primary));
        Assert.AreEqual(new PixelPoint(-120, 924), FrontendDesktopSettingsService.RestorePosition(
            new(-10, 1070), size, [primary, left], primary));
        Assert.AreEqual(new PixelPoint(1776, 860), FrontendDesktopSettingsService.RestorePosition(
            new(-1800, 140), size, [primary], primary));
        Assert.AreEqual(new PixelPoint(0, 24), FrontendDesktopSettingsService.RestorePosition(
            new(10, 30), new PixelSize(4000, 4000), [primary], primary));
    }

    [TestMethod]
    public void ScaleNormalization_RejectsNonfiniteAndBoundsImportedValues()
    {
        Assert.AreEqual(1, FrontendDesktopSettingsService.NormalizeScale(double.NaN));
        Assert.AreEqual(1, FrontendDesktopSettingsService.NormalizeScale(double.PositiveInfinity));
        Assert.AreEqual(0.5, FrontendDesktopSettingsService.NormalizeScale(-100));
        Assert.AreEqual(2, FrontendDesktopSettingsService.NormalizeScale(100));
        Assert.AreEqual("creeper", FrontendDesktopSettingsService.NormalizeAppearance("unknown"));
        Assert.AreEqual("creeper_gif", FrontendDesktopSettingsService.NormalizePreset("unknown"));
        Assert.AreEqual("slime", FrontendDesktopSettingsService.NormalizePreset(null, "slime"));
        Assert.AreEqual("campfire_gif", FrontendDesktopSettingsService.NormalizePreset("campfire_gif"));
        Assert.AreEqual(WindowCloseAction.MinimizeToTray,
            FrontendDesktopSettingsService.NormalizeCloseAction("MinimizeToTray", WindowCloseAction.Exit));
        Assert.AreEqual(WindowCloseAction.Exit,
            FrontendDesktopSettingsService.NormalizeCloseAction("unknown", WindowCloseAction.Exit));
    }
}

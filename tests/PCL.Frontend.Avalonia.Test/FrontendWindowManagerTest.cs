using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
public sealed class FrontendWindowManagerTest
{
    [TestMethod]
    [DataRow("niri", true)]
    [DataRow("sway", true)]
    [DataRow("Hyprland", true)]
    [DataRow("i3", true)]
    [DataRow("GNOME:i3", true)]
    [DataRow(" X-Custom : Qtile ", true)]
    [DataRow("bspwm", true)]
    [DataRow("awesome", true)]
    [DataRow("river", true)]
    [DataRow("ubuntu:GNOME", false)]
    [DataRow("KDE", false)]
    [DataRow("XFCE", false)]
    [DataRow("X-Cinnamon", false)]
    [DataRow("unknown", false)]
    [DataRow("not-niri", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void DetectsKnownDesktopNames(string? desktop, bool expected)
    {
        Assert.AreEqual(expected, FrontendWindowManagerService.IsTilingWindowManager(true,
            name => name == "XDG_CURRENT_DESKTOP" ? desktop : null));
    }

    [TestMethod]
    [DataRow("XDG_SESSION_DESKTOP", "sway")]
    [DataRow("DESKTOP_SESSION", "i3")]
    [DataRow("NIRI_SOCKET", "/run/user/1000/niri.sock")]
    [DataRow("SWAYSOCK", "/run/user/1000/sway.sock")]
    [DataRow("HYPRLAND_INSTANCE_SIGNATURE", "instance")]
    [DataRow("I3SOCK", "/run/user/1000/i3.sock")]
    public void DetectsSessionFallbacksAndCompositorMarkers(string variable, string value)
    {
        Assert.IsTrue(FrontendWindowManagerService.IsTilingWindowManager(true,
            name => name == variable ? value : null));
        Assert.IsFalse(FrontendWindowManagerService.IsTilingWindowManager(false,
            name => name == variable ? value : null));
    }

    [TestMethod]
    public void CurrentDesktopTakesPrecedenceOverLoginSession()
    {
        Assert.IsFalse(FrontendWindowManagerService.IsTilingWindowManager(true, name => name switch
        {
            "XDG_CURRENT_DESKTOP" => "GNOME",
            "DESKTOP_SESSION" => "i3",
            _ => null
        }));
    }

    [TestMethod]
    public void CompositorMarkerIdentifiesTilingWithinAnotherDesktop()
    {
        Assert.IsTrue(FrontendWindowManagerService.IsTilingWindowManager(true, name => name switch
        {
            "XDG_CURRENT_DESKTOP" => "GNOME",
            "I3SOCK" => "/run/user/1000/i3.sock",
            _ => null
        }));
    }
}

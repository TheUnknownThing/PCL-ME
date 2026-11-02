using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendDesktopShellTest
{
    [TestMethod]
    public async Task TrayResidency_RestoresTheSameWindowAndHonorsTheLaunchGuard()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            var window = new Window();
            var launches = 0;
            var ready = true;
            var command = new ActionCommand(() => { Assert.IsTrue(window.IsVisible); launches++; ready = false; }, () => ready);
            using var shell = new DesktopShellController(window, new TestI18n(), command, window.Close, () => { });
            shell.SetTrayHostAvailable(true);
            shell.ApplySettings(new(true));
            var icon = shell.TrayIcon;
            Assert.IsNotNull(icon);
            window.Show();
            window.WindowState = WindowState.Maximized;
            window.Close();
            Assert.IsFalse(window.IsVisible);
            shell.LaunchSelectedInstance();
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            Assert.AreEqual(1, launches);
            shell.LaunchSelectedInstance();
            Assert.AreEqual(1, launches);
            shell.Quit();
            Assert.IsNull(shell.TrayIcon);
            Assert.IsNull(icon.Menu);
            Assert.IsNull(icon.Command);
            Assert.IsFalse(icon.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task LastRecoverySurface_DisappearingOrBeingRemovedRestoresTheLauncher()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            var main = new Window();
            var widget = new Window();
            using var shell = new DesktopShellController(main, new TestI18n(), new ActionCommand(() => { }), main.Close, () => { });
            shell.SetTrayHostAvailable(false);
            shell.ApplySettings(new(true));
            main.Show(); widget.Show();
            using var registration = shell.RegisterRecoverySurface(widget);
            main.Close();
            Assert.IsFalse(main.IsVisible);
            widget.WindowState = WindowState.Minimized;
            Assert.IsTrue(main.IsVisible);
            widget.WindowState = WindowState.Normal;
            main.Close();
            Assert.IsFalse(main.IsVisible);
            registration.Dispose();
            Assert.IsTrue(main.IsVisible);
            widget.Close(); shell.Quit();
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task MissingTray_DoesNotHideAnUnrecoverableWindow()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            var window = new Window();
            var closed = false;
            window.Closed += (_, _) => closed = true;
            using var shell = new DesktopShellController(window, new TestI18n(), new ActionCommand(() => { }), window.Close, () => { });
            shell.SetTrayHostAvailable(false);
            shell.ApplySettings(new(true)); window.Show();
            Assert.IsNull(shell.TrayIcon, "Do not create a native tray icon until a tray host is available.");
            shell.ApplyShellAction(MinecraftLaunchShellActionKind.HideLauncher);
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(WindowState.Minimized, window.WindowState);
            window.Close();
            Assert.IsTrue(closed);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task TrayHostChanges_KeepTheSameIconAndMenuAndRestoreTheHiddenLauncher()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            var window = new Window();
            using var shell = new DesktopShellController(window, new TestI18n(), new ActionCommand(() => { }), window.Close, () => { });
            shell.SetTrayHostAvailable(false);
            shell.ApplySettings(new(true));
            window.Show();
            shell.SetTrayHostAvailable(true);
            var icon = shell.TrayIcon;
            Assert.IsNotNull(icon);
            var menu = icon.Menu;
            Assert.IsNotNull(menu);
            window.Close();
            Assert.IsFalse(window.IsVisible);

            shell.SetTrayHostAvailable(false);
            Assert.IsTrue(window.IsVisible);
            Assert.IsFalse(shell.HasRecoverySurface);
            Assert.AreSame(icon, shell.TrayIcon, "Host discovery must not dispose an exporter with queued menu updates.");
            Assert.AreSame(menu, shell.TrayIcon?.Menu);

            shell.SetTrayHostAvailable(true);
            Assert.AreSame(icon, shell.TrayIcon);
            Assert.AreSame(menu, shell.TrayIcon?.Menu);
            Assert.IsTrue(shell.HasRecoverySurface);
            window.Close();
            Assert.IsFalse(window.IsVisible);
            shell.Quit();
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ExplicitExit_ClosesEvenWhenRecoveryWindowsExist()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            var main = new Window(); var widget = new Window();
            var closed = false; main.Closed += (_, _) => closed = true;
            using var shell = new DesktopShellController(main, new TestI18n(), new ActionCommand(() => { }), main.Close, () => { });
            shell.ApplySettings(new(true, WindowCloseAction.Exit));
            main.Show(); widget.Show();
            using var registration = shell.RegisterRecoverySurface(widget);
            main.Close(); Assert.IsTrue(closed); Assert.IsNull(shell.TrayIcon);
            widget.Close();
            return Task.CompletedTask;
        });
    }

    private sealed class TestI18n : II18nService
    {
        public string Locale => "en-US";
        public IReadOnlyList<string> AvailableLocales => [Locale];
        public string T(string key) => key;
        public string T(string key, IReadOnlyDictionary<string, object?> args) => key;
        public string T(PCL.Core.App.I18n.I18nText text) => text.Key;
        public bool SetLocale(string locale) => false;
        public bool ReloadLocaleFromSettings() => false;
        public bool ReloadCurrentLocale() => false;
        public event Action? Changed { add { } remove { } }
    }
}

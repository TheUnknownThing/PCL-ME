using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
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
    public async Task CloseToTray_HidesAndRestoresTheSameWindowAndQuitReallyClosesIt()
    {
        await Run(() =>
        {
            var window = new Window();
            var closed = false;
            var flushes = 0;
            window.Closed += (_, _) => closed = true;
            using var shell = new DesktopShellController(window, new TestI18n(), new ActionCommand(() => { }), window.Close, () => flushes++);
            shell.SetTrayHostAvailable(true);
            shell.ApplySettings(new(KeepRunningInTray: true, PetEnabled: true));
            Assert.IsFalse(window.IsVisible, "Enabling residency must not show the main window before startup does.");
            window.Show();
            window.WindowState = WindowState.Maximized;
            window.Close();
            Assert.IsFalse(closed);
            Assert.IsFalse(window.IsVisible);
            Assert.IsTrue(shell.Pet!.IsVisible);
            Assert.AreEqual(1, flushes);

            shell.ShowLauncher();
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            var pet = shell.Pet;
            shell.Quit();
            Assert.IsTrue(closed);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(pet.Sprite.IsAnimationRunning);
            Assert.IsNull(shell.TrayIcon);
        });
    }

    [TestMethod]
    public async Task NormalClose_WithPetEnabled_DisposesAllRecoverySurfaces()
    {
        await Run(() =>
        {
            var window = new Window();
            using var shell = CreateShell(window);
            window.Show();
            shell.ApplySettings(new(PetEnabled: true));
            var pet = shell.Pet!;
            Assert.IsTrue(pet.IsVisible);
            window.Close();
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(pet.Sprite.IsAnimationRunning);
            Assert.IsNull(shell.TrayIcon);
        });
    }

    [TestMethod]
    public async Task MissingTrayHost_FallsBackToMinimizingAndAllowsNormalClose()
    {
        await Run(() =>
        {
            var window = new Window();
            var closed = false;
            window.Closed += (_, _) => closed = true;
            using var shell = CreateShell(window);
            shell.SetTrayHostAvailable(false);
            shell.ApplySettings(new(KeepRunningInTray: true));
            window.Show();
            shell.ApplyShellAction(MinecraftLaunchShellActionKind.MinimizeLauncher);
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(WindowState.Minimized, window.WindowState);
            window.Close();
            Assert.IsTrue(closed, "No recovery surface means closing must retain its normal behavior.");
        });
    }

    [TestMethod]
    public async Task LosingLastRecoverySurface_RestoresHiddenLauncher()
    {
        await Run(() =>
        {
            var window = new Window();
            using var shell = CreateShell(window);
            shell.SetTrayHostAvailable(true);
            shell.ApplySettings(new(KeepRunningInTray: true));
            window.Show();
            window.WindowState = WindowState.Maximized;
            shell.ApplyShellAction(MinecraftLaunchShellActionKind.MinimizeLauncher);
            Assert.IsFalse(window.IsVisible);
            shell.SetTrayHostAvailable(false);
            Assert.IsTrue(window.IsVisible);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);

            shell.ApplySettings(new(KeepRunningInTray: true, PetEnabled: true));
            window.Close();
            Assert.IsFalse(window.IsVisible);
            shell.Pet!.WindowState = WindowState.Minimized;
            Assert.IsTrue(window.IsVisible, "A minimized pet must not become the last recovery surface.");
            shell.Pet.WindowState = WindowState.Normal;
            window.Close();
            Assert.IsFalse(window.IsVisible);
            shell.ApplySettings(new(KeepRunningInTray: true));
            Assert.IsTrue(window.IsVisible, "Hiding the last recovery surface must recover the main window.");
            shell.Quit();
        });
    }

    [TestMethod]
    public async Task LaunchFromDesktop_ShowsPromptsAndHonorsTheSharedCommandGuard()
    {
        await Run(() =>
        {
            var window = new Window();
            var launches = 0;
            var ready = true;
            var launch = new ActionCommand(() =>
            {
                Assert.IsTrue(window.IsVisible, "In-app prompts must be visible before starting the shared launch flow.");
                launches++;
                ready = false;
            }, () => ready);
            using var shell = new DesktopShellController(window, new TestI18n(), launch, window.Close, () => { });
            shell.SetTrayHostAvailable(true);
            shell.ApplySettings(new(KeepRunningInTray: true));
            window.Show();
            window.Close();
            var menuLaunch = ((NativeMenuItem)shell.TrayIcon!.Menu!.Items[1]).Command!;
            Assert.IsTrue(menuLaunch.CanExecute(null));
            shell.LaunchSelectedInstance();
            launch.NotifyCanExecuteChanged();
            Assert.IsFalse(menuLaunch.CanExecute(null));
            shell.LaunchSelectedInstance();
            Assert.AreEqual(1, launches);
            shell.Quit();
            shell.LaunchSelectedInstance();
            Assert.AreEqual(1, launches);
        });
    }

    [TestMethod]
    public async Task Pet_HidesWithoutBackgroundAnimationAndReusesItsWindow()
    {
        await Run(() =>
        {
            var window = new Window();
            using var shell = CreateShell(window);
            window.Show();
            shell.ApplySettings(new(PetEnabled: true));
            var pet = shell.Pet!;
            var tray = shell.TrayIcon!;
            var menu = tray.Menu;
            Assert.IsNull(pet.Owner);
            Assert.IsFalse(pet.ShowActivated);
            Assert.IsTrue(pet.Sprite.IsAnimationRunning);
            shell.ApplySettings(new());
            Assert.IsFalse(pet.Sprite.IsAnimationRunning);
            Assert.IsFalse(tray.IsVisible);
            Assert.IsFalse(shell.HasRecoverySurface);
            shell.ApplySettings(new(PetEnabled: true, PetTopmost: false, PetScale: 1.5));
            Assert.AreSame(pet, shell.Pet);
            Assert.AreSame(tray, shell.TrayIcon);
            Assert.AreSame(menu, shell.TrayIcon!.Menu);
            Assert.IsTrue(pet.Sprite.IsAnimationRunning);
            Assert.IsFalse(pet.Topmost);
            Assert.AreEqual(120d, pet.Width);
            shell.UpdateSavedPetPosition(new PixelPoint(200, 200));
            Assert.AreEqual(new PixelPoint(200, 200), pet.Position);
            shell.UpdateSavedPetPosition(null);
            Assert.AreNotEqual(new PixelPoint(200, 200), pet.Position, "Resetting saved settings must reset the live pet position too.");
            window.Close();
        });
    }

    [TestMethod]
    public async Task PetDrag_PersistsOnlyTheFinalPositionAndDoesNotRestoreTheLauncher()
    {
        await Run(() =>
        {
            var window = new Window();
            using var shell = CreateShell(window);
            shell.SetTrayHostAvailable(false);
            shell.ApplySettings(new(KeepRunningInTray: true, PetEnabled: true));
            window.Show();
            window.Close();
            var pet = shell.Pet!;
            pet.Position = new PixelPoint(100, 100);
            var saves = new List<PixelPoint>();
            shell.PetPositionChanged += saves.Add;
            pet.MouseDown(new Point(40, 40), MouseButton.Left, RawInputModifiers.None);
            pet.MouseMove(new Point(60, 60), RawInputModifiers.LeftMouseButton);
            Assert.AreEqual(0, saves.Count);
            pet.MouseUp(new Point(40, 40), MouseButton.Left, RawInputModifiers.None);
            Assert.AreEqual(1, saves.Count);
            Assert.AreEqual(pet.Position, saves[0]);
            Assert.IsFalse(window.IsVisible);
            shell.Quit();
        });
    }

    [TestMethod]
    public async Task LocaleChanges_RefreshBothMenusWithoutChangingPetVisibility()
    {
        await Run(() =>
        {
            var i18n = new TestI18n();
            var window = new Window();
            using var shell = new DesktopShellController(window, i18n, new ActionCommand(() => { }), window.Close, () => { });
            shell.ApplySettings(new(PetEnabled: true));
            var pet = shell.Pet!;
            var oldTitle = pet.Title;
            var trayMenu = shell.TrayIcon!.Menu;
            var petMenu = pet.ContextMenu;
            i18n.SetLocale("zh-Hans");
            Assert.AreNotEqual(oldTitle, pet.Title);
            Assert.AreEqual("zh-Hans:desktop.actions.show_launcher", ((NativeMenuItem)shell.TrayIcon!.Menu!.Items[0]).Header);
            Assert.AreEqual("zh-Hans:desktop.actions.show_launcher", ((MenuItem)pet.ContextMenu!.Items[0]!).Header);
            Assert.IsTrue(pet.IsVisible);
            Assert.AreSame(trayMenu, shell.TrayIcon.Menu);
            Assert.AreSame(petMenu, pet.ContextMenu);
            shell.Quit();
        });
    }

    private static DesktopShellController CreateShell(Window window) => new(
        window, new TestI18n(), new ActionCommand(() => { }), window.Close, () => { });

    private static Task Run(Action action) => FrontendMotionTest.RunAsync(() =>
    {
        action();
        return Task.CompletedTask;
    });

    private sealed class TestI18n : II18nService
    {
        public string Locale { get; private set; } = "en-US";
        public IReadOnlyList<string> AvailableLocales => ["en-US", "zh-Hans"];
        public event Action? Changed;
        public string T(string key) => Locale + ":" + key;
        public string T(string key, IReadOnlyDictionary<string, object?> args) => T(key);
        public string T(I18nText text) => T(text.ToString()!);
        public bool SetLocale(string locale)
        {
            Locale = locale;
            Changed?.Invoke();
            return true;
        }
        public bool ReloadLocaleFromSettings() => false;
        public bool ReloadCurrentLocale() => false;
    }
}

using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class ExtensionManagementE2E
{
    [TestMethod]
    public Task SettingsNavigation_RepeatedToggles_PetSettings_LocaleAndRestart() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenSettings();
        Assert.AreEqual(2, app.Window.GetVisualDescendants().OfType<ExtensionCardView>().Count());
        CollectionAssert.AreEquivalent(new[] { "pcl.desktop-pet", "pcl.global-search" }, app.Session.Host.Extensions.Select(info => info.Manifest.Id).ToArray());
        app.Screenshot("extensions-settings");
        foreach (var id in new[] { "pcl.desktop-pet", "pcl.global-search" })
        {
            for (var i = 0; i < 3; i++)
            {
                await app.Toggle(id);
                Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get(id).Status);
                await app.Toggle(id);
                Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get(id).Status);
            }
        }
        await app.Expand("pcl.desktop-pet");
        var card = app.Card("pcl.desktop-pet");
        var preset = card.GetVisualDescendants().OfType<ComboBox>().First();
        await Click(app.Window, preset);
        app.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Settle();
        var expectedPreset = app.Session.Host.Get("pcl.desktop-pet").Settings.Get("preset", "creeper_gif");
        Assert.AreNotEqual("creeper_gif", expectedPreset);
        await app.Toggle("pcl.global-search");
        Assert.AreSame(preset, card.GetVisualDescendants().OfType<ComboBox>().First(), "Other extensions must not recreate active editors.");
        app.Text.SetLocale("zh-Hans"); await Settle();
        Assert.AreEqual("PCL-ME 桌宠", card.Find<PclCard>("Card")!.Header);
        app.Screenshot("extensions-settings-zh");
        await app.Restart(); await app.OpenSettings();
        Assert.AreEqual(expectedPreset, app.Session.Host.Get("pcl.desktop-pet").Settings.Get("preset", ""));
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("pcl.global-search").Status);
    });

    [TestMethod]
    public Task ExternalPage_SearchNavigation_DisableCleanup_AndRestartPersistence() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenSettings();
        await Install(app, CreatePackage(app, "1.0.0"));
        await app.Toggle("example.hello");
        await OpenPage();
        var workspace = app.Window.Find<ContentControl>("ExtensionWorkspace")!;
        Assert.IsTrue(workspace.GetVisualDescendants().OfType<PclCard>().Any(card => card.Header == "Hello extension"),
            "An installed extension must render its native page using the shared PCL controls and its own localization.");
        await app.Search("Extensions");
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "Search did not navigate to extension settings");
        await app.Toggle("example.hello");
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Restart(); await app.OpenSettings();
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Toggle("example.hello");
        await OpenPage();
        await app.Session.Host.SetEnabledAsync("example.hello", false);
        await Settle();
        Assert.IsFalse(app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
        Assert.IsFalse((await app.Session.Host.QueryAsync(new("Hello"))).Any(result => result.CommandId == "example.hello.open"));

        async Task OpenPage()
        {
            await app.Search("Hello");
            app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Wait(() => app.Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible, "Search did not open the installed extension page");
        }
    });

    [TestMethod]
    public Task PackagePicker_Cancel_InvalidArchive_Install_Enable_Update_Restart_Uninstall() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenSettings();
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallButton")!);
        Assert.IsNotNull(app.Picker.Dialog);
        await Click(app.Picker.Dialog, app.Picker.Dialog.Find<Button>("PickerCancel")!);
        Assert.AreEqual(2, app.Session.Host.Extensions.Count);
        var invalid = Path.Combine(app.Root, "invalid.pclext"); File.WriteAllText(invalid, "Invalid ZIP");
        await Install(app, invalid);
        Assert.IsTrue(manager.Find<TextBlock>("FeedbackText")!.IsVisible);
        var package = CreatePackage(app, "1.0.0");
        await Install(app, package);
        Assert.IsFalse(manager.Find<TextBlock>("FeedbackText")!.IsVisible);
        Assert.AreEqual(ExtensionStatus.Disabled, app.Session.Host.Get("example.hello").Status);
        await app.Toggle("example.hello");
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get("example.hello").Status);
        await Install(app, CreatePackage(app, "2.0.0"));
        Assert.IsTrue(app.Session.Host.Get("example.hello").RestartRequired);
        await app.Restart(); await app.OpenSettings(); await app.Expand("example.hello");
        Assert.AreEqual("2.0.0", app.Session.Host.Get("example.hello").Manifest.Version);
        await Click(app.Window, app.Card("example.hello").Find<PclButton>("UninstallButton")!);
        Assert.IsTrue(app.Session.Host.Get("example.hello").RemovalPending);
        await app.Restart(); await app.OpenSettings();
        Assert.IsFalse(app.Session.Host.Extensions.Any(info => info.Manifest.Id == "example.hello"));
    });

    private static async Task Install(DesktopFixture app, string package)
    {
        var manager = app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single();
        await Click(app.Window, manager.Find<PclButton>("InstallButton")!);
        var dialog = app.Picker.Dialog!;
        await Type(dialog, dialog.Find<TextBox>("PickerPath")!, package);
        await Click(dialog, dialog.Find<Button>("PickerOpen")!);
        await Wait(() => manager.Find<PclButton>("InstallButton")!.IsEnabled, "Package operation did not finish");
    }

    [TestMethod]
    public Task Shortcuts_ConflictValidation_KeyboardDispatch_AndSafeModePreferences() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenSettings(); await app.Expand("pcl.desktop-pet");
        var pet = app.Card("pcl.desktop-pet");
        const string fieldName = "Shortcut_pcl_desktop_pet_hide";
        const string saveName = "SaveShortcut_pcl_desktop_pet_hide";
        await Type(app.Window, pet.Find<TextBox>(fieldName)!, "Primary+Shift+P");
        await Click(app.Window, pet.Find<PclButton>(saveName)!);
        Assert.IsTrue(pet.Find<TextBlock>("OperationErrorText")!.IsVisible);
        Assert.IsNull(app.Session.Host.Commands.Single(command => command.Definition.Id == "pcl.desktop-pet.hide").Definition.Shortcut);
        await Type(app.Window, pet.Find<TextBox>(fieldName)!, "Primary+Alt+H");
        await Click(app.Window, pet.Find<PclButton>(saveName)!);
        Assert.IsFalse(pet.Find<TextBlock>("OperationErrorText")!.IsVisible, pet.Find<TextBlock>("OperationErrorText")!.Text);
        app.Window.KeyPressQwerty(PhysicalKey.H, (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control) | RawInputModifiers.Alt);
        app.Window.KeyReleaseQwerty(PhysicalKey.H, RawInputModifiers.None);
        await Wait(() => app.Session.Host.Get("pcl.desktop-pet").Status == ExtensionStatus.Disabled, "Updated shortcut did not execute");
        await app.Restart(safeMode: true); await app.OpenSettings();
        Assert.IsTrue(app.Session.Host.SafeMode);
        Assert.AreEqual(0, app.Session.Windows.Count);
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Single().Find<TextBlock>("SafeModeText")!.IsVisible);
        await app.Toggle("pcl.desktop-pet");
        Assert.AreEqual(ExtensionStatus.Inactive, app.Session.Host.Get("pcl.desktop-pet").Status);
        Assert.AreEqual(0, app.Session.Windows.Count);
        await app.Restart(); await app.OpenSettings(); await app.Expand("pcl.desktop-pet");
        Assert.AreEqual(ExtensionStatus.Active, app.Session.Host.Get("pcl.desktop-pet").Status);
        Assert.AreEqual("Primary+Alt+H", app.Card("pcl.desktop-pet").Find<TextBox>(fieldName)!.Text);
    });

    [TestMethod]
    public Task SettingsEditors_Reset_DarkTheme_AllLocales_AndReturnNavigation() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await app.OpenSettings(); await app.Expand("pcl.global-search");
        var card = app.Card("pcl.global-search");
        var limit = card.Find<NumericUpDown>("Setting_limit")!;
        await Type(app.Window, limit.GetVisualDescendants().OfType<TextBox>().Single(), "12");
        await Click(app.Window, card.Find<PclButton>("ResetButton")!);
        Assert.AreEqual(30, app.Session.Host.Get("pcl.global-search").Settings.Get("limit", 0d));
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        Workflows.FrontendAppearanceService.ApplyStoredAppearance(global::Avalonia.Application.Current!, app.Paths);
        Assert.AreEqual(global::Avalonia.Styling.ThemeVariant.Dark, app.Window.ActualThemeVariant);
        foreach (var locale in new[] { "en-US", "zh-Hans", "zh-Hant", "lzh", "de-DE", "fr-FR", "es-ES", "ko-KR" })
        {
            app.Text.SetLocale(locale); await Settle();
            Assert.AreSame(card, app.Card("pcl.global-search"));
            var title = card.Find<PclCard>("Card")!.FindControl<TextBlock>("HeaderTextBlock")!;
            var toggle = card.Find<CheckBox>("EnabledToggle")!;
            Assert.IsTrue(title.Bounds.Right <= toggle.TranslatePoint(default, title.GetVisualParent()!)!.Value.X,
                "Extension title overlaps its checkbox in " + locale);
        }
        app.Text.SetLocale("en-US"); await Task.Delay(500); Tick();
        card.BringIntoView(); await Settle(); app.Screenshot("extensions-dark");
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Misc"));
        Assert.IsFalse(app.Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any());
        await app.OpenSettings();
        Assert.IsFalse(app.Card("pcl.global-search").Find<PclCard>("Card")!.IsChevronExpanded);
    });

    [TestMethod]
    public Task InstanceSearch_NativeTab_AndDisableCleanup() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        var instance = app.CreateInstance("E2E-example");
        await app.Start(); await app.OpenSettings();
        await Install(app, CreatePackage(app, "1.0.0")); await app.Toggle("example.hello");
        await app.Search("E2E-example");
        var search = app.Window;
        search.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        if (search.IsVisible) search.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => app.Window.GetVisualDescendants().OfType<TabItem>().Any(tab => (string?)tab.Header == "Extension information"), "Instance search did not open the extension tab");
        var tab = app.Window.GetVisualDescendants().OfType<TabItem>().Single(tab => (string?)tab.Header == "Extension information");
        Assert.AreEqual(instance, ((TextBlock)tab.Content!).Text);
        await app.OpenSettings(); await app.Toggle("example.hello");
        Assert.IsNull(app.Launcher.ExtensionInstanceTabsContent);
    });

    private static string CreatePackage(DesktopFixture app, string version)
    {
        var path = Path.Combine(app.Root, "hello-" + version + ".pclext");
        var manifest = new ExtensionManifest
        {
            Id = "example.hello", Name = "Hello extension", Version = version, AvaloniaMajorVersion = 11, StartWithLauncher = true,
            EntryAssembly = "HelloExtension.dll", EntryType = "HelloExtension.HelloExtension",
            Commands = [new() { Id = "example.hello.open", Title = "Hello" }]
        };
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var stream = zip.CreateEntry("extension.json").Open()) JsonSerializer.Serialize(stream, manifest, ExtensionPackage.JsonOptions);
        zip.CreateEntryFromFile(typeof(HelloExtension.HelloExtension).Assembly.Location, "HelloExtension.dll");
        foreach (var locale in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Locales"), "*.json"))
            zip.CreateEntryFromFile(locale, "Locales/" + Path.GetFileName(locale));
        return path;
    }
}

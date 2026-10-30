using System.Reflection;
using System.Net;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Core.App.I18n;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.IO.Net.Http.Proxying;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendAppearanceTest
{
    [TestMethod]
    public async Task DesktopPreferences_PersistAcrossNavigationLocaleChangesAndReset()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            var launcher = fixture.Launcher;
            Assert.IsFalse(launcher.KeepRunningInTray);
            Assert.IsTrue(launcher.DesktopPetEnabled);
            launcher.KeepRunningInTray = true;
            launcher.DesktopPetEnabled = true;
            launcher.DesktopPetTopmost = false;
            launcher.DesktopPetScale = 1.5;
            launcher.SelectedDesktopPetPresetIndex = 1;
            fixture.Actions.FlushPendingSettings();
            var expected = new FrontendDesktopSettings(true, true, false, 1.5, false, "slime",
                PetClickAction: PetAction.PlayAnimation, CloseAction: WindowCloseAction.MinimizeToTray);
            Assert.AreEqual(expected, FrontendDesktopSettingsService.Read(new YamlFileProvider(fixture.Paths.LocalConfigPath)));

            fixture.Navigate(LauncherFrontendSubpageKey.SetupLaunch);
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            Assert.AreEqual(expected, launcher.DesktopSettings);
            fixture.I18n.SetLocale("zh-Hans");
            Tick();
            Assert.AreEqual(expected, launcher.DesktopSettings);

            var customImage = Path.Combine(fixture.Paths.DataDirectory, "custom.png");
            fixture.Actions.PersistLocalValue(FrontendDesktopSettingsService.PetImagePathKey, customImage);
            fixture.Actions.PersistLocalValue(FrontendDesktopSettingsService.PetUseCustomKey, true);
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLaunch);
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            Assert.AreEqual(customImage, launcher.DesktopPetImagePath);
            Assert.IsTrue(launcher.HasDesktopPetImage);
            Assert.IsTrue(launcher.DesktopPetUseCustom);
            Assert.IsFalse(launcher.IsDesktopPetPresetSelectionEnabled);
            Assert.IsTrue(launcher.IsDesktopPetCustomSelectionEnabled);

            fixture.I18n.SetLocale("fr-FR");
            Tick();
            Assert.IsTrue(launcher.HasDesktopPetImage, "Translating the settings must preserve the custom image.");

            launcher.SelectedDesktopPetPresetIndex = 2;
            Assert.IsFalse(launcher.DesktopPetUseCustom);
            Assert.AreEqual("grass_block", launcher.DesktopSettings.PetPresetId);
            Assert.AreEqual(customImage, launcher.DesktopPetImagePath,
                "Switching to a preset keeps the custom image for later reuse.");

            fixture.Actions.PersistLocalValue(FrontendDesktopSettingsService.PetXKey, -1800);
            fixture.Actions.PersistLocalValue(FrontendDesktopSettingsService.PetYKey, 200);
            // Simulate a profile upgraded from the pre-preset build, where only the legacy key exists.
            fixture.Actions.PersistLocalValue(FrontendDesktopSettingsService.PetAppearanceKey, "slime");
            launcher.ResetLauncherMiscSettingsCommand.Execute(null);
            Tick();
            Assert.AreEqual(new FrontendDesktopSettings(PetEnabled: true, CloseAction: WindowCloseAction.MinimizeToTray),
                launcher.DesktopSettings);
            Assert.IsNull(FrontendDesktopSettingsService.ReadPosition(fixture.Paths.OpenLocalConfigProvider()));
            Assert.IsFalse(fixture.Paths.OpenLocalConfigProvider().Exists(FrontendDesktopSettingsService.PetAppearanceKey),
                "Resetting must drop the migrated legacy appearance key.");
        });
    }

    [TestMethod]
    public async Task SettingsNavigation_PreservesActiveNetworkConfiguration()
    {
        await RunOnUiThread(() =>
        {
            var originalDns = FrontendHttpProxyService.CurrentSecureDnsConfiguration;
            var originalProxy = HttpClient.DefaultProxy;
            try
            {
                using var fixture = new AppearanceFixture();
                fixture.Actions.PersistLocalValue("SystemSystemUpdate", (int)LauncherUpdateMode.Disabled);
                var activeProxy = new WebProxy();
                var activeDns = new FrontendSecureDnsConfiguration(
                    FrontendSecureDnsMode.DnsOverTls, FrontendSecureDnsProvider.Cloudflare);
                HttpClient.DefaultProxy = activeProxy;
                FrontendHttpProxyService.ApplySecureDnsConfiguration(activeDns);

                // Exercise the same sidebar commands used by the UI, including repeat visits.
                var entries = fixture.Launcher.SidebarSections.SelectMany(section => section.Items).ToArray();
                foreach (var entry in entries.Concat(entries.Reverse()))
                {
                    entry.Command.Execute(null);
                    Assert.AreSame(activeProxy, HttpClient.DefaultProxy, entry.Title);
                    Assert.AreEqual(activeDns, FrontendHttpProxyService.CurrentSecureDnsConfiguration, entry.Title);
                }
            }
            finally
            {
                HttpClient.DefaultProxy = originalProxy;
                FrontendHttpProxyService.ApplySecureDnsConfiguration(originalDns);
            }
        });
    }

    [TestMethod]
    public async Task SettingsNetworkChanges_ApplyImmediatelyAndSurviveNavigation()
    {
        await RunOnUiThread(() =>
        {
            var originalDns = FrontendHttpProxyService.CurrentSecureDnsConfiguration;
            var originalProxy = HttpClient.DefaultProxy;
            try
            {
                using var fixture = new AppearanceFixture();
                fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
                fixture.Launcher.SelectedHttpProxyTypeIndex = 0;
                fixture.Launcher.SelectedSecureDnsModeIndex = (int)FrontendSecureDnsMode.DnsOverTls;
                fixture.Launcher.SelectedSecureDnsProviderIndex = (int)FrontendSecureDnsProvider.Cloudflare;

                Assert.IsInstanceOfType<ProxyManager>(HttpClient.DefaultProxy);
                Assert.AreEqual(ProxyMode.NoProxy, ((ProxyManager)HttpClient.DefaultProxy).Mode);
                var expectedDns = new FrontendSecureDnsConfiguration(
                    FrontendSecureDnsMode.DnsOverTls, FrontendSecureDnsProvider.Cloudflare);
                Assert.AreEqual(expectedDns, FrontendHttpProxyService.CurrentSecureDnsConfiguration);

                fixture.Navigate(LauncherFrontendSubpageKey.SetupLaunch);
                fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
                Assert.AreEqual(0, fixture.Launcher.SelectedHttpProxyTypeIndex);
                Assert.AreEqual((int)FrontendSecureDnsMode.DnsOverTls, fixture.Launcher.SelectedSecureDnsModeIndex);
                Assert.AreEqual((int)FrontendSecureDnsProvider.Cloudflare, fixture.Launcher.SelectedSecureDnsProviderIndex);
                Assert.AreEqual(ProxyMode.NoProxy, ((ProxyManager)HttpClient.DefaultProxy).Mode);
                Assert.AreEqual(expectedDns, FrontendHttpProxyService.CurrentSecureDnsConfiguration);
            }
            finally
            {
                HttpClient.DefaultProxy = originalProxy;
                FrontendHttpProxyService.ApplySecureDnsConfiguration(originalDns);
            }
        });
    }

    [TestMethod]
    public async Task AppearanceChoices_ReuseListsAndRefreshTranslationsAfterLocaleChange()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            var launcher = fixture.Launcher;
            var fonts = launcher.FontOptions;
            var presets = launcher.HomepagePresetOptions;
            var groups = launcher.UiFeatureToggleGroups.ToArray();
            Assert.IsTrue(groups.Length > 0);
            Assert.AreSame(fonts, launcher.FontOptions);
            Assert.AreSame(presets, launcher.HomepagePresetOptions);

            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            fixture.Navigate(LauncherFrontendSubpageKey.SetupUI);
            CollectionAssert.AreEqual(groups, launcher.UiFeatureToggleGroups.ToArray());

            var toggle = groups.SelectMany(group => group.Items).First(item => item.ConfigKey == "UiHiddenPageDownload");
            toggle.IsChecked = !toggle.IsChecked;
            Assert.AreSame(toggle, launcher.UiFeatureToggleGroups.SelectMany(group => group.Items)
                .First(item => item.ConfigKey == toggle.ConfigKey));
            Assert.AreEqual(toggle.IsChecked, fixture.Paths.OpenLocalConfigProvider().Get<bool>(toggle.ConfigKey!));

            fixture.Paths.OpenLocalConfigProvider().Set(toggle.ConfigKey!, !toggle.IsChecked);
            fixture.Paths.OpenLocalConfigProvider().Sync();
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            fixture.Navigate(LauncherFrontendSubpageKey.SetupUI);
            Assert.AreEqual(!toggle.IsChecked, launcher.UiFeatureToggleGroups.SelectMany(group => group.Items)
                .First(item => item.ConfigKey == toggle.ConfigKey).IsChecked);

            Assert.IsTrue(fixture.I18n.SetLocale("zh-Hans"));
            Tick();
            Assert.AreNotSame(fonts, launcher.FontOptions);
            Assert.AreNotSame(presets, launcher.HomepagePresetOptions);
            Assert.AreNotEqual(fonts[0], launcher.FontOptions[0]);
            Assert.AreNotEqual(presets[0], launcher.HomepagePresetOptions[0]);
            Assert.AreNotEqual(groups[0].Title, launcher.UiFeatureToggleGroups[0].Title);
            Assert.AreSame(launcher.FontOptions, launcher.FontOptions);
            Assert.AreSame(launcher.HomepagePresetOptions, launcher.HomepagePresetOptions);
        });
    }

    [TestMethod]
    [DataRow(LauncherFrontendSubpageKey.SetupLaunch, false)]
    [DataRow(LauncherFrontendSubpageKey.SetupLaunch, true)]
    [DataRow(LauncherFrontendSubpageKey.SetupGameManage, false)]
    [DataRow(LauncherFrontendSubpageKey.SetupGameManage, true)]
    [DataRow(LauncherFrontendSubpageKey.SetupLauncherMisc, false)]
    [DataRow(LauncherFrontendSubpageKey.SetupLauncherMisc, true)]
    [DataRow(LauncherFrontendSubpageKey.SetupUpdate, false)]
    [DataRow(LauncherFrontendSubpageKey.SetupUpdate, true)]
    public async Task SettingsChoices_PreserveSelectionsAfterLocaleChange(
        LauncherFrontendSubpageKey subpage, bool detachDuringLocaleChange)
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            fixture.Actions.PersistLocalValue("SystemSystemUpdate", (int)LauncherUpdateMode.Disabled);
            fixture.Navigate(subpage);
            UserControl view = subpage switch
            {
                LauncherFrontendSubpageKey.SetupLaunch => new SetupLaunchRightPaneView(),
                LauncherFrontendSubpageKey.SetupGameManage => new SetupGameManageRightPaneView(),
                LauncherFrontendSubpageKey.SetupLauncherMisc => new SetupLauncherMiscRightPaneView(),
                LauncherFrontendSubpageKey.SetupUpdate => new SetupUpdateRightPaneView(),
                _ => throw new ArgumentOutOfRangeException(nameof(subpage))
            };
            view.DataContext = fixture.Launcher;
            var window = new Window { Width = 1000, Height = 900, Content = view };
            window.Show();
            Tick();
            foreach (var card in view.GetVisualDescendants().OfType<PclCard>().ToArray())
            {
                card.IsChevronExpanded = true;
            }
            Tick();
            var combos = view.GetVisualDescendants().OfType<ComboBox>().ToArray();
            Assert.IsTrue(combos.Length > 0);
            if (subpage is LauncherFrontendSubpageKey.SetupLaunch or LauncherFrontendSubpageKey.SetupGameManage)
            {
                foreach (var combo in combos) combo.SelectedIndex = 1;
            }
            var selections = combos.Select(combo => combo.SelectedIndex).ToArray();
            Assert.IsTrue(selections.All(index => index >= 0), "Settings must initially have selections.");
            var properties = typeof(LauncherViewModel).GetProperties()
                .Where(property => property.PropertyType == typeof(int) &&
                    property.Name.StartsWith("Selected", StringComparison.Ordinal) &&
                    property.Name.EndsWith("Index", StringComparison.Ordinal)).ToArray();
            var modelSelections = properties.Select(property => property.GetValue(fixture.Launcher)).ToArray();

            foreach (var locale in new[] { "zh-Hans", "zh-Hant", "en-US" })
            {
                if (detachDuringLocaleChange)
                {
                    window.Content = null;
                    fixture.Navigate(LauncherFrontendSubpageKey.SetupUI);
                }
                Assert.IsTrue(fixture.I18n.SetLocale(locale));
                Tick();
                if (detachDuringLocaleChange)
                {
                    fixture.Navigate(subpage);
                    window.Content = view;
                    Tick();
                }
                // Card templates can recreate their controls when the pane is reattached.
                combos = view.GetVisualDescendants().OfType<ComboBox>().ToArray();
                Assert.AreEqual(selections.Length, combos.Length);
                for (var index = 0; index < combos.Length; index++)
                {
                    Assert.AreEqual(selections[index], combos[index].SelectedIndex, $"{subpage}, {locale}, option {index}");
                    Assert.AreEqual(combos[index].Items[selections[index]], combos[index].SelectedItem);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(combos[index].SelectedItem?.ToString()));
                }
                foreach (var (property, index) in properties.Select((property, index) => (property, index)))
                {
                    if (property.Name == nameof(LauncherViewModel.SelectedLauncherLocaleIndex)) continue;
                    Assert.AreEqual(modelSelections[index], property.GetValue(fixture.Launcher), property.Name);
                }
            }
            window.Close();
        });
    }

    [TestMethod]
    public async Task AppearanceSections_LoadCurrentSelectionsAndRetainScrollPositionOnReattach()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            var launcher = fixture.Launcher;
            var view = new SetupUiRightPaneView { DataContext = launcher };
            var window = new Window { Width = 900, Height = 300, Content = view };
            window.Show();
            Tick();
            var items = view.FindControl<StackPanel>("AppearanceSections")!;
            var sections = items.Children.Cast<PclDeferredContentControl>().ToArray();
            Assert.IsNull(sections[4].Content, "Homepage content should start outside the viewport.");

            launcher.SelectedHomepagePresetIndex = 2;
            Assert.IsTrue(fixture.I18n.SetLocale("zh-Hans"));
            Tick();
            sections[4].BringIntoView();
            Tick();
            var homepage = (Control)sections[4].Content!;
            var combo = ((Control)((PclCard)homepage).CardContent!).GetLogicalDescendants().OfType<ComboBox>()
                .Single(control => control.Name == "HomepagePresetComboBox");
            Assert.IsNotNull(combo, "Deferred templates must retain their named selection controls.");
            Assert.AreEqual(launcher.SelectedHomepagePresetIndex, combo.SelectedIndex);
            Assert.AreSame(launcher.HomepagePresetOptions, combo.ItemsSource);
            Assert.IsTrue(fixture.I18n.SetLocale("en-US"));
            Tick();
            Assert.AreEqual(launcher.SelectedHomepagePresetIndex, combo.SelectedIndex);
            Assert.AreSame(launcher.HomepagePresetOptions, combo.ItemsSource);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
            var offset = scroll.Offset;
            Assert.IsTrue(offset.Y > 0);

            window.Content = null;
            window.Content = view;
            Tick();
            Assert.AreSame(homepage, sections[4].Content);
            Assert.AreEqual(offset, scroll.Offset);
            Assert.AreEqual(launcher.SelectedHomepagePresetIndex, combo.SelectedIndex);

            for (var repeat = 0; repeat < 2; repeat++)
            {
                foreach (var index in new[] { 1, 2, 3, 4, 5, 0 })
                {
                    sections[index].BringIntoView();
                    Tick();
                }
            }
            scroll.ScrollToHome();
            Tick();
            Assert.AreEqual(0d, sections[0].Bounds.Y, .01, "Returning home must preserve the start of the layout.");

            launcher.UiFeatureToggleGroups.SelectMany(group => group.Items)
                .Single(item => item.ConfigKey == "UiHiddenFunctionHidden").IsChecked = true;
            Tick();
            sections[5].BringIntoView();
            Tick();
            Assert.IsFalse(launcher.ShowUiFeatureHiddenCard);
            Assert.AreEqual(0d, sections[5].DesiredSize.Height, .01, "A hidden final card must not leave a spacing gap.");
            launcher.ToggleHiddenItemsOverride();
            Tick();
            sections[5].BringIntoView();
            Tick();
            Assert.IsTrue(((PclCard)sections[5].Content!).IsEffectivelyVisible);
            window.Close();
        });
    }

    private sealed class AppearanceFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-appearance-test", Guid.NewGuid().ToString("N"));

        public FrontendRuntimePaths Paths { get; }
        public I18nService I18n { get; }
        public LauncherViewModel Launcher { get; }
        public LauncherActionService Actions { get; }

        public AppearanceFixture()
        {
            Paths = new FrontendRuntimePaths(_root, Path.Combine(_root, "temp"), Path.Combine(_root, "data"),
                Path.Combine(_root, "shared"), Path.Combine(_root, "shared", "config.json"),
                Path.Combine(_root, "data", "config.yml"), Path.Combine(_root, "appdata"), []);
            I18n = new I18nService(new I18nSettingsManager(new FrontendConfigProviderAdapter(Paths)));
            Actions = new LauncherActionService(Paths, new FrontendPlatformAdapter(), () => { }, I18n);
            Launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), Actions, I18n);
            Navigate(LauncherFrontendSubpageKey.SetupUI);
        }

        public void Navigate(LauncherFrontendSubpageKey subpage)
        {
            var navigate = typeof(LauncherViewModel).GetMethod("NavigateTo", BindingFlags.Instance | BindingFlags.NonPublic)!;
            navigate.Invoke(Launcher, [new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, subpage),
                "Appearance test navigation", Activator.CreateInstance(navigate.GetParameters()[2].ParameterType)]);
            Tick();
        }

        public void Dispose()
        {
            Actions.FlushPendingSettings();
            I18n.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public async Task AppearanceSlider_UpdatesImmediatelyAndSavesOnReleaseKeyboardCompletionAndClose()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            var window = new MainWindow { DataContext = fixture.Launcher };
            var slider = new Slider { Minimum = 0, Maximum = 600 };
            slider.Bind(RangeBase.ValueProperty, new Binding(nameof(LauncherViewModel.LauncherOpacity))
            {
                Source = fixture.Launcher,
                Mode = BindingMode.TwoWay
            });
            window.Content = slider;
            window.Show();
            Tick();
            fixture.Actions.FlushPendingSettings();
            fixture.Actions.PersistLocalValue("UiLauncherTransparent", (int)Math.Round(fixture.Launcher.LauncherOpacity));
            var before = File.ReadAllText(fixture.Paths.LocalConfigPath);

            slider.Value = 400;
            Assert.AreEqual(.8d, window.Opacity, .0001);
            Assert.AreEqual(400d, fixture.Launcher.LauncherOpacity);
            Assert.AreEqual(before, File.ReadAllText(fixture.Paths.LocalConfigPath));
            window.MouseUp(new Point(20, 20), MouseButton.Left, RawInputModifiers.None);
            Assert.AreEqual(400, new YamlFileProvider(fixture.Paths.LocalConfigPath).Get<int>("UiLauncherTransparent"));

            Assert.IsTrue(slider.Focus());
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Assert.AreEqual((int)Math.Round(slider.Value),
                new YamlFileProvider(fixture.Paths.LocalConfigPath).Get<int>("UiLauncherTransparent"));

            slider.Value = 420;
            window.Close();
            Assert.AreEqual(420, new YamlFileProvider(fixture.Paths.LocalConfigPath).Get<int>("UiLauncherTransparent"));
        });
    }

    [TestMethod]
    public async Task AppearanceSlider_UsesCurrentValuesForNavigationAndSettingsSnapshots()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            fixture.Launcher.LauncherOpacity = 410;
            fixture.Actions.FlushPendingSettings();
            var snapshot = FrontendSettingsSnapshotWorkflowService.CreateSnapshot(fixture.Paths);
            Assert.AreEqual(410, new YamlFileProvider(Path.Combine(snapshot, Path.GetFileName(fixture.Paths.LocalConfigPath)))
                .Get<int>("UiLauncherTransparent"));

            fixture.Launcher.LauncherOpacity = 430;
            fixture.Navigate(LauncherFrontendSubpageKey.SetupLauncherMisc);
            Assert.AreEqual(430, new YamlFileProvider(fixture.Paths.LocalConfigPath).Get<int>("UiLauncherTransparent"));
            fixture.Actions.FlushPendingSettings();
            FrontendSettingsSnapshotWorkflowService.RestoreSnapshot(fixture.Paths, snapshot);
            fixture.Actions.FlushPendingSettings();
            Assert.AreEqual(410, fixture.Paths.OpenLocalConfigProvider().Get<int>("UiLauncherTransparent"));
        });
    }

    [TestMethod]
    public async Task InstanceSlider_PreservesOtherEditsAndExitSavesPendingGlobalValues()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new AppearanceFixture();
            var instance = Path.Combine(fixture.Paths.DataDirectory, "TestInstance");
            Directory.CreateDirectory(Path.Combine(instance, "mods"));
            var config = Path.Combine(instance, "PCL", "config.v1.yml");
            fixture.Actions.PersistInstanceValue(instance, "VersionRamCustom", 3d);
            fixture.Actions.PersistInstanceValueDeferred(instance, "VersionRamCustom", 6d);
            fixture.Actions.PersistInstanceValueDeferred(instance, "VersionRamCustom", 8d);
            Assert.AreEqual(3d, new YamlFileProvider(config).Get<double>("VersionRamCustom"));
            fixture.Actions.PersistInstanceValue(instance, "VersionArgumentTitle", "My Minecraft");
            Assert.AreEqual(8d, new YamlFileProvider(config).Get<double>("VersionRamCustom"));
            Assert.AreEqual("My Minecraft", new YamlFileProvider(config).Get<string>("VersionArgumentTitle"));

            fixture.Actions.PersistLocalValueDeferred("UiLauncherTransparent", 470);
            fixture.Actions.ExitLauncher();
            Assert.AreEqual(470, new YamlFileProvider(fixture.Paths.LocalConfigPath).Get<int>("UiLauncherTransparent"));
        });
    }
}

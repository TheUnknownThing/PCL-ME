using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Extensions.Avalonia;
using PCL.Extensions.DesktopPet;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendProfileEditingTest;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendExtensionsTest
{
    [TestMethod]
    public async Task PetExtension_DisablingClosesItsWindowAndRecoversTheHiddenLauncher()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            await using var fixture = new SessionFixture();
            await fixture.Session.StartAsync();
            await fixture.Session.Host.SetEnabledAsync("pcl.desktop-pet", true);
            var pet = fixture.Session.Windows.OfType<DesktopPetWindow>().Single();
            Assert.IsTrue(pet.Sprite.IsAnimationRunning);
            Assert.IsNull(pet.Owner);
            fixture.Shell.SetTrayHostAvailable(false);
            fixture.Window.Close();
            Assert.IsFalse(fixture.Window.IsVisible);
            await fixture.Session.Host.SetEnabledAsync("pcl.desktop-pet", false);
            Tick();
            Assert.IsTrue(fixture.Window.IsVisible);
            Assert.IsFalse(pet.IsVisible);
            Assert.IsFalse(pet.Sprite.IsAnimationRunning);
            Assert.IsFalse(fixture.Session.Windows.Contains(pet));
            await fixture.Session.Host.SetEnabledAsync("pcl.desktop-pet", true);
            Assert.AreNotSame(pet, fixture.Session.Windows.OfType<DesktopPetWindow>().Single());
            await fixture.DisposeSessionAsync();
        });
    }

    [TestMethod]
    public async Task PetExtension_SettingsAndLocaleChangesStayInsideTheExtension()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            await using var fixture = new SessionFixture();
            await fixture.Session.StartAsync();
            await fixture.Session.Host.SetEnabledAsync("pcl.desktop-pet", true);
            var settings = fixture.Session.Host.Get("pcl.desktop-pet").Settings;
            var pet = fixture.Session.Windows.OfType<DesktopPetWindow>().Single();
            var preview = (UserControl)fixture.Session.CreateSettingsViews("pcl.desktop-pet").Single();
            using var previewLifetime = (IDisposable)preview;
            settings.Set("preset", "slime"); settings.Set("topmost", false); settings.Set("scale", 1.5);
            Tick();
            Assert.AreEqual("slime", pet.Sprite.Preset!.Id);
            Assert.AreEqual("slime", ((DesktopPetSprite)preview.Content!).Preset!.Id);
            Assert.IsFalse(pet.Topmost);
            var title = pet.Title;
            fixture.Text.SetLocale("zh-Hans"); Tick();
            Assert.AreNotEqual(title, pet.Title);
            Assert.AreEqual("slime", settings.Get("preset", ""));
            Assert.IsFalse(fixture.Paths.OpenLocalConfigProvider().Exists("UiDesktopPetPreset"));
            fixture.Window.Close();
            var click = new global::Avalonia.Point(30, 30);
            settings.Set("click", "ShowLauncher"); Tick();
            pet.MouseDown(click, MouseButton.Left, RawInputModifiers.None);
            pet.MouseUp(click, MouseButton.Left, RawInputModifiers.None); Tick();
            Assert.IsTrue(fixture.Window.IsVisible);
            await fixture.DisposeSessionAsync();
        });
    }

    [TestMethod]
    public async Task GlobalSearch_CombinesSettingsCommandsAndVersionsAndReleasesItsShortcut()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            await using var fixture = new SessionFixture();
            await fixture.Session.StartAsync();
            var results = await fixture.Session.Host.QueryAsync(new("1.8.9"));
            Assert.IsTrue(results.Any(result => result.Arguments?.GetValueOrDefault("query") == "1.8.9"));
            var commands = await fixture.Session.Host.QueryAsync(new(""));
            Assert.IsTrue(commands.Any(result => result.CommandId is not null));
            fixture.Text.SetLocale("zh-Hans"); Tick();
            var translated = await fixture.Session.Host.QueryAsync(new(fixture.Text.T("desktop.actions.show_launcher")));
            Assert.IsTrue(translated.Any(result => result.CommandId == "pcl.launcher.show" && result.Title == fixture.Text.T("desktop.actions.show_launcher")));
            fixture.Launcher.DismissPromptOverlayCommand.Execute(null); Tick();
            await fixture.Session.Host.ExecuteCommandAsync("pcl.global-search.open");
            Assert.IsNotNull(fixture.Session.OverlayContent);
            await fixture.Session.Host.SetEnabledAsync("pcl.global-search", false);
            Assert.IsNull(fixture.Session.OverlayContent);
            Assert.IsFalse(fixture.Session.Host.Commands.Any(command => command.Definition.Id == "pcl.global-search.open"));
            await fixture.DisposeSessionAsync();
        });
    }

    [TestMethod]
    public async Task ExternalPackage_RegistersNativePagesSearchAndAnInstanceTabWithoutFrontendReferences()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            await using var fixture = new SessionFixture();
            var manifest = new ExtensionManifest
            {
                Id = "example.hello", Name = "Hello extension", Version = "1.0.0", AvaloniaMajorVersion = 11,
                EntryAssembly = "HelloExtension.dll", EntryType = "HelloExtension.HelloExtension",
                Commands = [new() { Id = "example.hello.open", Title = "Hello" }]
            };
            var package = Path.Combine(fixture.Paths.DataDirectory, "hello.pclext");
            Directory.CreateDirectory(fixture.Paths.DataDirectory);
            using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                using (var output = zip.CreateEntry("extension.json").Open()) JsonSerializer.Serialize(output, manifest, ExtensionPackage.JsonOptions);
                zip.CreateEntryFromFile(typeof(HelloExtension.HelloExtension).Assembly.Location, "HelloExtension.dll");
            }
            var info = await fixture.Session.Host.InstallAsync(package);
            await fixture.Session.Host.SetEnabledAsync(info.Manifest.Id, true);
            fixture.Launcher.DismissPromptOverlayCommand.Execute(null);
            var composition = (Models.FrontendInstanceComposition)GetField(fixture.Launcher, "_instanceComposition")!;
            var selected = composition with { Selection = composition.Selection with { HasSelection = true, InstanceDirectory = "/test/instance" } };
            typeof(ViewModels.LauncherViewModel).GetField("_instanceComposition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Launcher, selected);
            Tick();
            var tabs = fixture.Launcher.ExtensionInstanceTabsContent as TabControl;
            Assert.IsNotNull(tabs);
            Assert.AreEqual("/test/instance", ((TextBlock)((TabItem)tabs.Items[0]!).Content!).Text);
            Assert.AreEqual(1, fixture.Session.CreateInstanceMenu().Items.Count);
            await fixture.Session.Host.ExecuteCommandAsync("example.hello.open");
            Assert.IsTrue(fixture.Window.FindControl<ContentControl>("ExtensionWorkspace")!.IsVisible);
            Tick();
            Assert.IsTrue(fixture.Window.FindControl<ContentControl>("ExtensionWorkspace")!.GetVisualDescendants().OfType<PclCard>().Any(), "External native pages must use the same PCL.UI controls as the launcher.");
            Assert.IsTrue((await fixture.Session.Host.QueryAsync(new("hello"))).Any(result => result.CommandId == "example.hello.open"));
            await fixture.Session.Host.SetEnabledAsync(info.Manifest.Id, false);
            Tick();
            Assert.IsFalse(fixture.Window.FindControl<ContentControl>("ExtensionWorkspace")!.IsVisible);
            Assert.IsNull(fixture.Launcher.ExtensionInstanceTabsContent);
            Assert.AreEqual(0, fixture.Session.CreateInstanceMenu().Items.Count);
            await fixture.DisposeSessionAsync();
        });
    }

    [TestMethod]
    public async Task NativeDialogs_CloseAndCancelTheirCommandWhenAnExtensionIsDisabled()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            await using var fixture = new SessionFixture();
            var manifest = ExtensionRuntimeTest.Manifest("test.dialog");
            fixture.Session.Host.AddBuiltIn(manifest, fixture.Paths.DataDirectory, () => new ExtensionRuntimeTest.TestExtension(context =>
            {
                var ui = context.GetService<IExtensionUi>();
                context.Commands.Register(manifest.Commands.Single(), token => ui.ShowDialogAsync(new Window(), token));
                return ValueTask.CompletedTask;
            }));
            var execution = fixture.Session.Host.ExecuteCommandAsync("test.dialog.run").AsTask();
            Tick();
            Assert.AreEqual(1, fixture.Session.Windows.Count);
            await fixture.Session.Host.SetEnabledAsync(manifest.Id, false);
            await Assert.ThrowsAsync<OperationCanceledException>(() => execution);
            Assert.AreEqual(0, fixture.Session.Windows.Count);
            Assert.AreEqual(ExtensionStatus.Disabled, fixture.Session.Host.Get(manifest.Id).Status);
        });
    }

    internal sealed class SessionFixture : IAsyncDisposable
    {
        private readonly ProfileFixture _fixture = new();
        public I18nService Text { get; }
        public FrontendRuntimePaths Paths => _fixture.Paths;
        public ViewModels.LauncherViewModel Launcher => _fixture.Launcher;
        public MainWindow Window { get; }
        public DesktopShellController Shell { get; }
        public FrontendExtensionSession Session { get; }
        private bool _closed;
        public SessionFixture()
        {
            Text = (I18nService)GetField(_fixture, "_i18n")!;
            var actions = (LauncherActionService)GetField(_fixture, "_actions")!;
            typeof(ViewModels.LauncherViewModel).GetField("_welcomeOverlayDismissed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_fixture.Launcher, true);
            Window = new MainWindow { DataContext = _fixture.Launcher };
            Shell = new(Window, Text, _fixture.Launcher.LaunchCommand, Window.Close, actions.FlushPendingSettings);
            Shell.ApplySettings(new(KeepRunningInTray: true));
            Session = new(Window, Shell, _fixture.Launcher, actions, Text, safeMode: false);
            Window.Show(); Tick();
        }
        public async ValueTask DisposeSessionAsync() { if (_closed) return; _closed = true; await Session.DisposeAsync(); }
        public async ValueTask DisposeAsync()
        {
            await DisposeSessionAsync();
            Shell.Dispose(); Window.Close(); _fixture.Dispose();
        }
    }
}

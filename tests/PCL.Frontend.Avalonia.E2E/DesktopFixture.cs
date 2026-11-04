using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.E2E;

internal sealed class DesktopFixture : IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "pcl-extension-e2e", Guid.NewGuid().ToString("N"));
    public FrontendRuntimePaths Paths { get; }
    public I18nService Text { get; private set; } = null!;
    public LauncherActionService Actions { get; private set; } = null!;
    public LauncherViewModel Launcher { get; private set; } = null!;
    public MainWindow Window { get; private set; } = null!;
    public DesktopShellController Shell { get; private set; } = null!;
    public FrontendExtensionSession Session { get; private set; } = null!;
    public PickerProvider Picker { get; } = PickerProvider.Create();

    public DesktopFixture()
    {
        Paths = new(Root, Root + "/temp", Root + "/data", Root + "/shared", Root + "/shared/config.json", Root + "/data/config.yml", Root + "/appdata", []);
    }

    public async Task Start(bool safeMode = false)
    {
        Text = new(new I18nSettingsManager(new FrontendConfigProviderAdapter(Paths)));
        Text.SetLocale("en-US");
        Actions = new(Paths, new FrontendPlatformAdapter(), () => { }, Text);
        Actions.AcceptLauncherEula();
        Actions.PersistSharedValue("SystemOnboardingCompleted", true);
        Actions.PersistLocalValue("SystemSystemUpdate", 2);
        Actions.PersistLocalValue("LaunchFolderSelect", Root + "/minecraft");
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, Paths);
        Launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), Actions, Text);
        Window = new MainWindow { DataContext = Launcher };
        SetLifetime(new ClassicDesktopStyleApplicationLifetime { MainWindow = Window, ShutdownMode = ShutdownMode.OnExplicitShutdown });
        typeof(TopLevel).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(global::Avalonia.Platform.Storage.IStorageProvider)).SetValue(Window, Picker.Service);
        Shell = new(Window, Text, Launcher.LaunchCommand, Window.Close, Actions.FlushPendingSettings);
        Session = new(Window, Shell, Launcher, Actions, Text, safeMode);
        Window.Show();
        await Session.StartAsync();
        await Settle();
        var startupContinue = Window.GetVisualDescendants().OfType<PclButton>().FirstOrDefault(button => button.IsEffectivelyVisible && button.Text == "Continue");
        if (startupContinue is not null) await Click(Window, startupContinue);
        await Wait(() => Launcher.IsTopLevelNavigationInteractive, "Startup prompt did not close");
    }

    public async Task OpenExtensions()
    {
        for (var i = 0; i < 4 && !Window.GetVisualDescendants().OfType<PclTabButton>().Any(tab => tab.IsEffectivelyVisible && tab.Text == "Tools"); i++)
            await Click(Window, Window.Find<PclIconButton>("BackButton")!);
        await Click(Window, Window.GetVisualDescendants().OfType<PclTabButton>().First(tab => tab.IsEffectivelyVisible && tab.Text == "Tools"));
        await Wait(() => Window.GetVisualDescendants().OfType<PclListItem>().Any(item => item.Title == "Extensions"), "Extensions sidebar missing: " + string.Join(",", Launcher.SidebarSections.SelectMany(section => section.Items).Select(item => item.Title)));
        await Click(Window, Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "Extensions"));
        await Wait(() => Window.GetVisualDescendants().OfType<ExtensionManagerView>().Any(), "Extensions tools page did not open");
        Assert.IsFalse(Window.Find<ContentControl>("ExtensionWorkspace")!.IsVisible);
    }

    public string CreateInstance(string name)
    {
        var directory = Path.Combine(Root, "minecraft", "versions", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            id = name, type = "release", mainClass = "net.minecraft.client.main.Main", minecraftArguments = "",
            assets = "1.20", libraries = Array.Empty<object>(), releaseTime = "2023-06-12T13:25:51Z"
        }));
        return directory;
    }

    public ExtensionCardView Card(string id) => Window.GetVisualDescendants().OfType<ExtensionCardView>().Single(card => card.Name == "Extension_" + id.Replace('.', '_').Replace('-', '_'));
    public async Task Expand(string id)
    {
        var card = Card(id).Find<PclCard>("Card")!;
        if (!card.IsChevronExpanded) await Click(Window, card.Find<Button>("HeaderButton")!);
        await Settle();
    }
    public Task Toggle(string id) => Click(Window, Card(id).Find<PclIconButton>("ToggleEnabledButton")!);
    public async Task Search(string text)
    {
        Window.KeyPressQwerty(PhysicalKey.P, (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control) | RawInputModifiers.Shift);
        Window.KeyReleaseQwerty(PhysicalKey.P, RawInputModifiers.None);
        await Wait(() => Session.OverlayContent is not null, "Search shortcut did not open search");
        await Type(Window, Window.Find<PclSearchBox>("SearchQuery")!.Find<TextBox>("SearchTextBox")!, text);
        await Wait(() => Window.Find<ListBox>("SearchResults") is { ItemCount: > 0, IsEnabled: true }, "Search returned no results");
    }

    public void Screenshot(string name)
    {
        var directory = Environment.GetEnvironmentVariable("PCL_E2E_ARTIFACTS") ?? Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");
        Directory.CreateDirectory(directory);
        using var frame = Window.CaptureRenderedFrame();
        Assert.IsNotNull(frame, "The full window must render using Skia.");
        var path = Path.Combine(directory, name + ".png");
        frame.Save(path);
        Assert.IsTrue(new FileInfo(path).Length > 10_000, "The rendered window is blank: " + name);
    }

    public async Task Restart(bool safeMode = false) { await Stop(); await Start(safeMode); }
    private async Task Stop()
    {
        await Session.DisposeAsync();
        Shell.Dispose(); Window.Close(); Actions.FlushPendingSettings(); Text.Dispose();
        SetLifetime(null);
        await Settle();
    }
    public async ValueTask DisposeAsync() { if (Session is not null) await Stop(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }

    private static void SetLifetime(IApplicationLifetime? lifetime) => typeof(Application)
        .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Single(field => field.FieldType == typeof(IApplicationLifetime)).SetValue(Application.Current, lifetime);

    public static async Task Click(TopLevel window, Control control)
    {
        control.BringIntoView(); await Settle();
        ClickNow(window, control);
        await Settle();
    }
    public static async Task Click(TopLevel window, Func<Control?> resolve)
    {
        await Wait(() => resolve() is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }, "The click target did not appear.");
        resolve()!.BringIntoView(); await Settle();
        // Virtualized catalogs can replace a row during layout. Resolve it again
        // after settling, before delivering any pointer input.
        await Wait(() =>
        {
            if (resolve() is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } target) return false;
            var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
            return point is { } position && window.InputHitTest(position) is Visual hit &&
                (ReferenceEquals(hit, target) || hit.GetVisualAncestors().Contains(target));
        }, "The click target is covered or detached.");
        ClickNow(window, resolve()!);
        await Settle();
    }
    private static void ClickNow(TopLevel window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
        Assert.IsNotNull(point, control.Name);
        Assert.IsTrue(control.IsEffectivelyVisible && control.IsEffectivelyEnabled, $"Cannot click {control.Name}");
        var hit = window.InputHitTest(point.Value) as Visual;
        Assert.IsTrue(ReferenceEquals(hit, control) || hit?.GetVisualAncestors().Contains(control) == true,
            $"{control.Name ?? control.GetType().Name} is covered by {hit?.GetType().Name}");
        window.MouseMove(point.Value);
        window.MouseDown(point.Value, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point.Value, MouseButton.Left, RawInputModifiers.None);
    }
    public static async Task Type(Window window, TextBox field, string text)
    {
        await Click(window, field);
        // Avalonia.Headless supplies the standard Control-based text-editing hotkeys on every OS.
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.None);
        window.KeyTextInput(text);
        await Settle();
    }
    public static async Task Wait(Func<bool> condition, string message)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(12)) { Tick(); await Task.Delay(25); }
        Assert.IsTrue(condition(), message);
        Tick();
    }
    public static async Task Settle() { Tick(); await Task.Delay(180); Tick(); }
    public static void Tick() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
    public static Task Run(Func<Task> test) => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(DesktopFixture).Assembly).Dispatch(async () => { await test(); return true; }, CancellationToken.None);
}

internal static class UiElements
{
    public static T? Find<T>(this Control root, string name) where T : Control => root.GetVisualDescendants().Prepend(root).OfType<T>().SingleOrDefault(control => control.Name == name);
}

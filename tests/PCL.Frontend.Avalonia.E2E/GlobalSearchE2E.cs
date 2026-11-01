using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class GlobalSearchE2E
{
    [TestMethod]
    public Task Palette_MouseKeyboard_Categories_Focus_Dismissal_AndNavigation() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        app.CreateInstance("Creative 1.20.1"); app.CreateInstance("Survival 1.21.4");
        await app.Start();
        var toolbarSearch = app.Window.GetVisualDescendants().OfType<PclIconButton>().Single(control => AutomationProperties.GetName(control) == "Global search");
        await Click(app.Window, toolbarSearch);
        await Wait(() => Results(app) is { ItemCount: > 0, IsEnabled: true }, "Palette suggestions did not load");
        Assert.IsTrue(Field(app).IsFocused);
        Assert.AreEqual(0, app.Session.Windows.Count(window => window.Title == "Global search"));
        app.Screenshot("search-light-suggestions");
        // Native editors and IME integrations must get the first chance to consume Enter/Escape.
        EventHandler<KeyEventArgs> consume = (_, e) => { if (e.Key is global::Avalonia.Input.Key.Enter or global::Avalonia.Input.Key.Escape) e.Handled = true; };
        Field(app).KeyDown += consume;
        Key(app, PhysicalKey.Enter); Key(app, PhysicalKey.Escape);
        Assert.IsNotNull(app.Session.OverlayContent, "A key handled by an editor must not execute or dismiss the palette.");
        Assert.IsTrue(Field(app).IsFocused);
        Field(app).KeyDown -= consume;
        var previous = Results(app).SelectedIndex;
        Key(app, PhysicalKey.ArrowDown);
        Assert.AreEqual(previous + 1, Results(app).SelectedIndex);
        Assert.IsTrue(Field(app).IsFocused, "Result navigation should keep the query ready for typing.");
        await Click(app.Window, app.Window.Find<ToggleButton>("Filter_instances")!);
        await Wait(() => Results(app) is { ItemCount: 2, IsEnabled: true }, "Instance category was not applied");
        Assert.IsTrue(Results(app).Items.Cast<ExtensionSearchResult>().All(result => result.Kind == ExtensionSearchKind.Instance));
        app.Screenshot("search-light-instances");
        await Type(app.Window, Field(app), "no such world 9f6e");
        Key(app, PhysicalKey.Enter);
        Assert.IsNotNull(app.Session.OverlayContent, "Pending results must not execute a stale selection.");
        await Wait(() => app.Window.Find<StackPanel>("EmptyState")!.IsVisible, "Empty state did not appear");
        Assert.AreEqual(-1, Results(app).SelectedIndex);
        app.Screenshot("search-light-empty");
        Key(app, PhysicalKey.Escape);
        Assert.IsNull(app.Session.OverlayContent);
        await app.Search("Extensions");
        var result = Results(app).Items.OfType<ExtensionSearchResult>().Single(item => item.Route == "Setup/SetupExtensions");
        var row = app.Window.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == result.Title);
        await Click(app.Window, row);
        await Wait(() => app.Session.OverlayContent is null, "Clicking a search result did not close the palette");
        Assert.IsTrue(app.Window.GetVisualDescendants().OfType<Desktop.Extensions.ExtensionManagerView>().Any());
        var focusTarget = app.Window.GetVisualDescendants().OfType<Desktop.Extensions.ExtensionManagerView>().Single().Find<PclButton>("InstallButton")!;
        focusTarget.GetVisualDescendants().OfType<Button>().First().Focus();
        var savedFocus = app.Window.FocusManager!.GetFocusedElement();
        await app.Search("Settings");
        app.Window.MouseDown(new Point(30, 440), MouseButton.Left, RawInputModifiers.None);
        app.Window.MouseUp(new Point(30, 440), MouseButton.Left, RawInputModifiers.None);
        await Settle();
        Assert.IsNull(app.Session.OverlayContent, "Clicking the backdrop should dismiss search.");
        if (savedFocus is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }) Assert.AreSame(savedFocus, app.Window.FocusManager.GetFocusedElement());
        await app.Search("Extensions");
        Key(app, PhysicalKey.Escape);
        Assert.AreSame(savedFocus, app.Window.FocusManager.GetFocusedElement(), "Escape should restore focus without the launcher clearing it afterward.");
        await app.Search("Extensions");
        Shortcut(app);
        Assert.IsNull(app.Session.OverlayContent, "The search shortcut should toggle the existing palette.");
        await app.Search("Extensions");
        await app.Session.Host.SetEnabledAsync("pcl.global-search", false);
        await Settle();
        Assert.IsNull(app.Session.OverlayContent, "Disabling an extension must dispose its overlay.");
        Assert.IsFalse(app.Window.GetVisualDescendants().Any(control => control is PclSearchBox));
    });

    [TestMethod]
    public Task Palette_DarkTheme_MinimumWindow_AllLocales_AndSharedControls() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        app.Window.Width = 810; app.Window.Height = 470;
        await app.Search("Settings");
        Assert.AreEqual(ThemeVariant.Dark, app.Window.ActualThemeVariant);
        await Settle();
        var surface = app.Window.Find<Border>("SearchSurface")!;
        AssertWithinWindow(app, surface);
        var surfaceColor = ((ISolidColorBrush)surface.Background!).Color;
        Assert.IsTrue(surfaceColor.R < 100 && surfaceColor.G < 100 && surfaceColor.B < 100, "The elevated surface must follow dark mode.");
        app.Screenshot("search-dark-small");
        foreach (var locale in new[] { "zh-Hans", "zh-Hant", "lzh", "de-DE", "fr-FR", "es-ES", "ko-KR", "en-US" })
        {
            app.Text.SetLocale(locale);
            await Settle();
            var paletteText = app.Session.Host.Get("pcl.global-search").Text;
            Assert.AreEqual(paletteText.Get("title"), app.Window.Find<TextBlock>("Heading")!.Text);
            Assert.AreEqual(paletteText.Get("watermark"), app.Window.Find<PclSearchBox>("SearchQuery")!.Watermark);
            AssertWithinWindow(app, surface);
            Assert.IsTrue(app.Window.Find<StackPanel>("Filters")!.GetVisualDescendants().OfType<ToggleButton>().All(button => button.Content is string text && text != "all"));
            if (locale == "zh-Hans") app.Screenshot("search-dark-zh");
            if (locale == "de-DE") app.Screenshot("search-dark-de");
        }
        await Type(app.Window, Field(app), "something nowhere 239f");
        await Wait(() => app.Window.Find<StackPanel>("EmptyState")!.IsVisible, "Empty state did not finish");
        AssertWithinWindow(app, app.Window.Find<StackPanel>("EmptyState")!);
        app.Screenshot("search-dark-empty");
        Key(app, PhysicalKey.Escape);
        app.Actions.PersistSharedValue("UiDarkMode", 0);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        app.Window.Width = 1000; app.Window.Height = 650;
        await app.Search("Settings");
        app.Screenshot("search-light-results");
    });

    private static void AssertWithinWindow(DesktopFixture app, Control control)
    {
        var origin = control.TranslatePoint(default, app.Window)!.Value;
        Assert.IsTrue(origin.X >= 16 && origin.Y >= 48 && origin.X + control.Bounds.Width <= app.Window.Bounds.Width - 16
            && origin.Y + control.Bounds.Height <= app.Window.Bounds.Height - 16, $"{control.Name}: {origin}, {control.Bounds.Size} outside {app.Window.Bounds}");
    }
    private static TextBox Field(DesktopFixture app) => app.Window.Find<PclSearchBox>("SearchQuery")!.Find<TextBox>("SearchTextBox")!;
    private static ListBox Results(DesktopFixture app) => app.Window.Find<ListBox>("SearchResults")!;
    private static void Key(DesktopFixture app, PhysicalKey key) { app.Window.KeyPressQwerty(key, RawInputModifiers.None); app.Window.KeyReleaseQwerty(key, RawInputModifiers.None); Tick(); }
    private static void Shortcut(DesktopFixture app)
    {
        app.Window.KeyPressQwerty(PhysicalKey.P, (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control) | RawInputModifiers.Shift);
        app.Window.KeyReleaseQwerty(PhysicalKey.P, RawInputModifiers.None); Tick();
    }
}

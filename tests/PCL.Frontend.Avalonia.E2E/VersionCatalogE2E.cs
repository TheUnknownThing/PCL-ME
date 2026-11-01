using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class VersionCatalogE2E
{
    [TestMethod]
    public Task Catalog_Search_Clear_Select_AndCollapseWithoutRevealingHiddenRows() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        SeedCatalog(app.Launcher);
        await Click(app.Window, app.Window.GetVisualDescendants().OfType<PclTabButton>()
            .Single(tab => tab.IsEffectivelyVisible && tab.Text == "Download"));
        await Wait(() => app.Window.GetVisualDescendants().OfType<DownloadInstallRightPaneView>().Any(), "Version catalog did not open.");
        var view = app.Window.GetVisualDescendants().OfType<DownloadInstallRightPaneView>().Single();
        var groups = view.Find<ItemsControl>("MinecraftCatalogGroupListHost")!;
        var full = view.Find<PclCard>("MinecraftCatalogExpandedHost")!;
        var inner = view.Find<ScrollViewer>("MinecraftCatalogExpandedScroll")!;
        var outer = view.Find<ScrollViewer>("InstallScrollViewer")!;
        var list = view.Find<ItemsControl>("MinecraftCatalogExpandedList")!;
        var field = view.GetVisualDescendants().OfType<PclSearchBox>().Single().Find<TextBox>("SearchTextBox")!;
        await Wait(() => groups.IsVisible && groups.GetVisualDescendants().OfType<PclCard>().Count() >= 3, "Version groups did not load.");
        await Settle();

        // Check rendered exit frames at both ends of a virtualized version list.
        foreach (var atEnd in new[] { false, true })
        {
            var release = groups.GetVisualDescendants().OfType<PclCard>()
                .Single(card => card.DataContext is DownloadInstallMinecraftSectionViewModel { CanCollapse: true } section
                    && section.Choices[0].Title.StartsWith("1.21.", StringComparison.Ordinal));
            await Click(app.Window, release.Find<Button>("HeaderButton")!);
            await Wait(() => full.IsVisible && !groups.IsVisible, "Version group did not expand.");
            await Settle();
            if (atEnd) inner.ScrollToEnd(); else inner.ScrollToHome();
            await Settle();
            var viewport = inner.Viewport;
            var offset = inner.Offset;
            var height = full.Bounds.Height;
            Assert.IsTrue(inner.Extent.Height > viewport.Height * 4);
            app.Screenshot(atEnd ? "versions-expanded-bottom" : "versions-expanded-top");
            ClickNow(full.Find<Button>("HeaderButton")!);
            var frames = 0;
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (full.IsVisible && DateTime.UtcNow < deadline)
            {
                Assert.AreEqual(ScrollBarVisibility.Disabled, outer.VerticalScrollBarVisibility);
                Assert.AreEqual(height, full.Bounds.Height, .01);
                Assert.AreEqual(viewport, inner.Viewport, "Hidden rows must not be exposed by an expanding exit viewport.");
                Assert.AreEqual(offset, inner.Offset, "The last visible rows must stay in place during collapse.");
                if (frames++ == 2) app.Screenshot(atEnd ? "versions-collapse-bottom" : "versions-collapse-top");
                using var frame = app.Window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                await Task.Delay(16); Tick();
            }
            Assert.IsTrue(frames > 0);
            await Wait(() => !full.IsVisible && groups.IsVisible, "Version group did not collapse.");
            Assert.AreEqual(ScrollBarVisibility.Auto, outer.VerticalScrollBarVisibility);
            await Settle();
        }

        await Type(app.Window, field, "1.21.");
        await Wait(() => full.IsVisible && !full.ShowChevron && list.ItemCount == 120, "Search results must have a static header.");
        await Settle();
        Assert.IsNull(full.HeaderCommand);
        Assert.IsTrue(full.IsContentVisible);
        Assert.IsTrue(full.Bounds.Height < app.Window.Bounds.Height);
        Assert.IsTrue(list.GetRealizedContainers().Count() < 50, "Large search results must remain virtualized.");
        ClickNow(full.Find<Button>("HeaderButton")!);
        await Settle();
        Assert.IsTrue(full.IsVisible && full.IsContentVisible && !full.ShowChevron);
        app.Screenshot("versions-search-results");
        await Type(app.Window, field, "NO-SUCH-VERSION");
        await Wait(() => view.Find<PclCard>("MinecraftCatalogEmptyStateCard")!.IsVisible && !full.IsVisible, "Empty search state did not appear.");
        app.Screenshot("versions-search-empty");
        await Click(app.Window, field);
        app.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        app.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.None);
        app.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        app.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        await Wait(() => groups.IsVisible && app.Launcher.DownloadInstallMinecraftSearchQuery.Length == 0, "Clearing search did not restore version groups.");
        await Type(app.Window, field, "1.21.119");
        await Wait(() => full.IsVisible && list.ItemCount == 1 && !full.ShowChevron, "Exact version search did not update.");
        await Settle();
        app.Actions.PersistSharedValue("UiDarkMode", 1);
        FrontendAppearanceService.ApplyStoredAppearance(Application.Current!, app.Paths);
        await Settle();
        Assert.AreEqual(ThemeVariant.Dark, app.Window.ActualThemeVariant);
        app.Screenshot("versions-search-dark");
        await Click(app.Window, list.GetVisualDescendants().OfType<PclListItem>().Single(item => item.Title == "1.21.119"));
        await Wait(() => app.Launcher.ShowDownloadInstallSelectionStage, "Selecting a search result did not open installation options.");
        Assert.AreEqual("1.21.119", app.Launcher.DownloadInstallMinecraftVersion);

        void ClickNow(Control control)
        {
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), app.Window)!.Value;
            app.Window.MouseMove(point);
            app.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            app.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Tick();
        }
    });

    private static void SeedCatalog(LauncherViewModel launcher)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var version = typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogVersion", flags)!;
        version.SetValue(launcher, (int)version.GetValue(launcher)! + 1);
        Set("_downloadInstallMinecraftCatalogLoaded", true);
        Set("_isDownloadInstallMinecraftCatalogLoading", false);
        Set("_downloadInstallMinecraftCatalogChoices", Enumerable.Range(0, 120).Select(index => Choice($"1.21.{index}", "release"))
            .Concat([Choice("25w44a", "preview"), Choice("b1.8.1", "legacy")]).ToArray());

        void Set(string name, object value) => typeof(LauncherViewModel).GetField(name, flags)!.SetValue(launcher, value);
        static FrontendInstallChoice Choice(string id, string group) => new(id, id, id, id, FrontendInstallChoiceKind.Minecraft,
            Metadata: new JsonObject { ["catalogGroup"] = group });
    }
}

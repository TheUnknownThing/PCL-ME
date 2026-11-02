using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Panes.Left;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.UI.Animation;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

public sealed partial class ExtensionCapabilitiesE2E
{
    [TestMethod]
    [DataRow(ExtensionPageArea.Download, "Download/DownloadCompFavorites")]
    [DataRow(ExtensionPageArea.Settings, "Setup/SetupAbout")]
    [DataRow(ExtensionPageArea.Tools, "Tools/ToolsTest")]
    public Task PageNavigation_UsesNativeAnimatedPresenter_AndPreservesSidebarControls(ExtensionPageArea area, string nativeRoute) => Run(async () =>
    {
        await using var app = new DesktopFixture(); await app.Start();
        var pages = new List<NavigationTestPage>();
        var context = await Activate(app, context =>
        {
            var contributions = context.GetService<IExtensionContributions>();
            contributions.RegisterPage(new() { Id = "test.capabilities.first", Title = "Integrated first", Area = area }, surface => Create(surface, "FirstEditor"));
            contributions.RegisterPage(new() { Id = "test.capabilities.second", Title = "Integrated second", Area = area }, surface => Create(surface, "SecondEditor"));
            return ValueTask.CompletedTask;
        });
        await context.Launcher.NavigateAsync(nativeRoute); await Settle();
        var nativePane = app.Launcher.CurrentStandardRightPane;
        var host = app.Window.GetVisualDescendants().OfType<PclAnimatedRightPaneHost>().Single();
        Assert.AreSame(nativePane, host.PaneContent);
        var sidebar = app.Window.GetVisualDescendants().OfType<StandardNavigationListPaneView>().Single();
        var originalRows = sidebar.RenderRows.ToArray();
        var originalControls = SidebarControls(app);
        AssertSidebarAnimationSequence(app);
        var collectionChanges = 0;
        app.Launcher.ExtensionNavigationEntries.CollectionChanged += (_, _) => collectionChanges++;
        var transitions = new List<NavigationTransitionEventArgs>();
        app.Launcher.NavigationTransitionRequested += (_, transition) => transitions.Add(transition);
        await Click(app.Window, originalControls.Single(item => item.Title == "Integrated first"));
        AssertExtensionPresenter(app, pages.Single());
        Assert.AreEqual(1, transitions.Count);
        Assert.IsTrue(transitions.Single().AnimateRightPane);
        Assert.IsFalse(transitions.Single().AnimateLeftPane);
        CollectionAssert.AreEqual(originalRows, sidebar.RenderRows.ToArray(), "Selecting a page must retain sidebar rows.");
        CollectionAssert.AreEqual(originalControls, SidebarControls(app), "Selecting a page must retain realized sidebar controls.");
        Assert.AreEqual(0, collectionChanges);
        var first = pages.Single();
        await Type(app.Window, first.Editor, "Preserve this edit");
        first.Editor.CaretIndex = 5;
        var transientNativeSelections = 0;
        foreach (var item in app.Launcher.SidebarSections.SelectMany(section => section.Items))
            item.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(item.IsSelected) && item.IsSelected) transientNativeSelections++; };
        app.Launcher.RefreshExtensionInstances(); await Settle();
        Assert.AreEqual(0, transientNativeSelections, "Background refresh must not briefly select the hidden native page.");
        var presenter = host.Children.Single();
        await Click(app.Window, originalControls.Single(item => item.Title == "Integrated first"));
        await context.GetService<IExtensionContributions>().ShowPageAsync("test.capabilities.first"); await Settle();
        Assert.AreEqual(1, pages.Count, "Repeated selection must not call the page factory again.");
        Assert.AreSame(presenter, host.Children.Single(), "Repeated selection must not restart the page entrance.");
        Assert.AreEqual(1, transitions.Count);
        Assert.AreEqual("Preserve this edit", first.Editor.Text);
        Assert.AreEqual(5, first.Editor.CaretIndex);
        Assert.IsFalse(first.Disposed);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await context.GetService<IExtensionContributions>().ShowPageAsync("test.capabilities.second", cancellation.Token));
        }
        Assert.AreEqual(1, pages.Count, "Cancelled navigation must not replace the active page.");
        await context.GetService<IExtensionContributions>().ShowPageAsync("test.capabilities.second"); await Settle();
        AssertExtensionPresenter(app, pages.Last());
        Assert.IsTrue(first.Disposed && first.Surface.Lifetime.IsCancellationRequested);
        Assert.AreEqual(1, first.DisposeCount);
        CollectionAssert.AreEqual(originalControls, SidebarControls(app));
        Assert.AreEqual(0, collectionChanges);
        Assert.IsTrue(app.Launcher.ExtensionNavigationEntries.Single(entry => entry.Id == "test.capabilities.second").Item.IsSelected);
        Assert.IsFalse(app.Launcher.SidebarSections.SelectMany(section => section.Items).Any(item => item.IsSelected));
        await context.Launcher.NavigateAsync(nativeRoute); await Settle();
        Assert.IsFalse(app.Launcher.HasExtensionPage);
        Assert.AreNotEqual(StandardRightPaneKind.Extension, app.Launcher.CurrentStandardRightPaneDescriptor!.Kind);
        Assert.AreSame(app.Launcher.CurrentStandardRightPane, host.PaneContent);
        Assert.AreEqual(1, host.Children.Count);
        Assert.IsTrue(pages.Last().Disposed);
        Assert.IsFalse(app.Launcher.ExtensionNavigationEntries.Any(entry => entry.Item.IsSelected));
        Assert.IsTrue(app.Launcher.SidebarSections.SelectMany(section => section.Items).Any(item => item.IsSelected));
        CollectionAssert.AreEqual(originalControls, SidebarControls(app));
        await context.GetService<IExtensionContributions>().ShowPageAsync("test.capabilities.first"); await Settle();
        var nativeItem = app.Launcher.SidebarSections.SelectMany(section => section.Items).First(item => item.IsSelected == false);
        await Click(app.Window, originalControls.Single(control => control.Title == nativeItem.Title));
        Assert.IsFalse(app.Launcher.HasExtensionPage, "A native sidebar click must leave the extension page.");
        Assert.IsTrue(pages.Last().Disposed);
        CollectionAssert.AreEqual(originalControls, SidebarControls(app));
        Assert.IsTrue(transitions.All(transition => !transition.AnimateLeftPane && transition.AnimateRightPane));
        app.Screenshot("extension-integrated-navigation-" + area);

        NavigationTestPage Create(ExtensionSurfaceContext surface, string name) { var page = new NavigationTestPage(surface, name); pages.Add(page); return page; }
    });

    [TestMethod]
    public Task PageRegistrationChanges_FailedFactories_AndDisablePreserveUnrelatedNavigation() => Run(async () =>
    {
        await using var app = new DesktopFixture(); await app.Start();
        NavigationTestPage? first = null;
        var context = await Activate(app, context =>
        {
            var ui = context.GetService<IExtensionContributions>();
            ui.RegisterPage(new() { Id = "test.capabilities.first", Title = "Stable page", Area = ExtensionPageArea.Tools }, surface => first = new(surface, "StableEditor"));
            ui.RegisterPage(new() { Id = "test.capabilities.broken", Title = "Broken page", Area = ExtensionPageArea.Tools }, _ => throw new InvalidOperationException("Expected factory failure"));
            return ValueTask.CompletedTask;
        });
        var ui = context.GetService<IExtensionContributions>();
        await ui.ShowPageAsync("test.capabilities.first"); await Settle();
        var originalControls = SidebarControls(app);
        var sidebar = app.Window.GetVisualDescendants().OfType<StandardNavigationListPaneView>().Single();
        var originalRows = sidebar.RenderRows.ToArray();
        var builtinControls = originalControls.Where(item => item.Title is not ("Stable page" or "Broken page")).ToArray();
        var extra = ui.RegisterPage(new() { Id = "test.capabilities.extra", Title = "Extra page", Area = ExtensionPageArea.Tools }, surface => new NavigationTestPage(surface, "ExtraEditor"));
        await Settle();
        foreach (var control in originalControls) Assert.IsTrue(SidebarControls(app).Contains(control));
        foreach (var row in originalRows) Assert.IsTrue(sidebar.RenderRows.Contains(row));
        AssertSidebarAnimationSequence(app);
        await Type(app.Window, first!.Editor, "Keep the active view");
        extra.Dispose(); await Settle();
        CollectionAssert.AreEqual(originalControls, SidebarControls(app));
        AssertSidebarAnimationSequence(app);
        await ui.ShowPageAsync("test.capabilities.broken"); await Settle();
        AssertExtensionPresenter(app, first);
        Assert.IsFalse(first.Disposed, "A failed factory must retain the last working page.");
        Assert.AreEqual("Keep the active view", first.Editor.Text);
        Assert.IsTrue(app.Launcher.ExtensionNavigationEntries.Single(entry => entry.Id == "test.capabilities.first").Item.IsSelected);
        await app.Session.Host.SetEnabledAsync(context.Manifest.Id, false); await Settle();
        Assert.IsTrue(first.Disposed && first.Surface.Lifetime.IsCancellationRequested);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.IsFalse(app.Launcher.HasExtensionPage);
        Assert.AreNotEqual(StandardRightPaneKind.Extension, app.Launcher.CurrentStandardRightPaneDescriptor!.Kind);
        Assert.AreSame(sidebar, app.Window.GetVisualDescendants().OfType<StandardNavigationListPaneView>().Single());
        foreach (var control in builtinControls)
            Assert.IsTrue(SidebarControls(app).Contains(control), "Disabling an extension recreated a built-in sidebar control: " + control.Title);
    });

    [TestMethod]
    public Task GlobalSearch_Back_Home_Locale_AndMissingHistoryTargetsUseTheSharedPageHost() => Run(async () =>
    {
        await using var app = new DesktopFixture(); var instance = CreateInstance(app, "navigation", "1.20.1"); await app.Start();
        var mounts = new List<NavigationTestPage>();
        IDisposable? settingsRegistration = null;
        var context = await Activate(app, context =>
        {
            var ui = context.GetService<IExtensionContributions>();
            settingsRegistration = ui.RegisterPage(new() { Id = "test.capabilities.settings", Title = "Integrated settings", Area = ExtensionPageArea.Settings }, surface => Create(surface, "HistoryEditor"));
            ui.RegisterPage(new() { Id = "test.capabilities.download", Title = "Integrated download", Area = ExtensionPageArea.Download }, surface => Create(surface, "DownloadEditor"));
            return ValueTask.CompletedTask;
        });
        await app.Search("Integrated settings");
        app.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); app.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Wait(() => mounts.Count == 1 && mounts.Last().IsEffectivelyVisible, "Search did not navigate to the contextual page.");
        AssertExtensionPresenter(app, mounts.Single());
        var original = mounts.Single();
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = instance }); await Settle();
        Assert.IsTrue(original.Disposed);
        Assert.IsFalse(app.Launcher.HasExtensionPage);
        var transitions = new List<NavigationTransitionEventArgs>();
        app.Launcher.NavigationTransitionRequested += (_, transition) => transitions.Add(transition);
        await Click(app.Window, app.Window.Find<PclIconButton>("BackButton")!);
        AssertExtensionPresenter(app, mounts.Last());
        Assert.AreEqual("HistoryEditor", mounts.Last().Editor.Name);
        Assert.AreEqual(1, transitions.Count, "Back must commit one destination, with no intermediate page transition.");
        Assert.AreEqual(NavigationTransitionDirection.Backward, transitions.Single().Direction);
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = instance }); await Settle();
        app.Launcher.HomeCommand.Execute(null); await Settle();
        AssertExtensionPresenter(app, mounts.Last());
        Assert.AreEqual("HistoryEditor", mounts.Last().Editor.Name);
        var beforeLocale = mounts.Last();
        app.Text.SetLocale("zh-Hans"); await Settle();
        Assert.IsTrue(beforeLocale.Disposed && beforeLocale.Surface.Lifetime.IsCancellationRequested);
        AssertExtensionPresenter(app, mounts.Last());
        Assert.AreEqual(1, beforeLocale.DisposeCount);
        app.Text.SetLocale("en-US"); await Settle();
        await context.Launcher.NavigateAsync("instance", new Dictionary<string, string> { ["id"] = instance }); await Settle();
        settingsRegistration!.Dispose();
        app.Launcher.BackCommand.Execute(null); await Settle();
        Assert.IsFalse(app.Launcher.HasExtensionPage, "A removed history target must fall back to the native page.");
        Assert.AreNotEqual(StandardRightPaneKind.Extension, app.Launcher.CurrentStandardRightPaneDescriptor!.Kind);
        var ui = context.GetService<IExtensionContributions>();
        var buildsBefore = mounts.Count;
        await ui.ShowPageAsync("test.capabilities.download");
        await context.Launcher.NavigateAsync("Tools/ToolsTest");
        await ui.ShowPageAsync("test.capabilities.download"); await Settle();
        Assert.AreEqual(buildsBefore + 2, mounts.Count);
        Assert.IsTrue(mounts[^2].Disposed);
        AssertExtensionPresenter(app, mounts.Last());
        Assert.AreEqual(ExtensionPageArea.Download, app.Launcher.ExtensionPageArea);
        Assert.IsTrue(app.Launcher.ExtensionNavigationEntries.Single().Item.IsSelected);
        app.Launcher.BackCommand.Execute(null); await Settle();
        // Top-level extension pages, like built-in pages, do not invent a child Back target.
        AssertExtensionPresenter(app, mounts.Last());

        NavigationTestPage Create(ExtensionSurfaceContext surface, string name) { var page = new NavigationTestPage(surface, name); mounts.Add(page); return page; }
    });

    private static PclListItem[] SidebarControls(DesktopFixture app) => app.Window.GetVisualDescendants().OfType<StandardNavigationListPaneView>()
        .Single().GetVisualDescendants().OfType<PclListItem>().ToArray();

    private static void AssertSidebarAnimationSequence(DesktopFixture app)
    {
        var sidebar = app.Window.GetVisualDescendants().OfType<StandardNavigationListPaneView>().Single();
        var native = SidebarControls(app).First();
        foreach (var (row, index) in sidebar.RenderRows.Select((row, index) => (row, index)))
        {
            Assert.AreEqual(index * SidebarListItemViewModel.EnterStaggerMilliseconds, row.EnterDelay,
                "All native and extension rows must share one entrance sequence.");
            if (row is not SidebarItemRenderRow item) continue;
            var control = SidebarControls(app).Single(control => control.DataContext is SidebarItemRenderRow rendered && ReferenceEquals(rendered.Item, item.Item));
            Assert.AreEqual(row.EnterDelay, Motion.GetDelay(control));
            Assert.IsTrue(Motion.GetAnimateOnVisible(control));
            Assert.AreEqual(Motion.GetOffsetX(native), Motion.GetOffsetX(control));
            Assert.AreEqual(Motion.GetOffsetY(native), Motion.GetOffsetY(control));
            Assert.AreEqual(Motion.GetOvershootTranslation(native), Motion.GetOvershootTranslation(control));
            Assert.AreEqual(Motion.GetInitialOpacity(native), Motion.GetInitialOpacity(control));
        }
    }

    private static void AssertExtensionPresenter(DesktopFixture app, NavigationTestPage page)
    {
        var host = app.Window.GetVisualDescendants().OfType<PclAnimatedRightPaneHost>().Single();
        var pane = app.Launcher.CurrentStandardRightPane as ExtensionRightPaneViewModel;
        Assert.IsNotNull(pane);
        Assert.AreSame(page, pane.Content);
        Assert.AreSame(pane, host.PaneContent);
        Assert.AreEqual(StandardRightPaneKind.Extension, app.Launcher.CurrentStandardRightPaneDescriptor!.Kind);
        Assert.AreEqual(1, host.Children.Count, "Only one page presenter may be attached.");
        Assert.IsTrue(page.GetVisualAncestors().Contains(host), "Extension pages must use the built-in animated presenter.");
        Assert.IsTrue(page.IsEffectivelyVisible);
    }

    private sealed class NavigationTestPage : ContentControl, IDisposable
    {
        public ExtensionSurfaceContext Surface { get; }
        public TextBox Editor { get; }
        public bool Disposed => DisposeCount > 0;
        public int DisposeCount { get; private set; }
        public NavigationTestPage(ExtensionSurfaceContext surface, string name)
        {
            Surface = surface; Editor = new TextBox { Name = name, Margin = new(20), Watermark = "Edit this integrated page" };
            Content = new StackPanel { Children = { new TextBlock { Text = "Integrated extension page", Margin = new(20), FontSize = 24 }, Editor } };
        }
        public void Dispose() => DisposeCount++;
    }
}

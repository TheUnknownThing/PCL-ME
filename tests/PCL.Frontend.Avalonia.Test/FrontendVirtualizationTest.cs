using Avalonia;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.Desktop.Panes.Right.Sections;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendVirtualizationTest
{
    [TestMethod]
    public async Task MinecraftCatalog_RealizesOnlyViewportVersions()
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var fixture = new PageFixture();
            // Keep a pending catalog fetch from replacing the synthetic data.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var version = typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogVersion", flags)!;
            version.SetValue(fixture.Launcher, (int)version.GetValue(fixture.Launcher)! + 1);
            typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogLoaded", flags)!.SetValue(fixture.Launcher, true);
            typeof(LauncherViewModel).GetField("_isDownloadInstallMinecraftCatalogLoading", flags)!.SetValue(fixture.Launcher, false);
            var command = new ActionCommand(() => { });
            var choices = Enumerable.Range(0, 1000).Select(index => new DownloadInstallMinecraftChoiceViewModel(
                $"Version {index}", "Release", null, command)).ToArray();
            fixture.Launcher.DownloadInstallMinecraftSections.Add(new DownloadInstallMinecraftSectionViewModel(
                "Versions", choices, true, true, command));
            var view = new DownloadInstallRightPaneView { DataContext = fixture.Launcher };
            var window = new Window { Width = 700, Height = 400, Content = view };
            window.Show();
            await FrontendMotionTest.Advance(400);
            AssertViewportRows<PclListItem>(view);
            view.FindControl<ScrollViewer>("MinecraftCatalogExpandedScroll")!.ScrollToEnd();
            Tick();
            Assert.IsTrue(view.GetVisualDescendants().OfType<PclListItem>()
                .Any(row => ReferenceEquals(row.DataContext, choices[^1])));
            AssertViewportRows<PclListItem>(view);
            window.Close();
        });
    }

    [TestMethod]
    public async Task InstancePage_RealizesOnlyViewportRowsAndCanReachTheLastEntry()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new PageFixture();
            var command = new ActionCommand(() => { });
            var entries = Enumerable.Range(0, 1000).Select(index => new InstanceSelectEntryViewModel(
                $"Instance {index}", "1.21", "", [], false, false, null,
                "Select", "Favorite", "Open", "Delete", "Settings", command, command, command, command, command)).ToArray();
            fixture.Launcher.InstanceSelectionEntries.Add(entries[0]);
            fixture.Launcher.InstanceSelectionGroups.Add(new InstanceSelectionGroupViewModel("Group", "Group", entries, true));
            var view = new InstanceSelectRightPaneView { DataContext = fixture.Launcher };
            var window = new Window { Width = 700, Height = 400, Content = view };
            window.Show();
            Tick();
            AssertViewportRows<InstanceSelectEntryCardView>(view);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroll.ScrollToEnd();
            Tick();
            Assert.IsTrue(view.GetVisualDescendants().OfType<InstanceSelectEntryCardView>()
                .Any(row => ReferenceEquals(row.DataContext, entries[^1])), "The last instance must be reachable.");
            AssertViewportRows<InstanceSelectEntryCardView>(view);
            window.Close();
        });
    }

    [TestMethod]
    public async Task FavoritesPage_RecyclesRowsWithoutLosingSelectionOrCommands()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new PageFixture();
            var clicks = 0;
            var command = new ActionCommand(() => clicks++);
            var entries = Enumerable.Range(0, 1000).Select(index => new InstanceResourceEntryViewModel(
                null, $"Favorite {index}", "Version", "", index.ToString(), command,
                showSelection: true, isSelected: index == 0)).ToArray();
            fixture.Launcher.DownloadFavoriteSections.Add(new DownloadFavoriteSectionViewModel("Favorites", entries));
            var view = new DownloadFavoritesRightPaneView { DataContext = fixture.Launcher };
            var window = new Window { Width = 800, Height = 450, Content = view };
            window.Show();
            Tick();
            AssertViewportRows<ResourceEntryCardView>(view);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroll.ScrollToEnd();
            Tick();
            AssertViewportRows<ResourceEntryCardView>(view);
            var last = view.GetVisualDescendants().OfType<ResourceEntryCardView>()
                .Single(row => ReferenceEquals(row.DataContext, entries[^1]));
            ((InstanceResourceEntryViewModel)last.DataContext!).ActionCommand.Execute(null);
            Assert.AreEqual(1, clicks);
            entries[^1].IsSelected = true;
            scroll.ScrollToHome();
            Tick();
            Assert.IsTrue(entries[0].IsSelected);
            Assert.IsTrue(entries[^1].IsSelected);
            Assert.IsTrue(view.GetVisualDescendants().OfType<ResourceEntryCardView>()
                .Any(row => ReferenceEquals(row.DataContext, entries[0])));
            window.Close();
        });
    }

    [TestMethod]
    public async Task TaskPage_RealizesOnlyViewportCards()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new PageFixture();
            var command = new ActionCommand(() => { });
            for (var index = 0; index < 1000; index++)
                fixture.Launcher.TaskManagerEntries.Add(new TaskManagerEntryViewModel(fixture.Launcher, command, command));
            var view = new TaskManagerRightPaneView { DataContext = fixture.Launcher };
            var window = new Window { Width = 700, Height = 400, Content = view };
            window.Show();
            Tick();
            AssertViewportRows<TaskManagerTaskCardView>(view);
            view.GetVisualDescendants().OfType<ScrollViewer>().First().ScrollToEnd();
            Tick();
            Assert.IsTrue(view.GetVisualDescendants().OfType<TaskManagerTaskCardView>()
                .Any(row => ReferenceEquals(row.DataContext, fixture.Launcher.TaskManagerEntries[^1])));
            AssertViewportRows<TaskManagerTaskCardView>(view);
            window.Close();
        });
    }

    [TestMethod]
    public async Task GroupedItems_VirtualizeWithinAnOuterScrollViewerAndCard()
    {
        await RunOnUiThread(() =>
        {
            var builds = 0;
            var items = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).ToArray(),
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { CacheLength = .5 }),
                ItemTemplate = new FuncDataTemplate<int>((value, _) =>
                {
                    builds++;
                    return new TextBox { Text = value.ToString(), Height = 48 };
                })
            };
            var card = new PclCard { Header = "Group", ShowChevron = true, CardContent = items };
            var scroll = new ScrollViewer { Content = new StackPanel { Children = { card } } };
            var window = new Window { Width = 500, Height = 300, Content = scroll };
            window.Show();
            Tick();
            Assert.IsTrue(builds is > 0 and < 30, $"Built {builds} rows for the initial viewport.");
            var panel = items.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single();
            Assert.AreEqual(0, panel.FirstRealizedIndex);

            scroll.Offset = new Vector(0, 24000);
            Tick();
            Assert.IsTrue(panel.FirstRealizedIndex > 400, $"First realized: {panel.FirstRealizedIndex}");
            Assert.IsTrue(panel.LastRealizedIndex - panel.FirstRealizedIndex < 30);
            Assert.IsTrue(builds < 60, $"Built {builds} rows after jumping down the list.");
            scroll.ScrollToHome();
            Tick();
            Assert.AreEqual(0, panel.FirstRealizedIndex);
            window.Close();
        });
    }

    private static void AssertViewportRows<T>(Control view) where T : Control
    {
        var count = view.GetVisualDescendants().OfType<T>().Count();
        Assert.IsTrue(count is > 0 and < 50, $"Realized {count} {typeof(T).Name} controls for 1000 items.");
    }

    private sealed class PageFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-virtualization-test", Guid.NewGuid().ToString("N"));
        private readonly I18nService _i18n;
        private readonly LauncherActionService _actions;

        public PageFixture()
        {
            var paths = new FrontendRuntimePaths(_root, _root + "/temp", _root + "/data", _root + "/shared",
                _root + "/shared/config.json", _root + "/data/config.yml", _root + "/appdata", []);
            _i18n = new I18nService(new I18nSettingsManager(new FrontendConfigProviderAdapter(paths)));
            _actions = new LauncherActionService(paths, new FrontendPlatformAdapter(), () => { }, _i18n);
            Launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), _actions, _i18n);
        }

        public LauncherViewModel Launcher { get; }

        public void Dispose()
        {
            _actions.FlushPendingSettings();
            _i18n.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}

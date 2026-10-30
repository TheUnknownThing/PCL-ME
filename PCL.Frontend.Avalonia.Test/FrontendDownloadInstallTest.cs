using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendDownloadInstallTest
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Catalog_RapidHeaderClicksKeepTheLatestViewVisible(bool finishExpanded)
    {
        await FrontendMotionTest.RunAsync(async () =>
        {
            using var fixture = new CatalogFixture();
            await Advance(400);
            var groups = fixture.View.FindControl<ItemsControl>("MinecraftCatalogGroupListHost")!;
            var expanded = fixture.View.FindControl<PclCard>("MinecraftCatalogExpandedHost")!;
            var groupCard = groups.GetVisualDescendants().OfType<PclCard>()
                .Single(card => ReferenceEquals(card.DataContext, fixture.Section));
            ClickHeader(groupCard);
            // CI may defer the transition continuation beyond its nominal duration.
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!expanded.IsVisible && DateTime.UtcNow < deadline) await Advance(16);
            Assert.IsTrue(expanded.IsVisible);

            // Reverse the collapse while its exit fade is still running.
            ClickHeader(expanded);
            await Advance(40);
            ClickHeader(expanded);
            if (!finishExpanded)
            {
                await Advance(40);
                ClickHeader(expanded);
            }

            await Advance(600);
            Assert.AreEqual(finishExpanded, fixture.Section.IsExpanded);
            Assert.AreEqual(finishExpanded, expanded.IsVisible);
            Assert.AreEqual(!finishExpanded, groups.IsVisible);
            if (finishExpanded)
            {
                Assert.AreEqual(1d, expanded.Opacity, .01, "Reversing an exit must restore opacity as well as visibility.");
            }
            else
            {
                Assert.IsTrue(groups.GetVisualDescendants().OfType<PclCard>()
                    .All(card => Math.Abs(card.Opacity - 1) < .01));
            }
        });
    }

    private static void ClickHeader(PclCard card)
    {
        card.FindControl<Button>("HeaderButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Tick();
    }

    private static async Task Advance(int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        do
        {
            Tick();
            await Task.Delay(16);
        } while (DateTime.UtcNow < deadline);
        Tick();
    }

    private sealed class CatalogFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-download-install-test", Guid.NewGuid().ToString("N"));
        private readonly I18nService _i18n;
        private readonly LauncherActionService _actions;
        private readonly Window _window;
        private readonly double _animationSpeed = MotionDurations.Source.DebugAnimationSpeed;

        public DownloadInstallRightPaneView View { get; }

        public DownloadInstallMinecraftSectionViewModel Section { get; }

        public CatalogFixture()
        {
            var paths = new FrontendRuntimePaths(_root, _root + "/temp", _root + "/data", _root + "/shared",
                _root + "/shared/config.json", _root + "/data/config.yml", _root + "/appdata", []);
            _i18n = new I18nService(new I18nSettingsManager(new FrontendConfigProviderAdapter(paths)));
            _actions = new LauncherActionService(paths, new FrontendPlatformAdapter(), () => { }, _i18n);
            var launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), _actions, _i18n);
            // Keep an in-flight catalog fetch from replacing the synthetic versions.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var version = typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogVersion", flags)!;
            version.SetValue(launcher, (int)version.GetValue(launcher)! + 1);
            typeof(LauncherViewModel).GetField("_downloadInstallMinecraftCatalogLoaded", flags)!.SetValue(launcher, true);
            typeof(LauncherViewModel).GetField("_isDownloadInstallMinecraftCatalogLoading", flags)!.SetValue(launcher, false);
            launcher.DownloadInstallMinecraftSections.Clear();
            var choices = Enumerable.Range(0, 6).Select(index => new DownloadInstallMinecraftChoiceViewModel(
                $"Version {index}", "Release", null, new ActionCommand(() => { }))).ToArray();
            launcher.DownloadInstallMinecraftSections.Add(new DownloadInstallMinecraftSectionViewModel(
                "Latest", choices[..1], true, false, new ActionCommand(() => { })));
            Section = new DownloadInstallMinecraftSectionViewModel("Release", choices, false, true,
                new ActionCommand(() => Section!.IsExpanded = !Section.IsExpanded));
            launcher.DownloadInstallMinecraftSections.Add(Section);
            MotionDurations.ApplyRuntimePreferences(9);
            View = new DownloadInstallRightPaneView { DataContext = launcher };
            _window = new Window { Width = 700, Height = 400, Content = View };
            _window.Show();
            Tick();
        }

        public void Dispose()
        {
            _window.Close();
            MotionDurations.ApplyRuntimePreferences(_animationSpeed);
            _actions.FlushPendingSettings();
            _i18n.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}

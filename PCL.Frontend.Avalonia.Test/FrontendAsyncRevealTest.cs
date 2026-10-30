using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
using static PCL.Frontend.Avalonia.Test.FrontendMotionTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendAsyncRevealTest
{
    [TestMethod]
    [DataRow("resource")]
    [DataRow("catalog")]
    [DataRow("install")]
    public async Task DownloadPage_LateContentIsPrimedBeforeItBecomesVisibleAndRevealsOnce(string page)
    {
        await RunAsync(async () =>
        {
            using var fixture = new PageFixture();
            var (view, hostName, loadingField, properties) = page switch
            {
                "resource" => ((UserControl)new DownloadResourceRightPaneView(), "ContentHost", "_isDownloadResourceLoading",
                    new[] { nameof(LauncherViewModel.ShowDownloadResourceLoadingCard), nameof(LauncherViewModel.ShowDownloadResourceContent) }),
                "catalog" => (new DownloadCatalogRightPaneView(), "ContentHost", "_isDownloadCatalogLoading",
                    new[] { nameof(LauncherViewModel.ShowDownloadCatalogLoadingCard), nameof(LauncherViewModel.ShowDownloadCatalogContent) }),
                _ => (new DownloadInstallRightPaneView(), "MinecraftCatalogGroupListHost", "_isDownloadInstallMinecraftCatalogLoading",
                    new[] { nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalogLoading), nameof(LauncherViewModel.ShowDownloadInstallMinecraftCatalogContent) })
            };
            view.DataContext = fixture.Launcher;
            var window = new Window { Width = 700, Height = 500, Content = view };
            try
            {
                window.Show();
                await Advance(350);
                var host = view.FindControl<Control>(hostName)!;
                Assert.IsFalse(host.IsVisible);
                var revealOpacities = new List<double>();
                var opacityResets = 0;
                host.PropertyChanged += (_, change) =>
                {
                    if (change.Property == Visual.IsVisibleProperty && change.GetNewValue<bool>())
                        revealOpacities.Add(host.Opacity);
                    if (change.Property == Visual.OpacityProperty && change.GetNewValue<double>() == 0)
                        opacityResets++;
                };

                fixture.SetLoading(loadingField, false, properties);
                await Advance(650);
                Assert.HasCount(1, revealOpacities);
                Assert.AreEqual(0d, revealOpacities[0], "A late card must be hidden before its entrance is prepared.");
                Assert.IsTrue(host.IsVisible);
                Assert.AreEqual(1d, host.Opacity);
                Assert.IsTrue(host.GetVisualDescendants().OfType<PclCard>().All(card => card.Opacity == 1));

                var resetsAfterReveal = opacityResets;
                fixture.SetLoading(loadingField, false, properties);
                await Advance(350);
                Assert.HasCount(1, revealOpacities, "Repeated load notifications must not reveal the same card twice.");
                Assert.AreEqual(resetsAfterReveal, opacityResets, "Content that is already visible must not blink or restart its entrance.");
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task DownloadPage_RapidLoadingTogglesKeepTheContentVisible()
    {
        await RunAsync(async () =>
        {
            using var fixture = new PageFixture();
            var view = (UserControl)new DownloadResourceRightPaneView();
            view.DataContext = fixture.Launcher;
            var window = new Window { Width = 700, Height = 500, Content = view };
            try
            {
                window.Show();
                await Advance(350);
                var host = view.FindControl<Control>("ContentHost")!;
                var properties = new[]
                {
                    nameof(LauncherViewModel.ShowDownloadResourceLoadingCard),
                    nameof(LauncherViewModel.ShowDownloadResourceContent)
                };

                fixture.SetLoading("_isDownloadResourceLoading", false, properties);
                await Advance(650);
                Assert.IsTrue(host.IsVisible);
                Assert.AreEqual(1d, host.Opacity);

                // Two fast searches: the cached result reveals the list again while the
                // hide-to-loading transition of the previous state is still running.
                for (var i = 0; i < 3; i++)
                {
                    fixture.SetLoading("_isDownloadResourceLoading", true, properties);
                    await Advance(30);
                    fixture.SetLoading("_isDownloadResourceLoading", false, properties);
                    await Advance(30);
                }

                await Advance(650);
                Assert.IsTrue(host.IsVisible, "The resource list must stay visible after rapid searches.");
                Assert.AreEqual(1d, host.Opacity, "An interrupted reveal must not leave the list transparent.");
                Assert.IsTrue(host.GetVisualDescendants().OfType<PclCard>().All(card => card.Opacity == 1));
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task Motion_ManualRevealKeepsNewItemTemplatesHiddenUntilLayoutIsReady()
    {
        await RunAsync(async () =>
        {
            var host = new ItemsControl
            {
                IsVisible = false,
                ItemsSource = Enumerable.Range(0, 3).ToArray(),
                ItemTemplate = new FuncDataTemplate<int>((index, _) => new PclCard
                {
                    Header = $"Group {index}", CardContent = new Border { Height = 40 }
                })
            };
            Motion.SetStaggerChildren(host, true);
            Motion.SetStaggerStep(host, 0);
            var window = new Window { Width = 600, Height = 400, Content = host };
            try
            {
                window.Show();
                Tick();
                Motion.PrimeEnter(host);
                Assert.AreEqual(0d, host.Opacity);
                host.IsVisible = true;
                var enter = Motion.PlayEnterAsync(host);
                Assert.AreEqual(0d, host.Opacity, "Starting the entrance must not expose an unanimated frame.");
                Tick();
                await enter;
                Tick();
                var cards = host.GetVisualDescendants().OfType<PclCard>().ToArray();
                Assert.HasCount(3, cards);
                Assert.IsTrue(cards.All(CompositionOffsetAnimation.IsRunning), "Animate the realized cards after layout, rather than an empty items host.");
                await Advance(350);
                Assert.AreEqual(1d, host.Opacity);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task Motion_CardKeepsItsPreparedEntranceWhenShown()
    {
        await RunAsync(async () =>
        {
            var card = new PclCard { IsVisible = false, Header = "Versions", CardContent = new Border { Height = 80 } };
            var window = new Window { Width = 600, Height = 400, Content = card };
            try
            {
                window.Show();
                Tick();
                Motion.PrimeEnter(card);
                card.IsVisible = true;
                Assert.AreEqual(0d, card.Opacity, "Resetting a card's close state must not expose its prepared entrance.");
                await Motion.PlayEnterAsync(card);
                await Advance(350);
                Assert.AreEqual(1d, card.Opacity);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task Motion_ExplicitEntranceSupersedesAQueuedVisibilityEntrance()
    {
        await RunAsync(async () =>
        {
            var target = new Border { IsVisible = false, Height = 80 };
            Motion.SetAnimateOnVisible(target, true);
            var window = new Window { Width = 600, Height = 400, Content = target };
            try
            {
                window.Show();
                Tick();
                target.IsVisible = true;
                var revealed = false;
                var restarts = 0;
                target.PropertyChanged += (_, change) =>
                {
                    if (change.Property != Visual.OpacityProperty) return;
                    if (change.GetNewValue<double>() == 1) revealed = true;
                    else if (revealed && change.GetNewValue<double>() == 0) restarts++;
                };
                await Motion.PlayEnterAsync(target);
                await Advance(350);
                Assert.IsTrue(revealed);
                Assert.AreEqual(0, restarts, "A queued automatic entrance must not restart an explicit animation that has already begun.");
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task Motion_NewLoadNotificationBeforeLayoutDoesNotLeaveTheRevealHidden()
    {
        await RunAsync(async () =>
        {
            var target = new Border { IsVisible = false, Height = 80 };
            var window = new Window { Width = 600, Height = 400, Content = target };
            try
            {
                window.Show();
                Tick();
                var version = 0;
                var oldReveal = Motion.ShowAsync(target, () => version == 0);
                version++;
                var latestReveal = Motion.ShowAsync(target, () => version == 1);
                Tick();
                await Task.WhenAll(oldReveal, latestReveal);
                await Advance(350);
                Assert.IsTrue(target.IsVisible);
                Assert.AreEqual(1d, target.Opacity);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public async Task Motion_StaggerAnimatesACardAsAWholeWithoutMovingItsNestedCardsAgain()
    {
        await RunAsync(async () =>
        {
            var inner = new PclCard { Header = "Inner", CardContent = new Border { Height = 40 } };
            var outer = new PclCard { Header = "Outer", CardContent = inner };
            var host = new StackPanel { Children = { outer } };
            Motion.SetStaggerChildren(host, true);
            var window = new Window { Width = 600, Height = 400, Content = host };
            try
            {
                window.Show();
                Tick();
                await Motion.PlayEnterAsync(host);
                Tick();
                Assert.IsTrue(CompositionOffsetAnimation.IsRunning(outer));
                Assert.IsFalse(CompositionOffsetAnimation.IsRunning(inner), "Animating a card and its nested card doubles the same entrance motion.");
                await Advance(350);
            }
            finally { window.Close(); }
        });
    }

    private sealed class PageFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-async-reveal-test", Guid.NewGuid().ToString("N"));
        private readonly I18nService _i18n;
        private readonly LauncherActionService _actions;
        private readonly double _animationSpeed = MotionDurations.Source.DebugAnimationSpeed;
        public LauncherViewModel Launcher { get; }

        public PageFixture()
        {
            var paths = new FrontendRuntimePaths(_root, _root + "/temp", _root + "/data", _root + "/shared",
                _root + "/shared/config.json", _root + "/data/config.yml", _root + "/appdata", []);
            _i18n = new I18nService(new I18nSettingsManager(new FrontendConfigProviderAdapter(paths)));
            _actions = new LauncherActionService(paths, new FrontendPlatformAdapter(), () => { }, _i18n);
            Launcher = LauncherViewModel.CreateBootstrap(AvaloniaCommandParser.CreateDefaultOptions(), _actions, _i18n);
            foreach (var name in new[] { "_downloadResourceRefreshVersion", "_downloadCatalogRefreshVersion", "_downloadInstallMinecraftCatalogVersion" })
            {
                var field = typeof(LauncherViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
                field.SetValue(Launcher, (int)field.GetValue(Launcher)! + 1);
            }
            foreach (var field in new[] { "_isDownloadResourceLoading", "_isDownloadCatalogLoading", "_isDownloadInstallMinecraftCatalogLoading" })
                SetLoading(field, true, []);
            Launcher.DownloadCatalogSections.Clear();
            Launcher.DownloadCatalogSections.Add(new DownloadCatalogSectionViewModel("Versions", []));
            Launcher.DownloadInstallMinecraftSections.Clear();
            Launcher.DownloadInstallMinecraftSections.Add(new DownloadInstallMinecraftSectionViewModel("Latest", [], true, false, new ActionCommand(() => { })));
            MotionDurations.ApplyRuntimePreferences(9);
        }

        public void SetLoading(string field, bool loading, string[] properties)
        {
            typeof(LauncherViewModel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Launcher, loading);
            var notify = typeof(ViewModelBase).GetMethod("RaisePropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var property in properties) notify.Invoke(Launcher, [property]);
        }

        public void Dispose()
        {
            MotionDurations.ApplyRuntimePreferences(_animationSpeed);
            _actions.FlushPendingSettings();
            _i18n.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}

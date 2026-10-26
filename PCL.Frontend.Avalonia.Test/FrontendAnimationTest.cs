using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendAnimationTest
{
    [TestMethod]
    public async Task FrameAnimation_PausesForHiddenAncestorsAndDetachedOwners()
    {
        await RunOnUiThread(() =>
        {
            var owner = new Border();
            var parent = new Border { Child = owner };
            var window = new Window { Content = parent };
            var updates = 0;
            using var animation = new FrameAnimation(owner, _ => updates++);
            animation.IsEnabled = true;
            Assert.IsFalse(animation.IsRunning);
            window.Show();
            Tick();
            Assert.IsTrue(updates > 0);

            parent.IsVisible = false;
            var pausedUpdates = updates;
            Tick();
            Assert.AreEqual(pausedUpdates, updates);

            parent.IsVisible = true;
            Tick();
            Assert.IsTrue(updates > pausedUpdates);

            window.WindowState = WindowState.Minimized;
            pausedUpdates = updates;
            Tick();
            Assert.AreEqual(pausedUpdates, updates);
            window.WindowState = WindowState.Normal;
            Tick();
            Assert.IsTrue(animation.IsRunning);

            parent.Child = null;
            pausedUpdates = updates;
            Tick();
            Assert.AreEqual(pausedUpdates, updates);
            parent.Child = owner;
            Tick();
            Assert.IsTrue(updates > pausedUpdates);

            animation.Dispose();
            pausedUpdates = updates;
            Tick();
            Assert.AreEqual(pausedUpdates, updates);
            window.Close();
        });
    }

    [TestMethod]
    public async Task PaneHost_ReusesPresentersAndEvictsTheLeastRecentlyVisitedPage()
    {
        await RunOnUiThread(() =>
        {
            var host = new PclAnimatedRightPaneHost();
            host.DataTemplates.Add(new FuncDataTemplate<RightPaneViewModel>((_, _) => new TextBox()));
            var window = new Window { Content = host };
            window.Show();
            var pages = new RightPaneViewModel[]
            {
                new SetupUiRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupUi)),
                new SetupLaunchRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupLaunch)),
                new SetupLauncherMiscRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupLauncherMisc)),
                new SetupGameManageRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupGameManage)),
                new SetupAboutRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupAbout))
            };
            ShowPane(host, pages[0]);
            var originalPresenter = host.Children.Single();
            var originalEditor = host.GetVisualDescendants().OfType<TextBox>().Single();
            originalEditor.Text = "Preserved page state";
            ShowPane(host, pages[1]);
            ShowPane(host, pages[0]);
            Assert.AreSame(originalPresenter, host.Children.Single());
            Assert.AreSame(originalEditor, host.GetVisualDescendants().OfType<TextBox>().Single());
            Assert.AreEqual("Preserved page state", host.GetVisualDescendants().OfType<TextBox>().Single().Text);

            foreach (var page in pages.Skip(1))
            {
                ShowPane(host, page);
            }

            ShowPane(host, pages[0]);
            Assert.AreNotSame(originalPresenter, host.Children.Single());
            Assert.AreEqual(1, host.Children.Count);
            window.Close();
        });
    }

    [TestMethod]
    public async Task PaneHost_CoalescesRapidNavigationAndCanRevisitAfterLeavingTheStandardRoute()
    {
        await RunOnUiThread(() =>
        {
            var host = new PclAnimatedRightPaneHost();
            var builds = 0;
            host.DataTemplates.Add(new FuncDataTemplate<RightPaneViewModel>((_, _) =>
            {
                builds++;
                return new TextBox();
            }));
            var window = new Window { Content = host };
            window.Show();
            var first = new SetupUiRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupUi));
            var last = new SetupLaunchRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupLaunch));
            host.PaneContent = first;
            host.PaneContent = last;
            Tick();
            Assert.AreEqual(1, builds);
            var presenter = host.Children.Single();
            ShowPane(host, null);
            Assert.AreEqual(0, host.Children.Count);
            ShowPane(host, last);
            Assert.AreSame(presenter, host.Children.Single());
            Assert.AreEqual(1, builds);
            window.Close();
        });
    }

    [TestMethod]
    public async Task PaneHost_DoesNotRetainDynamicPagesOrReuseViewsAcrossDataContexts()
    {
        await RunOnUiThread(() =>
        {
            var host = new PclAnimatedRightPaneHost { DataContext = new object() };
            host.DataTemplates.Add(new FuncDataTemplate<RightPaneViewModel>((_, _) => new TextBox()));
            var window = new Window { Content = host };
            window.Show();
            var dynamicPage = new DownloadResourceRightPaneViewModel(null!,
                new StandardRightPaneDescriptor(StandardRightPaneKind.DownloadResource,
                    StandardRightPaneGroup.DownloadResource, "download", false));
            ShowPane(host, dynamicPage);
            var dynamicPresenter = host.Children.Single();
            ShowPane(host, null);
            ShowPane(host, dynamicPage);
            Assert.AreNotSame(dynamicPresenter, host.Children.Single());

            var settings = new SetupUiRightPaneViewModel(null!, Descriptor(StandardRightPaneKind.SetupUi));
            ShowPane(host, settings);
            var settingsPresenter = host.Children.Single();
            ShowPane(host, null);
            host.DataContext = new object();
            ShowPane(host, settings);
            Assert.AreNotSame(settingsPresenter, host.Children.Single());
            window.Close();
        });
    }

    [TestMethod]
    public async Task CollapsedCard_BuildsOptionalContentOnFirstExpansionAndRetainsIt()
    {
        await RunOnUiThread(() =>
        {
            var builds = 0;
            var card = new PclCard
            {
                ShowChevron = true,
                IsChevronExpanded = false,
                CardContent = new object(),
                CardContentTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    builds++;
                    return new TextBox();
                })
            };
            var window = new Window { Content = card };
            window.Show();
            Tick();
            Assert.AreEqual(0, builds);
            card.IsChevronExpanded = true;
            Tick();
            Assert.AreEqual(1, builds);
            card.IsChevronExpanded = false;
            card.IsChevronExpanded = true;
            Tick();
            Assert.AreEqual(1, builds);
            window.Content = null;
            window.Content = card;
            Tick();
            Assert.IsTrue(double.IsNaN(card.Height));
            window.Close();
        });
    }

    [TestMethod]
    public async Task HeadlessSession_ReusesWorkerAndIsolatesApplications()
    {
        Application? firstApplication = null;
        var firstThread = 0;
        await RunOnUiThread(() =>
        {
            firstApplication = Application.Current;
            firstThread = Environment.CurrentManagedThreadId;
            Assert.IsNotNull(firstApplication);
        });
        await RunOnUiThread(() =>
        {
            Assert.IsNotNull(Application.Current);
            Assert.AreNotSame(firstApplication, Application.Current);
            Assert.AreEqual(firstThread, Environment.CurrentManagedThreadId);
        });
    }

    private static Task RunOnUiThread(Action action)
    {
        return FrontendDeferredContentTest.RunOnUiThread(action);
    }

    private static StandardRightPaneDescriptor Descriptor(StandardRightPaneKind kind)
    {
        return new StandardRightPaneDescriptor(kind, StandardRightPaneGroup.SetupFamily, kind.ToString(), false);
    }

    private static void ShowPane(PclAnimatedRightPaneHost host, object? content)
    {
        host.PaneContent = content;
        Tick();
    }

    private static void Tick()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public sealed class AnimationTestApplication : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<AnimationTestApplication>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }
    }
}

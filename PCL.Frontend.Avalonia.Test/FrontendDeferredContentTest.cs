using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Controls;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendDeferredContentTest
{
    [TestMethod]
    public async Task DeferredSections_DeferOffscreenContentAndRetainEditsWhenRevisited()
    {
        await RunOnUiThread(() =>
        {
            var builds = 0;
            var sections = Enumerable.Range(0, 6).Select(index => new PclDeferredContentControl
            {
                Margin = new Thickness(0, 0, 0, index < 5 ? 15 : 0),
                DeferredTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    builds++;
                    return new TextBox { Text = $"Section {index}", Height = 200 };
                })
            }).ToArray();
            var items = new StackPanel();
            foreach (var section in sections) items.Children.Add(section);
            var scroll = new ScrollViewer { Content = items };
            var window = new Window { Width = 500, Height = 300, Content = scroll };
            window.Show();
            Tick();
            Assert.IsTrue(builds is > 0 and < 6, $"Built {builds} sections for the initial viewport.");
            var first = (TextBox)sections[0].Content!;
            first.Text = "An unsaved edit";

            sections[5].BringIntoView();
            Tick();
            Assert.IsNotNull(sections[5].Content);
            Assert.IsTrue(scroll.Offset.Y > 0);
            var offset = scroll.Offset;
            Tick();
            Assert.AreEqual(offset, scroll.Offset, "Scrolling should settle without another offset change.");

            scroll.ScrollToHome();
            Tick();
            Assert.AreSame(first, sections[0].Content);
            Assert.AreEqual("An unsaved edit", first.Text);
            Assert.AreEqual(0d, scroll.Offset.Y, .01);

            var built = builds;
            window.Content = null;
            window.Content = scroll;
            Tick();
            Assert.AreEqual(built, builds);
            Assert.AreSame(first, sections[0].Content);
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeferredSections_AllowTabbingIntoTheNextOffscreenSection()
    {
        await RunOnUiThread(() =>
        {
            var sections = Enumerable.Range(0, 3).Select(_ => new PclDeferredContentControl
            {
                DeferredTemplate = new FuncDataTemplate<object>((_, _) => new TextBox { Height = 220 })
            }).ToArray();
            var items = new StackPanel();
            foreach (var section in sections) items.Children.Add(section);
            var scroll = new ScrollViewer { Content = items };
            var window = new Window { Width = 500, Height = 180, Content = scroll };
            window.Show();
            Tick();
            Assert.IsNull(sections[1].Content, "The next section must still be unloaded before tabbing into it.");
            Assert.IsTrue(((TextBox)sections[0].Content!).Focus());
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Tick();
            Assert.IsNotNull(sections[1].Content);
            Assert.IsTrue(((TextBox)sections[1].Content!).IsFocused);
            Assert.IsTrue(scroll.Offset.Y > 0);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Tick();
            Assert.IsTrue(((TextBox)sections[0].Content!).IsFocused);
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeferredSection_UsesCurrentDataContextWhenCreatedAfterAChange()
    {
        await RunOnUiThread(() =>
        {
            object? builtWith = null;
            var section = new PclDeferredContentControl
            {
                DataContext = "old",
                DeferredTemplate = new FuncDataTemplate<object>((context, _) =>
                {
                    builtWith = context;
                    return new TextBox();
                })
            };
            section.DataContext = "new";
            var window = new Window { Content = section };
            window.Show();
            Tick();
            Assert.AreEqual("new", builtWith);
            Assert.IsNotNull(section.Content);
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeferredSections_PreserveTabOrderWithinSectionsAndSkipDisabledSections()
    {
        await RunOnUiThread(() =>
        {
            var sections = Enumerable.Range(0, 3).Select(index => new PclDeferredContentControl
            {
                IsEnabled = index != 1,
                DeferredTemplate = new FuncDataTemplate<object>((_, _) => new StackPanel
                {
                    Height = 220,
                    Children = { new TextBox { Height = 60 }, new TextBox { Height = 60 } }
                })
            }).ToArray();
            var items = new StackPanel();
            foreach (var section in sections) items.Children.Add(section);
            var scroll = new ScrollViewer { Content = items };
            var window = new Window { Width = 500, Height = 180, Content = scroll };
            window.Show();
            Tick();
            var firstInputs = ((StackPanel)sections[0].Content!).Children.Cast<TextBox>().ToArray();
            Assert.IsTrue(firstInputs[0].Focus());
            PressTab(window);
            Assert.IsTrue(firstInputs[1].IsFocused);
            Assert.IsNull(sections[2].Content);
            PressTab(window);
            Assert.IsNotNull(sections[2].Content);
            Assert.IsTrue(((StackPanel)sections[2].Content!).Children[0].IsFocused);
            PressTab(window, RawInputModifiers.Shift);
            Assert.IsTrue(firstInputs[1].IsFocused);
            PressTab(window, RawInputModifiers.Shift);
            Assert.IsTrue(firstInputs[0].IsFocused);
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeferredSections_WithDifferentHeightsSettleAfterScrollingAndResizing()
    {
        await RunOnUiThread(() =>
        {
            double[] heights = [120, 350, 80, 500, 150, 300];
            var builds = 0;
            var sections = heights.Select(height => new PclDeferredContentControl
            {
                DeferredTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    builds++;
                    return new TextBox { Height = height };
                })
            }).ToArray();
            var items = new StackPanel();
            foreach (var section in sections) items.Children.Add(section);
            var scroll = new ScrollViewer { Content = items };
            var window = new Window { Width = 500, Height = 240, Content = scroll };
            window.Show();
            Tick();
            Assert.IsTrue(builds < sections.Length);
            sections[3].BringIntoView();
            Tick();
            window.Width = 350;
            window.Height = 300;
            Tick();
            var offset = scroll.Offset;
            var extent = scroll.Extent;
            Tick();
            Assert.AreEqual(offset, scroll.Offset);
            Assert.AreEqual(extent, scroll.Extent);
            Assert.IsTrue(offset.Y >= 0 && offset.Y <= extent.Height - scroll.Viewport.Height);
            Assert.IsTrue(sections[3].IsEffectivelyVisible);
            sections[5].BringIntoView();
            Tick();
            scroll.ScrollToHome();
            Tick();
            Assert.AreEqual(0d, scroll.Offset.Y, .01);
            Assert.AreEqual(heights[0], ((TextBox)sections[0].Content!).Bounds.Height, .01);
            window.Close();
        });
    }

    private static void PressTab(Window window, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(PhysicalKey.Tab, modifiers);
        window.KeyReleaseQwerty(PhysicalKey.Tab, modifiers);
        Tick();
    }

    [TestMethod]
    public async Task DeferredGroups_BuildAtMostOnePerFrameAndCancelWhenDetached()
    {
        await RunOnUiThread(() =>
        {
            var builds = 0;
            var sections = Enumerable.Range(0, 3).Select(_ => new PclDeferredContentControl
            {
                EstimatedHeight = 60,
                BuildOnNextFrame = true,
                DeferredTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    builds++;
                    return new TextBox { Height = 60 };
                })
            }).ToArray();
            var panel = new StackPanel();
            foreach (var section in sections) panel.Children.Add(section);
            var window = new Window { Height = 500, Width = 300, Content = new ScrollViewer { Content = panel } };
            window.Show();
            window.UpdateLayout();
            for (var frame = 0; frame < 8 && builds < 3; frame++)
            {
                var before = builds;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(builds - before <= 1, "Visible groups must not all build in one frame.");
            }
            Assert.AreEqual(3, builds);

            var cancelledBuilds = 0;
            var cancelled = new PclDeferredContentControl
            {
                BuildOnNextFrame = true,
                DeferredTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    cancelledBuilds++;
                    return new TextBox();
                })
            };
            window.Content = cancelled;
            window.UpdateLayout();
            window.Content = null;
            Tick();
            Assert.AreEqual(0, cancelledBuilds);
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeferredGroups_InItemTemplatesSupportTabbingIntoAnUnloadedGroup()
    {
        await RunOnUiThread(() =>
        {
            var items = new ItemsControl
            {
                ItemsSource = new[] { "one", "two", "three" },
                ItemTemplate = new FuncDataTemplate<string>((_, _) => new PclDeferredContentControl
                {
                    EstimatedHeight = 220,
                    BuildOnNextFrame = true,
                    DeferredTemplate = new FuncDataTemplate<object>((_, _) => new TextBox { Height = 220 })
                })
            };
            var window = new Window { Width = 300, Height = 180, Content = new ScrollViewer { Content = items } };
            window.Show();
            Tick();
            var groups = items.GetVisualDescendants().OfType<PclDeferredContentControl>().ToArray();
            Assert.AreEqual(3, groups.Length);
            Assert.IsNull(groups[1].Content);
            Assert.IsTrue(((TextBox)groups[0].Content!).Focus());
            PressTab(window);
            Assert.IsTrue(((TextBox)groups[1].Content!).IsFocused);
            PressTab(window, RawInputModifiers.Shift);
            Assert.IsTrue(((TextBox)groups[0].Content!).IsFocused);
            window.Close();
        });
    }

    internal static void Tick()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    internal static async Task RunOnUiThread(Action action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(FrontendAnimationTest.AnimationTestApplication));
        try
        {
            await session.Dispatch(action, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await Task.Run(session.Dispose).ConfigureAwait(false);
        }
    }
}

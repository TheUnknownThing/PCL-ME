using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using static PCL.Frontend.Avalonia.Test.FrontendDeferredContentTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendMotionTest
{
    [TestMethod]
    public async Task SelectionBar_KeepsLayoutStableAndResetsWhenRecycled()
    {
        await RunAsync(async () =>
        {
            var bar = new Border { Width = 5 };
            SelectionBarMotion.Initialize(bar);
            bool? selected = null;
            SelectionBarMotion.Apply(bar, ref selected, false, 32);
            var window = new Window { Content = new Grid { Children = { bar } } };
            window.Show();
            Tick();
            var layouts = 0;
            window.LayoutUpdated += (_, _) => layouts++;
            for (var i = 0; i < 6; i++)
            {
                SelectionBarMotion.Apply(bar, ref selected, i % 2 == 0, 32);
                await Advance(50);
                Assert.AreEqual(32d, bar.Bounds.Height);
            }
            Assert.AreEqual(0, layouts, "Selection must not invalidate layout on each animation frame.");

            selected = null;
            SelectionBarMotion.Apply(bar, ref selected, true, 40);
            Assert.AreEqual(1d, ((ScaleTransform)bar.RenderTransform!).ScaleY);
            Assert.AreEqual(1d, bar.Opacity);
            window.Close();
        });
    }

    [TestMethod]
    public async Task Motion_PreservesTransformsAndLayoutOffsetsAcrossExitAndReentry()
    {
        await RunAsync(async () =>
        {
            var scale = new ScaleTransform(.98, .98);
            var target = new Border { Height = 60, RenderTransform = scale, Background = Brushes.Blue };
            var panel = new StackPanel { Children = { new Border { Height = 80 }, target } };
            var window = new Window { Width = 500, Height = 300, Content = panel };
            window.Show();
            Tick();
            var opacityChanges = 0;
            target.PropertyChanged += (_, change) =>
            {
                if (change.Property == Visual.OpacityProperty) opacityChanges++;
            };
            await Motion.PlayEnterAsync(target);
            await Advance(50);
            Assert.AreSame(target, window.InputHitTest(new Point(250, 110)), "The animated row must retain its layout offset.");
            await Advance(300);
            Assert.AreSame(scale, target.RenderTransform, "Entrance motion must preserve press and hover transforms.");
            Assert.AreEqual(1d, target.Opacity);
            Assert.AreEqual(80d, ElementComposition.GetElementVisual(target)!.Offset.Y);
            Assert.IsTrue(opacityChanges < 5, "The UI opacity property must not be updated on every render tick.");

            await Motion.PlayExitAsync(target);
            Assert.AreEqual(0d, target.Opacity);
            target.IsVisible = false;
            target.IsVisible = true;
            await Motion.PlayEnterAsync(target);
            await Advance(350);
            Assert.AreEqual(1d, target.Opacity);
            Assert.AreSame(scale, target.RenderTransform);
            window.Close();
        });
    }

    [TestMethod]
    public async Task Motion_CancelsDelayedStaggersWhenDetachedAndCanBeReused()
    {
        await RunAsync(async () =>
        {
            var panel = new StackPanel { Children = { new Border { Height = 50 }, new Border { Height = 50 } } };
            Motion.SetStaggerChildren(panel, true);
            Motion.SetStaggerStep(panel, 100);
            var window = new Window { Content = panel };
            window.Show();
            Tick();
            var enter = Motion.PlayEnterAsync(panel);
            Tick();
            window.Content = null;
            await enter;
            await Advance(150);
            Assert.IsTrue(panel.Children.All(child => child.Opacity == 1));
            window.Content = panel;
            Tick();
            await Motion.PlayEnterAsync(panel);
            await Advance(350);
            Assert.IsTrue(panel.Children.All(child => child.Opacity == 1));
            window.Close();
        });
    }

    [TestMethod]
    public async Task Motion_EntranceCleanupDoesNotResetANewerLayoutAnimation()
    {
        await RunAsync(async () =>
        {
            var target = new Border { Height = 60 };
            var window = new Window { Content = target };
            window.Show();
            Tick();
            await Motion.PlayEnterAsync(target);
            var visual = ElementComposition.GetElementVisual(target)!;
            var offset = visual.Compositor.CreateVector3DKeyFrameAnimation();
            offset.Duration = TimeSpan.FromSeconds(1);
            offset.InsertKeyFrame(0f, new Vector3D(0, 100, 0));
            offset.InsertKeyFrame(1f, new Vector3D(0, 200, 0));
            var version = CompositionOffsetAnimation.Start(target, offset);
            await Advance(50);
            visual.Offset = new Vector3D(0, 123, 0);
            await Advance(350);
            Assert.AreEqual(123d, visual.Offset.Y, "An older entrance cleanup must not reset the layout offset.");
            CompositionOffsetAnimation.Stop(target, version);
            Assert.AreEqual(target.Bounds.Y, visual.Offset.Y);
            window.Close();
        });
    }

    [TestMethod]
    public async Task Card_ExpandsAndCollapsesWithoutPerFrameLayoutAndRetainsEdits()
    {
        await RunAsync(async () =>
        {
            var editor = new TextBox { Text = "Unsaved edit", Height = 250 };
            var card = new PclCard { Header = "Card", ShowChevron = true, IsChevronExpanded = false, CardContent = editor };
            var following = new TextBox { Height = 40 };
            var panel = new StackPanel { Children = { card, following } };
            var window = new Window { Width = 500, Height = 500, Content = new ScrollViewer { Content = panel } };
            window.Show();
            Tick();
            var collapsedY = following.Bounds.Y;
            var layouts = 0;
            window.LayoutUpdated += (_, _) => layouts++;
            card.IsChevronExpanded = true;
            await Advance(350);
            Assert.IsTrue(double.IsNaN(card.Height));
            Assert.IsTrue(following.Bounds.Y > collapsedY + 200);
            Assert.IsTrue(layouts < 8, $"Expansion performed {layouts} layout passes.");

            layouts = 0;
            card.IsChevronExpanded = false;
            await Advance(300);
            Assert.AreEqual(40d, card.Height);
            Assert.IsFalse(card.IsContentVisible);
            Assert.AreEqual(collapsedY, following.Bounds.Y, .01);
            Assert.AreEqual(following.Bounds.Y, ElementComposition.GetElementVisual(following)!.Offset.Y, .01);
            Assert.IsTrue(layouts < 8, $"Collapse performed {layouts} layout passes.");
            card.IsChevronExpanded = true;
            await Advance(350);
            Assert.AreEqual("Unsaved edit", editor.Text);
            Assert.IsFalse(card.FindControl<Grid>("CardLayoutRoot")!.ClipToBounds);
            window.Close();
        });
    }

    [TestMethod]
    public async Task Card_HoverOutlineDoesNotRemeasureTheCard()
    {
        await RunAsync(async () =>
        {
            var card = new PclCard { Header = "Card", CardContent = new Border { Height = 120 } };
            var window = new Window
            {
                Width = 500, Height = 400,
                Content = new StackPanel { Children = { card } }
            };
            window.Show();
            Tick();
            await Advance(300);
            var layouts = 0;
            window.LayoutUpdated += (_, _) => layouts++;
            window.MouseMove(new Point(100, 20));
            await Advance(300);
            window.MouseMove(new Point(450, 350));
            await Advance(300);
            Assert.AreEqual(0, layouts, "Fading the hover outline must not animate layout-affecting border thickness.");
            window.Close();
        });
    }

    [TestMethod]
    public async Task Card_ReversesAndDetachesWithoutLeavingClipsOrSiblingOffsets()
    {
        await RunAsync(async () =>
        {
            var card = new PclCard
            {
                Header = "Card", ShowChevron = true, IsChevronExpanded = false,
                CardContent = new Border { Height = 500 }
            };
            var following = new Border { Height = 50 };
            var panel = new StackPanel { Children = { card, following } };
            var window = new Window { Width = 500, Height = 500, Content = panel };
            window.Show();
            Tick();
            card.IsChevronExpanded = true;
            await Advance(40);
            card.IsChevronExpanded = false;
            await Advance(20);
            card.IsChevronExpanded = true;
            await Advance(350);
            Assert.IsTrue(double.IsNaN(card.Height));
            Assert.AreEqual(following.Bounds.Y, ElementComposition.GetElementVisual(following)!.Offset.Y, .01);
            card.IsChevronExpanded = false;
            await Advance(20);
            window.Content = null;
            window.Content = panel;
            Tick();
            await Advance(200);
            Assert.AreEqual(40d, card.Height);
            Assert.IsFalse(card.FindControl<Grid>("CardLayoutRoot")!.ClipToBounds);
            Assert.AreEqual(following.Bounds.Y, ElementComposition.GetElementVisual(following)!.Offset.Y, .01);
            window.Close();
        });
    }

    [TestMethod]
    public async Task Card_VirtualizedContentSettlesAfterFirstExpansion()
    {
        await RunAsync(async () =>
        {
            var speed = MotionDurations.Source.DebugAnimationSpeed;
            MotionDurations.ApplyRuntimePreferences(30);
            try
            {
                var items = new ItemsControl
                {
                    ItemsSource = Enumerable.Range(0, 1000).ToArray(),
                    ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { CacheLength = .5 }),
                    ItemTemplate = new FuncDataTemplate<int>((index, _) => new Border { Height = index == 0 ? 200 : 36 })
                };
                var card = new PclCard { Header = "Group", ShowChevron = true, IsChevronExpanded = false, CardContent = items };
                var following = new Border { Height = 40 };
                var scroll = new ScrollViewer { Content = new StackPanel { Children = { card, following } } };
                var window = new Window { Width = 500, Height = 400, Content = scroll };
                window.Show();
                Tick();
                card.IsChevronExpanded = true;
                await Advance(350);
                Assert.IsTrue(double.IsNaN(card.Height));
                Assert.AreEqual(card.Bounds.Bottom, following.Bounds.Y, .01);
                Assert.IsTrue(items.GetRealizedContainers().Count() < 50);
                scroll.ScrollToEnd();
                Tick();
                var extent = scroll.Extent;
                Tick();
                Assert.AreEqual(extent, scroll.Extent);
                card.IsChevronExpanded = false;
                await Advance(350);
                Assert.AreEqual(40d, card.Height);
                Assert.AreEqual(40d, following.Bounds.Y);
                window.Close();
            }
            finally
            {
                MotionDurations.ApplyRuntimePreferences(speed);
            }
        });
    }

    internal static async Task Advance(int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        do
        {
            Tick();
            await Task.Delay(16);
        } while (DateTime.UtcNow < deadline);
        Tick();
    }

    internal static async Task RunAsync(Func<Task> action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(FrontendAnimationTest.AnimationTestApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                await action();
                // Let cancellation continuations drain before the session
                // tears down its dispatcher and font manager.
                await Task.Yield();
                Tick();
                return true;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await Task.Run(session.Dispose).ConfigureAwait(false);
        }
    }
}
